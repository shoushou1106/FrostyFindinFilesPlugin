using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            UpdateUI();
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

            try
            {
                IndexLibrary.SetEnumerateSearch(Find_TextBox.Text,
                    isCaseSensitive.IsChecked.GetValueOrDefault(false),
                    isMatchWholeWord.IsChecked.GetValueOrDefault(false),
                    isRegularExpressions.IsChecked.GetValueOrDefault(false));
                resultEnumerator = IndexLibrary.EnumerateResult.GetEnumerator();
            }
            catch { }
        }

        #region - Bottom bar -

        private void FindAllButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();

            StringBuilder sb = new StringBuilder();

            List<EbxAssetEntry> result = new List<EbxAssetEntry>();
            string searchFor = Find_TextBox.Text;
            bool caseSensitive = isCaseSensitive.IsChecked.GetValueOrDefault(false);
            bool matchWholeWord = isMatchWholeWord.IsChecked.GetValueOrDefault(false);
            bool regularExpressions = isRegularExpressions.IsChecked.GetValueOrDefault(false);

            // setup ability to cancel the process
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, searchFor, "", (task) =>
            {
                try
                {
                    result = IndexLibrary.SearchAll(searchFor, cancelToken.Token, task.TaskLogger,
                        caseSensitive,
                        matchWholeWord,
                        regularExpressions);
                }
                catch (OperationCanceledException) {}

            }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            ResultAssetListView.ItemsSource = result;

            GC.Collect();
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();

            try
            {
                FrostyTaskWindow.Show(this, Find_TextBox.Text, "Finding", (task) =>
                {
                    resultEnumerator.MoveNext();
                });

                ResultAssetListView.ItemsSource = new List<EbxAssetEntry>() { resultEnumerator.Current };
            }
            catch (Exception ex)
            {
                FrostyExceptionBox.Show(ex, "Find in Files Plugin");
            }

            GC.Collect();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // setup ability to cancel the process
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Saving results", "", (task) =>
            {
                try
                {
                    task.Update("Ask for filename");
                    FrostySaveFileDialog dialog = new FrostySaveFileDialog("Save Result", "Plain Text|*.txt", "FindinFiles_Result", Find_TextBox.Text);

                    if (dialog.ShowDialog())
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        uint totalCount = (uint)((List<EbxAssetEntry>)ResultAssetListView.ItemsSource).Count;
                        uint index = 0;

                        using (StreamWriter writer = new StreamWriter(dialog.FileName))
                        {
                            foreach (EbxAssetEntry entry in ResultAssetListView.ItemsSource)
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();
                                task.Update(entry.Name, (index++ / (double)totalCount) * 100.0d);
                                writer.WriteLine(entry.Name);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });

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

        #endregion

        #region - Index -

        private void IndexButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();

            // setup ability to cancel the process
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            string title = IndexLibrary.isIndexInitialized ? "Deleting index" : "Creating index";

            FrostyTaskWindow.Show(this, "Creating index", "", (task) =>
            {
                try
                {
                    IndexLibrary.InitializeIndex(cancelToken.Token, task.TaskLogger);
                }
                catch (OperationCanceledException) { }

            }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());

            UpdateUI();
            GC.Collect();
        }

        private void SaveIndexButton_Click(object sender, RoutedEventArgs e)
        {
            FrostyTaskWindow.Show(this, "Saving index", "", (task) =>
            {
                task.Update("Ask for filename");
                FrostySaveFileDialog dialog = new FrostySaveFileDialog("Save index to file", "Json|*.json", "FindinFiles_Index", "index");

                if (dialog.ShowDialog())
                {
                    task.Update("Writing json file");
                    IndexLibrary.IndexToJson(dialog.FileName);
                }
            });

            GC.Collect();
        }

        private void LoadIndexButton_Click(object sender, RoutedEventArgs e)
        {
            FrostyTaskWindow.Show(this, "Loading index", "", (task) =>
            {
                task.Update("Ask for filename");
                FrostyOpenFileDialog dialog = new FrostyOpenFileDialog("Load index from file", "Json|*.json", "FindinFiles_Index")
                {
                    Multiselect = false
                };

                if (dialog.ShowDialog())
                {
                    task.Update("Reading json file");
                    IndexLibrary.JsonToIndex(dialog.FileName);
                }
            });

            UpdateUI();
            GC.Collect();
        }

        #endregion

        /// <summary>
        /// Update Layout
        /// <para/>
        /// <para>idk why the fucking Binding cant update so i write this shit to update only TWO BUTTON</para>
        /// i searched a lot and try it for few hours and now i know that PropertyChanged and UpdateLayout and UpdateTarget are all not working for this
        /// </summary>
        private void UpdateUI()
        {
            IndexButton.Content = IndexLibrary.isIndexInitialized ? "Delete Index" : "Create Index";
            SaveIndexButton.IsEnabled = IndexLibrary.isIndexInitialized;
        }
    }
}
