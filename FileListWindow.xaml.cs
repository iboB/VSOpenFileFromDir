using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;

namespace OpenFileFromDir
{
    public partial class FileListWindow : Window
    {
        public FileListWindow(FilteredListProvider filteredListProvider)
        {
            this.filteredListProvider = filteredListProvider;

            InitializeComponent();

            filterTextBox.Text = "";
            filterTextBox.Focus();
        }

        private void filterTextChanged(object sender, TextChangedEventArgs args)
        {
            var filteredEntries = filteredListProvider.GetFilteredEntries(filterTextBox.Text);

            listBox.Items.Clear();

            foreach(var e in filteredEntries)
            {
                listBox.Items.Add(e.filename);
            }
        }
        private void Window_KeyDown(object sender, KeyEventArgs args)
        {

        }

        private FilteredListProvider filteredListProvider;
    }
}
