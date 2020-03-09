using System;
using System.ComponentModel.Design;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using WinSCP;
using Task = System.Threading.Tasks.Task;

namespace VSRemoteDebugger
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal sealed class RemoteDebugCommand
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 0x0100;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("5b4eaa99-73ea-49a5-99c3-bd64eecafa37");

		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		private readonly AsyncPackage _package;

		public static BuildEvents BuildEvents { get; set; }

		private LocalHost _lh;

		private bool _isBuildSucceeded;

		/// <summary>
		/// Initializes a new instance of the <see cref="RemoteDebugCommand"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="commandService">Command service to add command to, not null.</param>
		private RemoteDebugCommand(AsyncPackage package, OleMenuCommandService commandService)
		{
			_package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new MenuCommand(this.Execute, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static RemoteDebugCommand Instance
		{
			get;
			private set;
		}

		/// <summary>
		/// Wrapper around a (alert) messagebox
		/// </summary>
		/// <param name="message"></param>
		private void Mbox(string message) => VsShellUtilities.ShowMessageBox(
			_package,
			message,
			"Error",
			OLEMSGICON.OLEMSGICON_CRITICAL,
			OLEMSGBUTTON.OLEMSGBUTTON_OK,
			OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary> 
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in RemoteDebug's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(false) as OleMenuCommandService;
			Instance = new RemoteDebugCommand(package, commandService);
		}


		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		private void Execute(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			InitSolution();
			MkDir();
			Clean();
			Build(); // once this finishes it will raise an event; see BuildEvents_OnBuildDone
		}

		private void InitSolution()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
			var project = dte.Solution.GetStartupProject();

			if(project == null)
			{
				Mbox("No startup project selected");
			}

			_lh = new LocalHost();

			_lh.ProjectFullName = project.FullName;
			_lh.ProjectName = project.Name;
			_lh.SolutionFullName = dte.Solution.FullName;
			_lh.SolutionDirPath = Path.GetDirectoryName(_lh.SolutionFullName);
			_lh.ProjectConfigName = project.ConfigurationManager.ActiveConfiguration.ConfigurationName;
			_lh.OutputDirName = project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
			_lh.OutputDirFullName = Path.Combine(Path.GetDirectoryName(project.FullName), _lh.OutputDirName);

			string text = $"ProjectFullName: {_lh.ProjectFullName} \nProjectName: {_lh.ProjectName} \n" +
				$"SolutionFullName: {_lh.SolutionFullName} \nSolutionDirPath:{_lh.SolutionDirPath} \n" +
				$"ProjectConfigname: {_lh.ProjectConfigName} \nOutputDirName: {_lh.OutputDirName} \nOutputDirFullName: {_lh.OutputDirFullName}";
		}

		/// <summary>
		/// create debug/release folders and take ownership
		/// </summary>
		private void MkDir()  
		{ 
			$"sudo mkdir -p {Remote.DebugFolderPath}".Bash(); 
			$"sudo mkdir -p {Remote.ReleaseFolderPath}".Bash();
			$"sudo chown -R {Remote.HostName}:{Remote.GroupName} {Remote.MasterFolderPath}".Bash();
		}

		/// <summary>
		/// clean everything in the debug directory
		/// </summary>
		private void Clean() => $"sudo rm -rf {Remote.DebugFolderPath}/*".Bash();

		private void InstallVSDbg()
		{

		}

		private void Build()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
			BuildEvents = dte.Events.BuildEvents;
			BuildEvents.OnBuildDone += this.BuildEvents_OnBuildDone;
			BuildEvents.OnBuildProjConfigDone += this.BuildEvents_OnBuildProjConfigDone;
			dte.SuppressUI = false;
			dte.Solution.SolutionBuild.BuildProject(_lh.ProjectConfigName, _lh.ProjectFullName);
		}

		private void TransferFiles()
		{
			try
			{
				// Setup session options
				var sessionOptions = new SessionOptions
				{
					Protocol = Protocol.Scp,
					HostName = Remote.IP,
					UserName = Remote.HostName,
					SshPrivateKeyPath = LocalHost.SSH_KEY_PATH + ".ppk",
					SshHostKeyFingerprint = "ssh-ed25519 255 LCIQQ26tv55Ap0KFtnwPGa03wLaZkhDbGUG1aqS7qeg=",
				};

				using(var session = new Session())
				{
					// Connect
					session.Open(sessionOptions);

					// Upload files
					var transferOptions = new TransferOptions
					{
						TransferMode = TransferMode.Binary
					};

					var transferResult = session.PutFilesToDirectory(_lh.OutputDirFullName, Remote.DebugFolderPath, null, false, transferOptions);

					if(!transferResult.IsSuccess)
					{
						throw new Exception("Error transferring the files to: " + Remote.DebugFolderPath);
					}

					// Throw on any error
					transferResult.Check();

				}
			}
			catch(Exception e)
			{
				Mbox(e.Message);
			}
		}

		private void Debug()
		{
			var dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
			string jsonPath = _lh.GenJson();
			dte.ExecuteCommand("DebugAdapterHost.Launch", $"/LaunchJson:\"{jsonPath}\"");

			File.Delete(jsonPath);
		}

		private void Cleanup()
		{
			BuildEvents.OnBuildDone -= this.BuildEvents_OnBuildDone;
			BuildEvents.OnBuildProjConfigDone -= this.BuildEvents_OnBuildProjConfigDone;
		}

		/// <summary>
		/// The build is finised sucessfully only when the startup project has been compiled without any errors
		/// </summary>
		private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			if(!success)
			{
				Mbox($"Build for project {project} failed");
			}

			string msg = $"Project: {project} --- Success: {success.ToString()}\n";
			_isBuildSucceeded = Path.GetFileName(project) == _lh.ProjectName + ".csproj" && success;
		}

		/// <summary>
		/// Build finished. We can now transfer the files to the remote host and start debugging the program
		/// </summary>
		private void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
		{
			if(_isBuildSucceeded)
			{
				TransferFiles();
				Debug();
				Cleanup();
			}
		}
	}
}
