using System;
using System.Globalization;
using System.Windows.Forms;

namespace VSRemoteDebugger.ToolBar
{
    [ProvideToolboxControl("VSRemoteDebugger.ToolBar.TheToolbar", false)]
    public partial class TheToolbar : UserControl
    {
        public TheToolbar()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentUICulture, "We are inside {0}.Button1_Click()", this.ToString()));
        }
    }
}
