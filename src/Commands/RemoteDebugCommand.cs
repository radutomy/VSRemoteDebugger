using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Renci.SshNet;
using Task = System.Threading.Tasks.Task;
using Process = System.Diagnostics.Process;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

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

		private VSRemoteDebuggerPackage Settings => _package as VSRemoteDebuggerPackage;
		private LocalHost _localhost;
		private bool _isBuildSucceeded;

		public static BuildEvents BuildEvents { get; set; }

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
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in RemoteDebug's constructor requires the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(false) as OleMenuCommandService;
			Instance = new RemoteDebugCommand(package, commandService);
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static RemoteDebugCommand Instance{ get; private set; }

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
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		private async void Execute(object sender, EventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			bool connected = await Task.Run(() => CheckConnWithRemote()).ConfigureAwait(true);

			if(!connected)
			{
				Mbox($"Cannot connect to {Settings.UserName}:{Settings.IP}. Connection refused");
			}
			else
			{
				if (!InitSolution())
				{
					Mbox("Please select a startup project");
				}
				else
				{
					await Task.Run(() =>
					{
						TryInstallVsDbg();
						MkDir();
						Clean();

					}).ConfigureAwait(true);

					if (!Settings.Publish)
					{
						Build(); // once this finishes it will raise an event; see BuildEvents_OnBuildDone
					}
					else
					{
						int exitcode = await PublishAsync().ConfigureAwait(true);

						if (exitcode != 0)
						{
							Mbox("File transfer to Remote Machine failed");
						}
						else
						{
							exitcode = await TransferFilesAsync().ConfigureAwait(true);

							if (exitcode != 0)
							{
								Mbox("Build failed");
							}
							else
							{
								Debug();
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Must be called on the UI thread
		/// </summary>
		private bool InitSolution()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
			var project = dte.Solution.GetStartupProject();

			if (project == null)
			{
				return false;
			}

			_localhost = new LocalHost(Settings.UserName, Settings.IP, Settings.VsDbgPath, Settings.DotnetPath, Settings.DebugFolderPath);
			
			_localhost.ProjectFullName = project.FullName;
			_localhost.ProjectName = project.Name;
			_localhost.Assemblyname = project.Properties.Item("AssemblyName").Value.ToString();
			_localhost.SolutionFullName = dte.Solution.FullName;
			_localhost.SolutionDirPath = Path.GetDirectoryName(_localhost.SolutionFullName);
			_localhost.ProjectConfigName = project.ConfigurationManager.ActiveConfiguration.ConfigurationName;
			_localhost.OutputDirName = project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
			_localhost.OutputDirFullName = Path.Combine(Path.GetDirectoryName(project.FullName), _localhost.OutputDirName);
			
			if (Settings.UseCommandLineArgs)
			{
				_localhost.CommandLineArguments = GetArgs(_localhost.SolutionDirPath);
			}

			string debugtext = $"ProjectFullName: {_localhost.ProjectFullName} \nProjectName: {_localhost.ProjectName} \n" +
				$"SolutionFullName: {_localhost.SolutionFullName} \nSolutionDirPath:{_localhost.SolutionDirPath} \n" +
				$"ProjectConfigname: {_localhost.ProjectConfigName} \nOutputDirName: {_localhost.OutputDirName} \nOutputDirFullName: {_localhost.OutputDirFullName}";

			return true;
		}

		/// <summary>
		/// Checks if a connection with the remote machine can be established
		/// </summary>
		/// <returns></returns>
		private bool CheckConnWithRemote()
		{
			try
			{
				Bash("echo hello");
				return true;
			}
			catch(Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// create debug/release folders and take ownership
		/// </summary>
		private void MkDir()
		{
			Bash($"sudo mkdir -p {Settings.DebugFolderPath}");
			Bash($"sudo mkdir -p {Settings.ReleaseFolderPath}");
			Bash($"sudo chown -R {Settings.UserName}:{Settings.GroupName} {Settings.AppFolderPath}");
		}

		/// <summary>
		/// clean everything in the debug directory
		/// </summary>
		private void Clean() => Bash($"sudo rm -rf {Settings.DebugFolderPath}/*");

		/// <summary>
		/// Instals VS Debugger if it doesn't exist already
		/// </summary>
		private void TryInstallVsDbg() => Bash("[ -d ~/.vsdbg ] || curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -r linux-arm -v latest -l ~/.vsdbg");

		private void Build()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = (DTE)Package.GetGlobalService(typeof(DTE));
			BuildEvents = dte.Events.BuildEvents;
			BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;
			BuildEvents.OnBuildProjConfigDone += BuildEvents_OnBuildProjConfigDone;
			dte.SuppressUI = false;
			dte.Solution.SolutionBuild.BuildProject(_localhost.ProjectConfigName, _localhost.ProjectFullName);
		}

		/// <summary>
		/// Publish the solution. Publishing is done via an external process
		/// </summary>
		/// <returns></returns>
		private async Task<int> PublishAsync()
		{
			using(var process = new Process())
			{
				process.StartInfo.FileName = "dotnet";
				process.StartInfo.Arguments = $@"publish -c {_localhost.ProjectConfigName} -r linux-arm --self-contained=false -o {_localhost.OutputDirFullName} {_localhost.ProjectFullName}";
				process.StartInfo.CreateNoWindow = true;
				process.Start();

				return await process.WaitForExitAsync().ConfigureAwait(true);
			}
		}

		/// <summary>
		/// Transfers the files to remote asynchronously. The transfer is done via an external process
		/// </summary>
		/// <returns></returns>
		private async Task<int> TransferFilesAsync()
		{
			try
			{
				using (var process = new Process())
				{
					process.StartInfo = new ProcessStartInfo
					{
						FileName = "c:\\windows\\sysnative\\openssh\\scp.exe",
						Arguments = $@"-pr {_localhost.OutputDirFullName}\* {Settings.UserName}@{Settings.IP}:{Settings.DebugFolderPath}",
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true,
					};

					process.Start();
					return await process.WaitForExitAsync().ConfigureAwait(true);
				}
			}
			catch(Exception)
			{
				return -1;
			}
		}

		/// <summary>
		/// Start debugging using the remote visual studio server adapter
		/// </summary>
		private void Debug()
		{
			var dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
			string jsonPath = _localhost.GenJson();
			dte.ExecuteCommand("DebugAdapterHost.Launch", $"/LaunchJson:\"{jsonPath}\"");

			File.Delete(jsonPath);
		}

		private void Cleanup()
		{
			BuildEvents.OnBuildDone -= BuildEvents_OnBuildDone;
			BuildEvents.OnBuildProjConfigDone -= BuildEvents_OnBuildProjConfigDone;
		}

		private void Bash(string cmd)
		{
			var connInfo = new PrivateKeyFile[] { new PrivateKeyFile(LocalHost.SSH_KEY_PATH) };

			try
			{
				using (var client = new SshClient(Settings.IP, Settings.UserName, connInfo))
				{
					client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
					client.Connect();
					var result = client.RunCommand(cmd);
					client.Disconnect();
				}
			}
			catch(Exception)
			{
				throw;
			}
		}

		/// <summary>
		/// The build is finised sucessfully only when the startup project has been compiled without any errors
		/// </summary>
		private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			string debugtext = $"Project: {project} --- Success: {success}\n";

			if (!success)
			{
				Cleanup();
			}

			_isBuildSucceeded = Path.GetFileName(project) == _localhost.ProjectName + ".csproj" && success;
		}

		/// <summary>
		/// Build finished. We can now transfer the files to the remote host and start debugging the program
		/// </summary>
		private async void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
		{
			if (_isBuildSucceeded)
			{
				int exitcode = await TransferFilesAsync().ConfigureAwait(true);

				if (exitcode == 0)
				{
					Debug();
					Cleanup();
				}
				else
				{
					Mbox("Transferring files failed");
				}
			}
		}

		/// <summary>
		/// Tries to search for a file "launchSettings.json", which is used by Visual Studio to store the command line arguments
		/// </summary>
		/// <param name="slnDirPath"></param>
		/// <remarks>If using multiple debugging profiles, this will not work</remarks>
		/// <returns>The debugging command arguments set in Project Settings -> Debug -> Command Line Arguments</returns>
		private string GetArgs(string slnDirPath)
		{
			string args = String.Empty;
			string[] launchSettingsOccurences = Directory.GetFiles(slnDirPath, "launchSettings.json", SearchOption.AllDirectories);

			if (launchSettingsOccurences.Length == 1)
			{
				var jobj = JObject.Parse(File.ReadAllText(launchSettingsOccurences[0]));

				var commandLineArgsOccurences = jobj.SelectTokens("$..commandLineArgs")
					.Select(t => t.Value<string>())
					.ToList();

				if (commandLineArgsOccurences.Count > 1)
				{
					Mbox("Multiple debugging profiles detected. Due to Visual Studio API limitations, command line arguments cannot be used. \n" +
						 "Please turn off Command Line Arguments in the extension settings page");
				}
				else if (commandLineArgsOccurences.Count == 1)
				{
					args = commandLineArgsOccurences[0];
				}
			}
			else if (launchSettingsOccurences.Length > 1)
			{
				Mbox("Cannot read command line arguments");
			}

			return args;
		}
	}
}
