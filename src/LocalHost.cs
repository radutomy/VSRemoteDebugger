using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace VSRemoteDebugger
{
    internal class LocalHost
    {
        internal static string DEBUG_ADAPTER_HOST_FILENAME => "launch.json";
        internal static string HOME_DIR_PATH => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        internal static string SSH_KEY_PATH => Path.Combine(HOME_DIR_PATH, ".ssh\\id_rsa");

        internal string DebugAdapterHostFilePath { get; set; }
        internal string ProjectName { get; set; }
        internal string ProjectFullName { get; set; }
        internal string SolutionFullName { get; set; }
        internal string SolutionDirPath { get; set; }
        internal string ProjectConfigName { get; set; }
        internal string OutputDirName { get; set; }
        internal string OutputDirFullName { get; set; }

        /// <summary>
        /// Generates a temporary json file and returns its path
        /// </summary>
        /// <returns>Full path to the generated json file</returns>
        internal string GenJson()
        {
            dynamic json = new JObject();
            json.version = "0.2.0";
            json.adapter = "c:\\windows\\sysnative\\openssh\\ssh.exe";
            json.adapterArgs = $"-i ~\\.ssh\\id_rsa {Remote.HostName}@{Remote.IP} {Remote.VsDbgPath} --interpreter=vscode";

            json.configurations = new JArray() as dynamic;
            dynamic config = new JObject();
            config.project = "default";
            config.type = "coreclr";
            config.request = "launch";
            config.program = "dotnet";
            config.args = new JArray($"./{ProjectName}.dll");
            config.cwd = Remote.DebugFolderPath;
            json.configurations.Add(config);

            string tempJsonPath = Path.Combine(Path.GetTempPath(), DEBUG_ADAPTER_HOST_FILENAME);
            File.WriteAllText(tempJsonPath, Convert.ToString(json));

            return tempJsonPath;
        }
    }
}
