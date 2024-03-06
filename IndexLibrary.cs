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
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using Newtonsoft.Json.Linq;

namespace FindinFilesPlugin
{  
    public static class IndexLibrary
    {
        public static IEnumerable<EbxAssetEntry> EnumerateResult { get; set; }
        public static bool isIndexInitialized { get; private set; }

        private static Dictionary<Guid, string> ebxAssetIndex { get; set; } = new Dictionary<Guid, string>();

        #region - Search -

        /// <summary>
        /// Use LINQ search
        /// </summary>
        /// <param name="Key">Search Key Word</param>
        public async static void SetEnumerateSearch(string Key, bool isCaseSensitive, bool isMatchWholeWord, bool isRegularExpressions, string lookIn)
        {
            await Task.Run(() =>
            {
                if (isIndexInitialized)
                {
                    if (isRegularExpressions)
                    {
                        EnumerateResult =
                            from entry in ebxAssetIndex
                            where Regex.IsMatch(entry.Value, Key)
                            select App.AssetManager.GetEbxEntry(entry.Key);
                    }
                    else
                    {
                        RegexOptions options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        string pattern = isMatchWholeWord ? $@"\b{Key}\b" : Key;

                        EnumerateResult =
                            from entry in ebxAssetIndex
                            where Regex.IsMatch(entry.Value, pattern, options)
                            select App.AssetManager.GetEbxEntry(entry.Key);
                    }
                }
                else
                {
                    if (isRegularExpressions)
                    {
                        EnumerateResult =
                            from entry in App.AssetManager.EnumerateEbx()
                            where entry.Path.StartsWith(lookIn)
                            where Regex.IsMatch(EbxToString(entry), Key)
                            select entry;
                    }
                    else
                    {
                        RegexOptions options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        string pattern = isMatchWholeWord ? $@"\b{Key}\b" : Key;

                        EnumerateResult =
                            from entry in App.AssetManager.EnumerateEbx()
                            where entry.Path.StartsWith(lookIn)
                            where Regex.IsMatch(EbxToString(entry), pattern, options)
                            select entry;
                    }
                }
            });
        }

        /// <summary>
        /// Use foreach search all
        /// </summary>
        /// <param name="Key">Search Key Word</param>
        public static List<EbxAssetEntry> SearchAll(string Key, CancellationToken cancelToken, ILogger inLogger, bool isCaseSensitive, bool isMatchWholeWord, bool isRegularExpressions, string lookIn)
        {
            cancelToken.ThrowIfCancellationRequested();
            List<EbxAssetEntry> result = new List<EbxAssetEntry>();

            if (isIndexInitialized)
            {
                uint totalCount = (uint)ebxAssetIndex.Count;
                uint index = 0;
                cancelToken.ThrowIfCancellationRequested();

                foreach (KeyValuePair<Guid, string> entry in ebxAssetIndex)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    inLogger.Log(entry.Key.ToString());
                    inLogger.Log("progress:" + (index++ / (double)totalCount) * 100.0d);

                    if (isRegularExpressions)
                    {
                        if (Regex.IsMatch(entry.Value, Key))
                        {
                            result.Add(App.AssetManager.GetEbxEntry(entry.Key));
                        }
                    }
                    else
                    {
                        RegexOptions options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        string pattern = isMatchWholeWord ? $@"\b{Key}\b" : Key;

                        if (Regex.IsMatch(entry.Value, pattern, options))
                        {
                            result.Add(App.AssetManager.GetEbxEntry(entry.Key));
                        }
                    }
                }
            }
            else
            {
                uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
                uint index = 0;
                cancelToken.ThrowIfCancellationRequested();

                foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                {
                    cancelToken.ThrowIfCancellationRequested();
                    inLogger.Log(entry.Name);
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
            }

            return result;
        }

        #endregion

        #region - Index -

        public static void InitializeIndex(CancellationToken cancelToken, ILogger inLogger)
        {
            ebxAssetIndex.Clear();

            if (isIndexInitialized)
            {
                isIndexInitialized = false;
                return; 
            }
            uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
            uint index = 0;
            cancelToken.ThrowIfCancellationRequested();

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
            {
                cancelToken.ThrowIfCancellationRequested();
                inLogger.Log(entry.Name);
                inLogger.Log("progress:" + (index++ / (double)totalCount) * 100.0d);

                ebxAssetIndex.Add(entry.Guid, EbxToString(entry));
            }

            isIndexInitialized = true;
        }

