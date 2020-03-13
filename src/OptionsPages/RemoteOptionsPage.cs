using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace VSRemoteDebugger.OptionsPage
{
    public class RemoteOptionsPage : DialogPage
    {
        [Category("Remote Machine Settings")]
        [DisplayName("IP Address")]
        [Description("Remote Linux IP Address")]
        public string IP { get; set; } = "192.168.0.10";

        [Category("Remote Machine Settings")]
        [DisplayName("User Name")]
        [Description("Remote Machine User Name")]
        public string UserName { get; set; } = "pi";

        [Category("Remote Machine Settings")]
        [DisplayName("Group Name")]
        [Description("Remote Machine Group Name")]
        public string GroupName { get; set; } = "pi";

        [Category("Remote Machine Settings")]
        [DisplayName("Visual Studio Debugger Path")]
        [Description("Remote Machine Visual Studio Debugger Path")]
        public string VsDbgPath { get; set; } = "~/.vsdbg/vsdbg";

        [Category("Remote Machine Settings")]
        [DisplayName("Project folder")]
        [Description("Master folder for the transferred files")]
        public string AppFolderPath { get; set; } = "/var/proj";
    }
}
