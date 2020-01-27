using System.Diagnostics;
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

        private void OpenSelectedFile()
        {
            Debug.WriteLine($"Selected {listBox.SelectedItem}");
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs args)
        {
            var filteredEntries = filteredListProvider.GetFilteredEntries(filterTextBox.Text);

            listBox.Items.Clear();

            if (filteredEntries.Count > 0)
            {
                foreach (var e in filteredEntries)
                {
                    listBox.Items.Add(e.filename);
                }
                listBox.SelectedIndex = 0;
            }
        }

        private void OnFilterKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Down)
            {
                args.Handled = true;
                int i = listBox.SelectedIndex;
                if (i != -1)
                {
                    ++i;
                    if (i == listBox.Items.Count)
                    {
                        i = 0;
                    }
                    listBox.SelectedIndex = i;
                }
            }
            else if (args.Key == Key.Up)
            {
                args.Handled = true;
                int i = listBox.SelectedIndex;
                if (i != -1)
                {
                    --i;
                    if (i == -1)
                    {
                        i = listBox.Items.Count;
                    }
                    listBox.SelectedIndex = i;
                }
            }
        }
        private void OnWindowKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter || args.Key == Key.Return)
            {
                args.Handled = true;
                OpenSelectedFile();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Close();
            }
        }

        private FilteredListProvider filteredListProvider;
    }
}