        public static void IndexToJson(string fileName)
        {
            if (isIndexInitialized == false)
                throw new ArgumentNullException();

            //return JsonConvert.SerializeObject(ebxAssetIndex, Formatting.Indented);

            using (StreamWriter writer = new StreamWriter(fileName))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();

                jsonWriter.Formatting = Formatting.Indented;
                jsonSerializer.Formatting = Formatting.Indented;

                jsonSerializer.Serialize(jsonWriter, ebxAssetIndex);

                jsonWriter.Flush();
                jsonWriter.Close();
            }
        }

        public static void JsonToIndex(string fileName)
        {
            ebxAssetIndex.Clear();

            //ebxAssetIndex = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(json);

            using (StreamReader reader = new StreamReader(fileName))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();

                ebxAssetIndex = jsonSerializer.Deserialize<Dictionary<Guid, string>>(jsonReader);

                jsonReader.Close();
            }

            if (ebxAssetIndex is null)
            {
                ebxAssetIndex = new Dictionary<Guid, string>();
                isIndexInitialized = false;
                return;
            }

            isIndexInitialized = true;
        }

        #endregion

        #region - Convert -

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

                    SB.AppendLine(PI.Name);
                    SB.AppendLine("[AddInfo]");

                    object Value = PI.GetValue(Obj);
                    string Tmp = "";

                    SB.AppendLine(FieldToString(Value, ref Tmp));

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
                AdditionalInfo = Count.ToString();

                if (Count > 0)
                {
                    for (int i = 0; i < Count; i++)
                    {
                        SB.AppendLine(i.ToString());

                        object SubValue = FieldType.GetMethod("get_Item").Invoke(Value, new object[] { i });
                        string Tmp = "";

                        SB.AppendLine(FieldToString(SubValue, ref Tmp));
                    }
                }
            }
            else
            {
                if (FieldType.Namespace == "FrostySdk.Ebx" && FieldType.BaseType != typeof(Enum))
                {
                    if (FieldType == typeof(CString)) SB.AppendLine(Value.ToString());
                    else if (FieldType == typeof(ResourceRef)) SB.AppendLine(Value.ToString());
                    else if (FieldType == typeof(FileRef)) SB.AppendLine(Value.ToString());
                    else if (FieldType == typeof(TypeRef)) SB.AppendLine(Value.ToString());
                    else if (FieldType == typeof(BoxedValueRef)) SB.AppendLine(Value.ToString());
                    else if (FieldType == typeof(PointerRef))
                    {
                        PointerRef Reference = (PointerRef)Value;
                        if (Reference.Type == PointerRefType.Internal)
                        {
                            Type SubObjType = Reference.Internal.GetType();
                            AssetClassGuid guid = (AssetClassGuid)SubObjType.GetField("__Guid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Reference.Internal);
                            SB.AppendLine(SubObjType.Name);
                            SB.AppendLine(guid.ToString());
                        }
                        else if (Reference.Type == PointerRefType.External)
                        {
                            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(Reference.External.FileGuid);
                            if (entry != null)
                            {
                                SB.AppendLine(entry.Name);
                                SB.AppendLine(Reference.External.ClassGuid.ToString());
                            }
                            else
                            {
                                SB.AppendLine("BadRef " + Reference.External.FileGuid + "/" + Reference.External.ClassGuid);
                            }
                        }
                        else
                            SB.AppendLine("nullptr");
                    }
                    else
                    {

                        SB.AppendLine(ClassToString(Value));
                    }
                }
                else
                {
                    if (FieldType == typeof(byte)) SB.AppendLine(((byte)Value).ToString("X2"));
                    else if (FieldType == typeof(ushort)) SB.AppendLine(((ushort)Value).ToString("X4"));
                    else if (FieldType == typeof(uint))
                    {
                        uint value = (uint)Value;
                        string val = Utils.GetString((int)value);

                        if (!val.StartsWith("0x"))
                        {
                            SB.AppendLine(val);
                            SB.AppendLine(value.ToString("X8"));
                        }
                        else
                        {
                            SB.AppendLine(val);
                        }
                    }
                    else if (FieldType == typeof(int))
                    {
                        int value = (int)Value;
                        string val = Utils.GetString(value);

                        if (!val.StartsWith("0x"))
                        {
                            SB.AppendLine(val);
                            SB.AppendLine(value.ToString("X8"));
                        }
                        else
                        {
                            SB.AppendLine(val);
                        }
                    }
                    else if (FieldType == typeof(ulong)) SB.AppendLine(((ulong)Value).ToString("X16"));
                    else if (FieldType == typeof(float)) SB.AppendLine(((float)Value).ToString());
                    else if (FieldType == typeof(double)) SB.AppendLine(((double)Value).ToString());
                    else SB.AppendLine(Value.ToString());
                }
            }

            return SB.ToString();
        }

        #endregion

    }
}
