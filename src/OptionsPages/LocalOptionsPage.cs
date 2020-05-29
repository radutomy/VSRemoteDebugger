using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace VSRemoteDebugger.OptionsPages
{
    public class LocalOptionsPage : DialogPage
    {
        [Category("Local Machine Settings")]
        [DisplayName("Publish")]
        [Description("Publishes the solution rather than building it. Useful for ASP.NET/Blazor projects. Only compatible with .NET Core due to inherit limitations in Visual Studio")]
        public bool Publish { get; set; } = false;

        [Category("Local Machine Settings")]
        [DisplayName("UseCommandLineArguments")]
        [Description("Uses command line arguments picked up from (Visual Studio) Project Settings -> Debugging -> Command Line Arguments." +
            "Does not work properly if using more than one debugging profiles, please set to false if that is the case")]
        public bool UseCommandLineArgs { get; set; } = false;
    }
}
