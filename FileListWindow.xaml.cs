using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Documents;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Imaging.Interop;

namespace OpenFileFromDir
{
    namespace WPFHelpers
    {
        class FormattedTextHelper
        {
            public static string GetFormattedText(DependencyObject obj)
            {
                return obj.GetValue(FormattedTextProperty) as string;
            }

            public static void SetFormattedText(DependencyObject obj, string val)
            {
                obj.SetValue(FormattedTextProperty, val);
            }

            public static readonly DependencyProperty FormattedTextProperty =
                DependencyProperty.RegisterAttached("FormattedText", typeof(string), typeof(FormattedTextHelper),
                    new UIPropertyMetadata("", FormattedTextChanged));

            private static void FormattedTextChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
            {
                var value = args.NewValue as string;
                var textBlock = obj as TextBlock;

                if (textBlock != null)
                {
                    textBlock.Inlines.Clear();

                    bool bold = false;
                    string cur = "";
                    for (int i=0; i<value.Length; ++i)
                    {
                        if (value[i] == '!')
                        {
                            if (!bold)
                            {
                                // staring bold
                                if (cur.Length != 0)
                                {
                                    textBlock.Inlines.Add(cur);
                                    cur = "";
                                }
                                bold = true;
                            }
                            ++i;
                        }
                        else
                        {
                            if (bold)
                            {
                                // stopping bold
                                if (cur.Length != 0)
                                {
                                    textBlock.Inlines.Add(new Bold(new Run(cur)));
                                    cur = "";
                                }
                                bold = false;
                            }
                        }

                        cur += value[i];
                    }

                    // add final
                    if (cur.Length != 0)
                    {
                        if (bold)
                        {
                            textBlock.Inlines.Add(new Bold(new Run(cur)));
                        }
                        else
                        {
                            textBlock.Inlines.Add(cur);
                        }
                    }
                }
            }
        }
    }

    public partial class FileListWindow : Window
    {
        public FileListWindow(FilteredListProvider filteredListProvider, OpenFileFromDirPackage package, IVsImageService2 imageService)
        {
            this.package = package;
            this.filteredListProvider = filteredListProvider;
            this.imageService = imageService;

            InitializeComponent();

            filterTextBox.Text = "";
            filterTextBox.Focus();

            UpdateListBox("");
        }

        public class ListItem
        {
            public ListItem(int rootLen, FilteredListProvider.FilteredEntry e, ImageMoniker icon)
            {
                var formattedRel = Path.GetDirectoryName(e.fullPath.Substring(rootLen + 1)) + Path.DirectorySeparatorChar;
                string formattedFilename = e.filename;
                if (e.matchType == FilteredListProvider.FilteredEntry.MatchType.FileOnly || e.matchType == FilteredListProvider.FilteredEntry.MatchType.Recent)
                {
                    for (int i = e.matchPositions.Count-1; i >= 0; --i)
                    {
                        var pos = e.matchPositions[i];
                        formattedFilename = formattedFilename.Insert(pos, "!");
                    }
                }
                else
                {
                    for (int i = e.matchPositions.Count - 1; i >= 0; --i)
                    {
                        var pos = e.matchPositions[i];
                        if (pos >= formattedRel.Length)
                        {
                            pos -= formattedRel.Length;
                            formattedFilename = formattedFilename.Insert(pos, "!");
                        }
                        else
                        {
                            formattedRel = formattedRel.Insert(pos, "!");
                        }
                    }
                }

                Filename = formattedFilename;
                RelPath = formattedRel;
                FullPath = e.fullPath;

                Recent = e.matchType == FilteredListProvider.FilteredEntry.MatchType.Recent ? "Recent" : "";

                Icon = icon;
            }

            public string Filename { get; set; }
            public string RelPath { get; set; }
            public string FullPath { get; set; }
            public string Recent { get; set; }

            public ImageMoniker Icon { get; set; }
        }

        private void OpenSelectedFile()
        {
            var path = (listBox.SelectedItem as ListItem).FullPath;
            package.DoOpenFile(path);
        }

        void UpdateListBox(string filter)
        {
            var filteredEntries = filteredListProvider.GetFilteredEntries(filter);

            listBox.Items.Clear();

            if (filteredEntries.Count > 0)
            {
                var rootLen = filteredListProvider.GetRootPath().Length;
                foreach (var e in filteredEntries)
                {
                    listBox.Items.Add(new ListItem(rootLen, e, imageService.GetImageMonikerForFile(e.filename)));
                }
                listBox.SelectedIndex = 0;
            }
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs args)
        {
            UpdateListBox(filterTextBox.Text);
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
                Close();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Close();
            }
        }

        private OpenFileFromDirPackage package;
        private FilteredListProvider filteredListProvider;
        private IVsImageService2 imageService;
    }
}
