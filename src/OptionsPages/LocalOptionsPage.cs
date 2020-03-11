using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace VSRemoteDebugger.OptionsPages
{
    public class LocalOptionsPage : DialogPage
    {
        [Category("Local Machine Settings")]
        [DisplayName("Include wwwroot")]
        [Description("If the startup folder has a wwwroot folder, it is included in the build output")]
        public bool Includewwwroot { get; set; } = false;
    }
}
