using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// actions.json 写回用的保序 JSON 文档模型
    ///
    /// 读取仍走 JsonUtility (与运行时一致)；写回时：
    /// 1. 按 Unity 序列化规则把 DTO 转为完整节点树 (ToNode)
    /// 2. 与读入时的原始节点合并 (Merge)：原有键保持原顺序与原写法，原文没有的键只在取值非默认时追加，
    ///    原文中 DTO 不认识的键原样保留
    /// 3. 按手写风格排版 (Write)：动作对象逐键换行，较短的子对象 / 数组写在一行
    /// 这样改一个字段只会在 diff 中出现这一行
    /// </summary>
    public sealed class JNode
    {
        public enum Kind { Object, Array, String, Number, Bool, Null }

        public Kind Type;
        public readonly List<KeyValuePair<string, JNode>> Props = new List<KeyValuePair<string, JNode>>();
        public readonly List<JNode> Items = new List<JNode>();
        public string Text;
        public bool Bool;

        /// <summary>
        /// 解析得到的容器在原文中的写法 (写回时内容未变则原样输出，保留手工对齐等排版)；修改过的节点为 null
        /// </summary>
        public string Raw;

        /// <summary>
        /// 原文中是否写成多行 (null 表示新节点，按行宽自动决定)
        /// </summary>
        public bool? Multiline;

        public static JNode Obj() => new JNode { Type = Kind.Object };
        public static JNode Arr() => new JNode { Type = Kind.Array };
        public static JNode Str(string s) => new JNode { Type = Kind.String, Text = s ?? string.Empty };
        public static JNode Num(string text) => new JNode { Type = Kind.Number, Text = text };
        public static JNode Boolean(bool b) => new JNode { Type = Kind.Bool, Bool = b };
        public static JNode NullNode() => new JNode { Type = Kind.Null };

        public JNode Get(string key)
        {
            for (int i = 0; i < Props.Count; i++)
            {
                if (Props[i].Key == key) return Props[i].Value;
            }
            return null;
        }

        public void Set(string key, JNode value)
        {
            Raw = null;
            for (int i = 0; i < Props.Count; i++)
            {
                if (Props[i].Key != key) continue;
                Props[i] = new KeyValuePair<string, JNode>(key, value);
                return;
            }
            Props.Add(new KeyValuePair<string, JNode>(key, value));
        }

        /// <summary>
        /// 浅拷贝为可修改的新节点 (丢弃原文写法，保留多行提示)
        /// </summary>
        public JNode CloneShallow()
        {
            var copy = new JNode { Type = Type, Text = Text, Bool = Bool, Multiline = Multiline };
            copy.Props.AddRange(Props);
            copy.Items.AddRange(Items);
            return copy;
        }
    }

    public static class ActionJson
    {
        /// <summary>
        /// 单行写法的最大行宽 (含缩进与键名)，超出则展开为多行
        /// </summary>
        private const int MaxInlineWidth = 160;

        /// <summary>
        /// 此深度及以内的非空容器总是多行 (0 根对象, 1 data 数组, 2 动作对象)；
        /// 动作的直接数组字段 (深度 3) 也总是多行，每个元素单独一行
        /// </summary>
        private const int AlwaysMultilineDepth = 2;

        #region 解析

        public static JNode Parse(string text)
        {
            int pos = 0;
            JNode node = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length) throw Error(text, pos, "JSON 末尾有多余内容");
            return node;
        }

        private static JNode ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            int start = pos;
            JNode node = ParseValueCore(s, ref pos);
            if (node.Type == JNode.Kind.Object || node.Type == JNode.Kind.Array)
            {
                node.Raw = s.Substring(start, pos - start);
                node.Multiline = node.Raw.IndexOf('\n') >= 0;
            }
            return node;
        }

        private static JNode ParseValueCore(string s, ref int pos)
        {
            if (pos >= s.Length) throw Error(s, pos, "JSON 意外结束");

            char c = s[pos];
            switch (c)
            {
                case '{':
                {
                    pos++;
                    JNode obj = JNode.Obj();
                    SkipWhitespace(s, ref pos);
                    if (pos < s.Length && s[pos] == '}') { pos++; return obj; }
                    while (true)
                    {
                        SkipWhitespace(s, ref pos);
                        if (pos >= s.Length || s[pos] != '"') throw Error(s, pos, "缺少键名");
                        string key = ParseString(s, ref pos);
                        SkipWhitespace(s, ref pos);
                        if (pos >= s.Length || s[pos] != ':') throw Error(s, pos, "缺少 ':'");
                        pos++;
                        obj.Props.Add(new KeyValuePair<string, JNode>(key, ParseValue(s, ref pos)));
                        SkipWhitespace(s, ref pos);
                        if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                        if (pos < s.Length && s[pos] == '}') { pos++; return obj; }
                        throw Error(s, pos, "缺少 ',' 或 '}'");
                    }
                }
                case '[':
                {
                    pos++;
                    JNode arr = JNode.Arr();
                    SkipWhitespace(s, ref pos);
                    if (pos < s.Length && s[pos] == ']') { pos++; return arr; }
                    while (true)
                    {
                        arr.Items.Add(ParseValue(s, ref pos));
                        SkipWhitespace(s, ref pos);
                        if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                        if (pos < s.Length && s[pos] == ']') { pos++; return arr; }
                        throw Error(s, pos, "缺少 ',' 或 ']'");
                    }
                }
                case '"':
                    return JNode.Str(ParseString(s, ref pos));
                case 't':
                    Expect(s, ref pos, "true");
                    return JNode.Boolean(true);
                case 'f':
                    Expect(s, ref pos, "false");
                    return JNode.Boolean(false);
                case 'n':
                    Expect(s, ref pos, "null");
                    return JNode.NullNode();
                default:
                {
                    int start = pos;
                    while (pos < s.Length && "+-0123456789.eE".IndexOf(s[pos]) >= 0) pos++;
                    if (pos == start) throw Error(s, pos, $"无法识别的字符 '{c}'");
                    return JNode.Num(s.Substring(start, pos - start));
                }
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            pos++; // 开头的引号
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (pos >= s.Length) break;
                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > s.Length) throw Error(s, pos, "\\u 转义不完整");
                        sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        pos += 4;
                        break;
                    default: throw Error(s, pos, $"无法识别的转义 \\{e}");
                }
            }
            throw Error(s, pos, "字符串缺少结束引号");
        }

        private static void Expect(string s, ref int pos, string word)
        {
            if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0) throw Error(s, pos, $"应为 {word}");
            pos += word.Length;
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }

        private static FormatException Error(string s, int pos, string message)
        {
            int line = 1;
            for (int i = 0; i < pos && i < s.Length; i++)
            {
                if (s[i] == '\n') line++;
            }
            return new FormatException($"{message} (第 {line} 行)");
        }

        #endregion

        #region DTO -> 节点 (Unity 序列化规则)

        /// <summary>
        /// 按 JsonUtility 的序列化规则把对象转为节点：public 或 [SerializeField] 的实例字段，
        /// null 字符串写为 ""，null 数组写为 []，null 的可序列化类按默认实例展开，枚举写为整数
        /// </summary>
        public static JNode ToNode(object value, Type type)
        {
            if (type == typeof(string)) return JNode.Str((string)value);
            if (type == typeof(bool)) return JNode.Boolean(value != null && (bool)value);
            if (type.IsEnum) return JNode.Num(Convert.ToInt64(value ?? 0, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            if (type == typeof(float)) return JNode.Num(((float)(value ?? 0f)).ToString("R", CultureInfo.InvariantCulture));
            if (type == typeof(double)) return JNode.Num(((double)(value ?? 0d)).ToString("R", CultureInfo.InvariantCulture));
            if (type.IsPrimitive) return JNode.Num(Convert.ToString(value ?? 0, CultureInfo.InvariantCulture));

            if (type == typeof(Vector2Int))
            {
                var v = (Vector2Int)(value ?? default(Vector2Int));
                JNode o = JNode.Obj();
                o.Set("x", JNode.Num(v.x.ToString(CultureInfo.InvariantCulture)));
                o.Set("y", JNode.Num(v.y.ToString(CultureInfo.InvariantCulture)));
                return o;
            }

            Type elementType = ElementTypeOf(type);
            if (elementType != null)
            {
                JNode arr = JNode.Arr();
                if (value is IList list)
                {
                    foreach (object item in list) arr.Items.Add(ToNode(item, elementType));
                }
                return arr;
            }

            if (value == null) value = Activator.CreateInstance(type);
            JNode obj = JNode.Obj();
            foreach (FieldInfo field in SerializedFields(type))
            {
                obj.Set(field.Name, ToNode(field.GetValue(value), field.FieldType));
            }
            return obj;
        }

        private static Type ElementTypeOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return null;
        }

        private static readonly Dictionary<Type, List<FieldInfo>> FieldCache = new Dictionary<Type, List<FieldInfo>>();

        /// <summary>
        /// 可序列化字段，基类在前、声明顺序
        /// </summary>
        private static List<FieldInfo> SerializedFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out List<FieldInfo> cached)) return cached;

            var chain = new List<Type>();
            for (Type t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType) chain.Insert(0, t);

            var fields = new List<FieldInfo>();
            foreach (Type t in chain)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsInitOnly || f.IsNotSerialized) continue;
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    fields.Add(f);
                }
            }
            FieldCache[type] = fields;
            return fields;
        }

        #endregion

        #region 合并

        /// <summary>
        /// 以原始节点为底合并新值：保留原键顺序、原数字写法 ("1.0" 不会变成 "1") 与 DTO 不认识的键；
        /// 原文没有的键只在新值非默认时追加；数组按下标逐个合并，新增元素按稀疏写法输出。
        /// 内容未变的子树返回原节点本身 (写回时按原文输出)
        /// </summary>
        public static JNode Merge(JNode original, JNode updated)
        {
            if (original == null) return Sparse(updated);

            if (original.Type == JNode.Kind.Object && updated.Type == JNode.Kind.Object)
            {
                JNode result = JNode.Obj();
                bool changed = false;
                foreach (KeyValuePair<string, JNode> p in original.Props)
                {
                    JNode u = updated.Get(p.Key);
                    JNode merged = u != null ? Merge(p.Value, u) : p.Value;
                    changed |= !ReferenceEquals(merged, p.Value);
                    result.Props.Add(new KeyValuePair<string, JNode>(p.Key, merged));
                }
                foreach (KeyValuePair<string, JNode> p in updated.Props)
                {
                    if (original.Get(p.Key) != null || IsDefault(p.Value)) continue;
                    result.Props.Add(new KeyValuePair<string, JNode>(p.Key, Sparse(p.Value)));
                    changed = true;
                }
                if (!changed) return original;
                result.Multiline = original.Multiline;
                return result;
            }

            if (original.Type == JNode.Kind.Array && updated.Type == JNode.Kind.Array)
            {
                JNode result = JNode.Arr();
                bool changed = updated.Items.Count != original.Items.Count;
                for (int i = 0; i < updated.Items.Count; i++)
                {
                    JNode merged = i < original.Items.Count ? Merge(original.Items[i], updated.Items[i]) : Sparse(updated.Items[i]);
                    changed |= i >= original.Items.Count || !ReferenceEquals(merged, original.Items[i]);
                    result.Items.Add(merged);
                }
                if (!changed) return original;
                result.Multiline = original.Multiline;
                return result;
            }

            if (original.Type == updated.Type && original.Text == updated.Text && original.Bool == updated.Bool &&
                original.Type != JNode.Kind.Object && original.Type != JNode.Kind.Array) return original;

            if (original.Type == JNode.Kind.Number && updated.Type == JNode.Kind.Number && SameNumber(original.Text, updated.Text)) return original;

            // 原文把数值写成字符串且值未变：保留原写法
            if (original.Type == JNode.Kind.String && updated.Type == JNode.Kind.Number && SameNumber(original.Text, updated.Text)) return original;

            return updated;
        }

        /// <summary>
        /// 稀疏写法：只保留非默认值；纯整数的小对象 (帧区间、偏移) 只要有一个非零就整体保留，保持 { "x": 0, "y": 5 } 的可读写法
        /// </summary>
        public static JNode Sparse(JNode node)
        {
            switch (node.Type)
            {
                case JNode.Kind.Object:
                {
                    JNode result = JNode.Obj();
                    if (IsIntegerPair(node))
                    {
                        foreach (KeyValuePair<string, JNode> p in node.Props) result.Props.Add(p);
                        return result;
                    }
                    foreach (KeyValuePair<string, JNode> p in node.Props)
                    {
                        if (IsDefault(p.Value)) continue;
                        result.Props.Add(new KeyValuePair<string, JNode>(p.Key, Sparse(p.Value)));
                    }
                    return result;
                }
                case JNode.Kind.Array:
                {
                    JNode result = JNode.Arr();
                    foreach (JNode item in node.Items) result.Items.Add(Sparse(item));
                    return result;
                }
                default:
                    return node;
            }
        }

        private static bool IsIntegerPair(JNode node)
        {
            if (node.Props.Count == 0 || node.Props.Count > 2) return false;
            foreach (KeyValuePair<string, JNode> p in node.Props)
            {
                if (p.Value.Type != JNode.Kind.Number || p.Value.Text.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) return false;
            }
            return true;
        }

        public static bool IsDefault(JNode node)
        {
            switch (node.Type)
            {
                case JNode.Kind.String: return string.IsNullOrEmpty(node.Text);
                case JNode.Kind.Number: return SameNumber(node.Text, "0");
                case JNode.Kind.Bool: return !node.Bool;
                case JNode.Kind.Null: return true;
                case JNode.Kind.Array: return node.Items.Count == 0;
                default:
                    foreach (KeyValuePair<string, JNode> p in node.Props)
                    {
                        if (!IsDefault(p.Value)) return false;
                    }
                    return true;
            }
        }

        private static bool SameNumber(string a, string b)
        {
            if (a == b) return true;
            return decimal.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal da) &&
                   decimal.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal db) &&
                   da == db;
        }

        #endregion

        #region 排版

        public static string Write(JNode root)
        {
            var sb = new StringBuilder();
            WriteNode(sb, root, 0, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <param name="column">当前行已占用的宽度 (缩进 + 键名)</param>
        private static void WriteNode(StringBuilder sb, JNode node, int depth, int column)
        {
            bool container = node.Type == JNode.Kind.Object || node.Type == JNode.Kind.Array;
            if (!container)
            {
                WriteInline(sb, node);
                return;
            }

            if (node.Raw != null)
            {
                sb.Append(node.Raw);
                return;
            }

            bool empty = node.Type == JNode.Kind.Object ? node.Props.Count == 0 : node.Items.Count == 0;
            bool forceMultiline = node.Multiline ?? (depth <= AlwaysMultilineDepth || (depth == AlwaysMultilineDepth + 1 && node.Type == JNode.Kind.Array));
            if (empty || !forceMultiline)
            {
                var inline = new StringBuilder();
                WriteInline(inline, node);
                // 原文写成单行的节点只要不超宽就保持单行；新节点按行宽决定
                if (empty || column + inline.Length <= MaxInlineWidth)
                {
                    sb.Append(inline);
                    return;
                }
            }

            string indent = new string(' ', (depth + 1) * 2);
            if (node.Type == JNode.Kind.Object)
            {
                sb.Append("{\n");
                for (int i = 0; i < node.Props.Count; i++)
                {
                    sb.Append(indent);
                    int start = sb.Length;
                    WriteString(sb, node.Props[i].Key);
                    sb.Append(": ");
                    WriteNode(sb, node.Props[i].Value, depth + 1, indent.Length + (sb.Length - start));
                    sb.Append(i < node.Props.Count - 1 ? ",\n" : "\n");
                }
                sb.Append(' ', depth * 2).Append('}');
            }
            else
            {
                sb.Append("[\n");
                for (int i = 0; i < node.Items.Count; i++)
                {
                    sb.Append(indent);
                    WriteNode(sb, node.Items[i], depth + 1, indent.Length);
                    sb.Append(i < node.Items.Count - 1 ? ",\n" : "\n");
                }
                sb.Append(' ', depth * 2).Append(']');
            }
        }

        private static void WriteInline(StringBuilder sb, JNode node)
        {
            switch (node.Type)
            {
                case JNode.Kind.Object:
                    if (node.Props.Count == 0) { sb.Append("{}"); return; }
                    sb.Append("{ ");
                    for (int i = 0; i < node.Props.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        WriteString(sb, node.Props[i].Key);
                        sb.Append(": ");
                        WriteInline(sb, node.Props[i].Value);
                    }
                    sb.Append(" }");
                    return;
                case JNode.Kind.Array:
                    sb.Append('[');
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        WriteInline(sb, node.Items[i]);
                    }
                    sb.Append(']');
                    return;
                case JNode.Kind.String: WriteString(sb, node.Text); return;
                case JNode.Kind.Number: sb.Append(node.Text); return;
                case JNode.Kind.Bool: sb.Append(node.Bool ? "true" : "false"); return;
                default: sb.Append("null"); return;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>
        /// 单行紧凑写法 (用于保存原始节点快照)
        /// </summary>
        public static string WriteCompact(JNode node)
        {
            var sb = new StringBuilder();
            WriteInline(sb, node);
            return sb.ToString();
        }

        #endregion
    }
}
