using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace FindinFilesPlugin.Windows
{
    /// <summary>
    /// Interaction logic for FindinFilesWindow.xaml
    /// </summary>
    public partial class FindinFilesWindow : FrostyDockableWindow
    {
        private IEnumerator<EbxAssetEntry> resultEnumerator;

        public FindinFilesWindow()
        {
            InitializeComponent();
        }

        private void Find_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(Find_TextBox.Text))
            {
                Find_TextBox_Watermark.Visibility = Visibility.Visible;
            }
            else
            {
                Find_TextBox_Watermark.Visibility = Visibility.Collapsed;
            }

            IndexLibrary.SetEnumerateSearch(Find_TextBox.Text,
                isCaseSensitive.IsChecked.GetValueOrDefault(false),
                isMatchWholeWord.IsChecked.GetValueOrDefault(false),
                isRegularExpressions.IsChecked.GetValueOrDefault(false));
            resultEnumerator = IndexLibrary.EnumerateResult.GetEnumerator();
        }

        private void FindAllButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();

            StringBuilder sb = new StringBuilder();

            string searchFor = Find_TextBox.Text;

            // setup ability to cancel the process
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, searchFor, "", (task) =>
            {
                IndexLibrary.SearchAll(searchFor, cancelToken.Token, task.TaskLogger,
                    isCaseSensitive.IsChecked.GetValueOrDefault(false),
                    isMatchWholeWord.IsChecked.GetValueOrDefault(false),
                    isRegularExpressions.IsChecked.GetValueOrDefault(false));
            }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            GC.Collect();
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();

            FrostyTaskWindow.Show(this, Find_TextBox.Text, "Finding", (task) =>
            {
                resultEnumerator.MoveNext();
            });

            ResultAssetListView.ItemsSource = new List<EbxAssetEntry>() { resultEnumerator.Current };

            GC.Collect();
        }

        private void ResultAssetListOpenItem_Click(object sender, RoutedEventArgs e)
        {
            if (ResultAssetListView.SelectedItem == null)
                return;
            App.EditorWindow.OpenAsset(ResultAssetListView.SelectedItem);
        }

        private void ResultAssetListFindItem_Click(object sender, RoutedEventArgs e)
        {
            if (ResultAssetListView.SelectedItem == null)
                return;
            App.EditorWindow.DataExplorer.SelectAsset(ResultAssetListView.SelectedItem);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            FrostySaveFileDialog dialog = new FrostySaveFileDialog("Save Result", "Plain Text|*.txt", "FindinFiles_Result", Find_TextBox.Text);

            if (dialog.ShowDialog())
            {
                using (StreamWriter writer = new StreamWriter(dialog.FileName))
                {
                    foreach (EbxAssetEntry entry in ResultAssetListView.ItemsSource)
                    {
                        writer.WriteLine(entry.Filename);
                    }
                }
            }
        }

        /// <summary>
        /// Handle hyperlink
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

    }
}
