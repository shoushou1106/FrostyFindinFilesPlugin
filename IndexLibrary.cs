using Frosty.Core.Controls;
using Frosty.Core;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using FrostySdk.Interfaces;

namespace FindinFilesPlugin
{
    public static class IndexLibrary
    {
        public static IEnumerable<EbxAssetEntry> EnumerateResult { get; set; }
        public static bool isInitialized { get; private set; }

        private static Dictionary<Guid, string> ebxAssetIndex = new Dictionary<Guid, string>();

        /// <summary>
        /// Use LINQ search
        /// </summary>
        /// <param name="Key">Search Key Word</param>
        public async static void SetEnumerateSearch(string Key, bool isCaseSensitive, bool isMatchWholeWord, bool isRegularExpressions)
        {
            await Task.Run(() =>
            {
                if (isRegularExpressions)
                {
                    EnumerateResult =
                        from entry in App.AssetManager.EnumerateEbx()
                        where Regex.IsMatch(EbxToString(entry), Key)
                        select entry;
                }
                else
                {
                    RegexOptions options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    string pattern = isMatchWholeWord ? $@"\b{Key}\b" : Key;

                    EnumerateResult =
                        from entry in App.AssetManager.EnumerateEbx()
                        where Regex.IsMatch(EbxToString(entry), pattern, options)
                        select entry;
                }
            });
        }

        /// <summary>
        /// Use foreach search all
        /// </summary>
        /// <param name="Key">Search Key Word</param>
        public static List<EbxAssetEntry> SearchAll(string Key, CancellationToken cancelToken, ILogger inLogger, bool isCaseSensitive, bool isMatchWholeWord, bool isRegularExpressions)
        {
            List<EbxAssetEntry> result = new List<EbxAssetEntry>();
            uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
            uint index = 0;

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
            {
                inLogger.Log(entry.Path + entry.Name);
                inLogger.Log("progress:" + (index++ / (double)totalCount) * 100.0d);

                if (isRegularExpressions)
                {
                    if (Regex.IsMatch(EbxToString(entry), Key)) 
                    {
                        result.Add(entry);
                    }
                }
                else
                {
                    RegexOptions options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    string pattern = isMatchWholeWord ? $@"\b{Key}\b" : Key;

                    if (Regex.IsMatch(EbxToString(entry), pattern, options))
                    {
                        result.Add(entry);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Convert EbxAssetEntry to string
        /// </summary>
        public static string EbxToString(EbxAssetEntry entry)
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

        /// <summary>
        /// This is mainly from FrostySdk.IO.EbxXmlWriter
        /// </summary>
        private static string ClassToString(object Obj)
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

        /// <summary>
        /// This is mainly from FrostySdk.IO.EbxXmlWriter
        /// </summary>
        private static string FieldToString(object Value, ref string AdditionalInfo)
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
