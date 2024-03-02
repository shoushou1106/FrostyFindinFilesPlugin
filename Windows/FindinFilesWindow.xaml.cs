using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;

namespace FindinFilesPlugin.Windows
{
    /// <summary>
    /// Interaction logic for FindinFilesWindow.xaml
    /// </summary>
    public partial class FindinFilesWindow : FrostyDockableWindow
    {
        public FindinFilesWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handle Watermark for Find_TextBox
        /// </summary>
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
        }

        /// <summary>
        /// Handle hyperlink
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            debugSpinner.Visibility = Visibility.Visible;

            //Search(Find_TextBox.Text);
            StringBuilder sb = new StringBuilder();

            //FrostyMessageBox.Show("search for lower case" + Environment.NewLine + Find_TextBox.Text.ToLower());
            string searchFor = Find_TextBox.Text;
            FrostyTaskWindow.Show(this, searchFor, "", (task) =>
            {
                uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
                uint index = 0;

                foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                {
                    //FrostyMessageBox.Show(EbxToString(entry));
                    task.Update("Checking: " + entry.Path, (index++ / (double)totalCount) * 100.0d);

                    if (EbxToString(entry).ToLower().Contains(searchFor.ToLower()))
                    {
                        //FrostyMessageBox.Show(entry.Path);
                        sb.AppendLine(entry.Path);
                    }
                }
            });

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Plain text (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog.ShowDialog();
            System.IO.File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

            debugSpinner.Visibility = Visibility.Hidden;
            GC.Collect();
        }

