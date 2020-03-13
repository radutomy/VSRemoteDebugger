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
    }
}
