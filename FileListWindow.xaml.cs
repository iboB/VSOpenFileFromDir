using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;

namespace OpenFileFromDir
{
    public partial class FileListWindow : System.Windows.Window
    {
        public FileListWindow()
        {
            InitializeComponent();

            filterTextBox.Text = "";
            filterTextBox.Focus();
        }
        private void Window_KeyDown(object sender, KeyEventArgs args)
        {

        }

        public IVsUIShell shell;
    }
}