        private string EbxToString(EbxAssetEntry entry)
        {
            //EbxAsset asset = App.AssetManager.GetEbx(entry);
            //XmlSerializer x = new XmlSerializer(typeof(object));
            //using (var memoryStream = new MemoryStream())
            //{
            //    EbxXmlWriter ebxXmlWriter = new EbxXmlWriter(memoryStream, App.AssetManager);
            //    ebxXmlWriter.WriteObjects(asset.Objects);
            //    memoryStream.Position = 0;
            //    StreamReader streamReader = new StreamReader(memoryStream);
            //    return streamReader.ReadToEnd();
            //}

            EbxAsset asset = App.AssetManager.GetEbx(entry);
            StringBuilder sb = new StringBuilder();
            foreach (var Object in asset.Objects)
            {
                sb.Append(ClassToString(Object));
            }
            return sb.ToString();
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

        //private async void Search(string str)
        //{
        //    await Task.Run(() =>
        //    {
        //        IEnumerable<EbxAssetEntry> searchResult =
        //            from entry in App.AssetManager.EnumerateEbx()
        //            where ToXml(entry).ToLower().Contains(str)
        //            select entry;
        //        this.Dispatcher.Invoke(() =>
        //        {
        //            ResultAssetListView.ItemsSource = searchResult;
        //        });
        //    });
        //}

        // This is mainly from FrostySdk.IO.EbxXmlWriter
        private string ClassToString(object Obj)
        {
            Type ObjType = Obj.GetType();
            StringBuilder SB = new StringBuilder();
            int TotalCount = ObjType.GetProperties().Length;

            PropertyInfo[] Properties = ObjType.GetProperties();
            Array.Sort(Properties, new PropertyComparer());

            string StrGuid = "";
            FieldInfo FI = ObjType.GetField("__Guid", BindingFlags.NonPublic | BindingFlags.Instance);

            if (FI != null)
            {
                AssetClassGuid Guid = (AssetClassGuid)FI.GetValue(Obj);
                StrGuid = Guid.ToString();
            }

            if (TotalCount != 0 && (Properties.Length > 0 || (ObjType.BaseType != typeof(object) && ObjType.BaseType != typeof(ValueType))))
            {
                SB.AppendLine(ObjType.Name);
                SB.AppendLine(StrGuid);

                foreach (PropertyInfo PI in Properties)
                {
                    if (PI.GetCustomAttribute<IsTransientAttribute>() != null)
                        continue;

                    SB.Append(PI.Name + "[AddInfo]");

                    object Value = PI.GetValue(Obj);
                    string Tmp = "";

                    SB.Append(FieldToString(Value, ref Tmp));

                    SB = SB.Replace("[AddInfo]", Tmp);
                }
            }
            else
            {

                SB.AppendLine(ObjType.Name);
                SB.AppendLine(StrGuid);
            }

            return SB.ToString();
        }

        // This is mainly from FrostySdk.IO.EbxXmlWriter
        private string FieldToString(object Value, ref string AdditionalInfo)
        {
            Type FieldType = Value.GetType();
            StringBuilder SB = new StringBuilder();

            if (FieldType.Name == "List`1")
            {
                int Count = (int)FieldType.GetMethod("get_Count").Invoke(Value, null);
                AdditionalInfo = Count.ToString() + Environment.NewLine;

                if (Count > 0)
                {
                    SB.AppendLine();

                    for (int i = 0; i < Count; i++)
                    {
                        SB.Append(i.ToString());

                        object SubValue = FieldType.GetMethod("get_Item").Invoke(Value, new object[] { i });
                        string Tmp = "";

                        SB.Append(FieldToString(SubValue, ref Tmp));

                        SB.AppendLine();
                    }
                }
            }
            else
            {
                if (FieldType.Namespace == "FrostySdk.Ebx" && FieldType.BaseType != typeof(Enum))
                {
                    if (FieldType == typeof(CString)) SB.Append(Value.ToString());
                    else if (FieldType == typeof(ResourceRef)) SB.Append(Value.ToString());
                    else if (FieldType == typeof(FileRef)) SB.Append(Value.ToString());
                    else if (FieldType == typeof(TypeRef)) SB.Append(Value.ToString());
                    else if (FieldType == typeof(BoxedValueRef)) SB.Append(Value.ToString());
                    else if (FieldType == typeof(PointerRef))
                    {
                        PointerRef Reference = (PointerRef)Value;
                        if (Reference.Type == PointerRefType.Internal)
                        {
                            Type SubObjType = Reference.Internal.GetType();
                            AssetClassGuid guid = (AssetClassGuid)SubObjType.GetField("__Guid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Reference.Internal);
                            SB.Append("[" + SubObjType.Name + "] " + guid.ToString());
                        }
                        else if (Reference.Type == PointerRefType.External)
                        {
                            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(Reference.External.FileGuid);
                            if (entry != null)
                            {
                                SB.Append("[Ebx] " + entry.Name + " [" + Reference.External.ClassGuid + "]");
                            }
                            else
                            {
                                SB.Append("[Ebx] BadRef " + Reference.External.FileGuid + "/" + Reference.External.ClassGuid);
                            }
                        }
                        else
                            SB.Append("nullptr");
                    }
                    else
                    {
                        SB.AppendLine();

                        SB.Append(ClassToString(Value));
                    }
                }
                else
                {
                    if (FieldType == typeof(byte)) SB.Append(((byte)Value).ToString("X2"));
                    else if (FieldType == typeof(ushort)) SB.Append(((ushort)Value).ToString("X4"));
                    else if (FieldType == typeof(uint))
                    {
                        uint value = (uint)Value;
                        string val = Utils.GetString((int)value);

                        if (!val.StartsWith("0x"))
                        {
                            SB.Append(val + " [" + value.ToString("X8") + "]");
                        }
                        else
                        {
                            SB.Append(val);
                        }
                    }
                    else if (FieldType == typeof(int))
                    {
                        int value = (int)Value;
                        string val = Utils.GetString(value);

                        if (!val.StartsWith("0x"))
                        {
                            SB.Append(val + " [" + value.ToString("X8") + "]");
                        }
                        else
                        {
                            SB.Append(val);
                        }
                    }
                    else if (FieldType == typeof(ulong)) SB.Append(((ulong)Value).ToString("X16"));
                    else if (FieldType == typeof(float)) SB.Append(((float)Value).ToString());
                    else if (FieldType == typeof(double)) SB.Append(((double)Value).ToString());
                    else SB.Append(Value.ToString());
                }
            }

            return SB.ToString();
        }
    }
}
