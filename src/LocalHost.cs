using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace VSRemoteDebugger
{
    internal class LocalHost
    {
        public static string DEBUG_ADAPTER_HOST_FILENAME => "launch.json";
        public static string HOME_DIR_PATH => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string SSH_KEY_PATH => Path.Combine(HOME_DIR_PATH, ".ssh\\id_rsa");

        public string DebugAdapterHostFilePath { get; set; }
        public string ProjectName { get; set; }
        public string ProjectFullName { get; set; }
        public string SolutionFullName { get; set; }
        public string SolutionDirPath { get; set; }
        public string ProjectConfigName { get; set; }
        public string OutputDirName { get; set; }
        public string OutputDirFullName { get; set; }

        /// <summary>
        /// Generates a temporary json file and returns its path
        /// </summary>
        /// <returns>Full path to the generated json file</returns>
        public string GenJson()
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
