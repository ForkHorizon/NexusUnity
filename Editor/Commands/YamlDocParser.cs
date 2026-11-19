// Indentation-tracking parser for the Unity YAML subset, plus the scalar value it records.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Commands
{
    /// <summary>One scalar value read from a YAML line.</summary>
    internal readonly struct YamlValue
    {
        public readonly string Text;
        public readonly int Line;
        /// <summary>Creates a value read from 1-based <paramref name="line"/>.</summary>
        public YamlValue(string text, int line) { Text = text; Line = line; }
    }

    /// <summary>Indentation-tracking line parser for Unity's YAML subset. Returns an error string instead of throwing.</summary>
    internal static class YamlDocParser
    {
        private sealed class Frame { public int Indent; public string Key; }

        /// <summary>Fills <see cref="YamlAsset.Docs"/> from its lines. Returns an error description, or null when the file parsed.</summary>
        public static string Parse(YamlAsset asset)
        {
            YamlDoc doc = null;
            var stack = new List<Frame>();
            var items = new List<KeyValuePair<int, Dictionary<string, YamlValue>>>();
            char openQuote = '\0';
            for (int i = 0; i < asset.Lines.Length; i++)
            {
                string raw = asset.Lines[i];
                int lineNo = i + 1;
                if (openQuote != '\0') { if (ClosesQuote(raw, openQuote)) openQuote = '\0'; continue; }
                if (raw.Length == 0 || raw.StartsWith("%", StringComparison.Ordinal)) continue;
                Match header = AssetYamlIndex.HeaderPattern.Match(raw);
                if (header.Success)
                {
                    doc = new YamlDoc
                    {
                        ClassId = int.Parse(header.Groups[1].Value),
                        FileId = long.Parse(header.Groups[2].Value),
                        Stripped = header.Groups[3].Success,
                        Line = lineNo
                    };
                    asset.Docs[doc.FileId] = doc;
                    stack.Clear();
                    items.Clear();
                    continue;
                }
                if (raw.StartsWith("---", StringComparison.Ordinal)) return "line " + lineNo + ": unsupported document header";
                if (doc == null) return "line " + lineNo + ": content before the first document";

                int indent = raw.Length - raw.TrimStart(' ').Length;
                string body = raw.Substring(indent);
                bool listItem = body.StartsWith("- ", StringComparison.Ordinal) || body == "-";
                if (listItem) { body = body.Length > 2 ? body.Substring(2) : string.Empty; indent += 2; }
                int colon = KeyColon(body);
                string value = colon < 0 ? body : body.Length > colon + 1 ? body.Substring(colon + 1).Trim() : string.Empty;
                // Unity wraps long flow mappings onto deeper-indented lines; join them before checking balance.
                int depth = FlowDepth(value);
                while (depth > 0 && i + 1 < asset.Lines.Length && Indent(asset.Lines[i + 1]) > indent - (listItem ? 2 : 0))
                {
                    value += " " + asset.Lines[++i].Trim();
                    depth = FlowDepth(value);
                }
                if (depth != 0) return "line " + lineNo + ": unbalanced flow collection";
                if (colon < 0) continue; // plain-scalar continuation or bare list value
                string key = body.Substring(0, colon);
                if (value.Length > 0 && (value[0] == '\'' || value[0] == '"') && !ClosesQuote(value.Substring(1), value[0])) openQuote = value[0];

                while (stack.Count > 0 && stack[stack.Count - 1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
                while (items.Count > 0 && (items[items.Count - 1].Key > indent || (listItem && items[items.Count - 1].Key == indent)))
                    items.RemoveAt(items.Count - 1);
                if (doc.ClassName == null && indent == 0) { doc.ClassName = key; continue; }
                string parent = string.Join(".", stack.Select(f => f.Key));
                if (listItem)
                {
                    var item = new Dictionary<string, YamlValue>(StringComparer.Ordinal);
                    items.Add(new KeyValuePair<int, Dictionary<string, YamlValue>>(indent, item));
                    if (!doc.Lists.TryGetValue(parent, out var list)) doc.Lists[parent] = list = new List<Dictionary<string, YamlValue>>();
                    list.Add(item);
                }
                if (items.Count > 0 && items[items.Count - 1].Key == indent) items[items.Count - 1].Value[key] = new YamlValue(value, lineNo);
                else if (items.Count == 0 && indent > 0) doc.Fields[parent.Length == 0 ? key : parent + "." + key] = new YamlValue(value, lineNo);
                foreach (Match g in AssetYamlIndex.GuidPattern.Matches(value)) doc.GuidRefs.Add(new YamlValue(g.Groups[1].Value, lineNo));
                if (indent > 0) stack.Add(new Frame { Indent = indent, Key = key });
            }
            return openQuote != '\0' ? "unterminated quoted scalar" : null;
        }

        private static int KeyColon(string body)
        {
            if (body.Length == 0 || body[0] == '{' || body[0] == '[' || body[0] == '\'' || body[0] == '"') return -1;
            int colon = body.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 0 && body.EndsWith(":", StringComparison.Ordinal)) colon = body.Length - 1;
            return colon;
        }

        private static bool ClosesQuote(string text, char quote)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != quote) continue;
                if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'') { i++; continue; }
                if (quote == '"' && i > 0 && text[i - 1] == '\\') continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Open-bracket depth of a flow collection value: 0 when balanced (or not a flow value), -1 when over-closed.
        /// ponytail: structural check, not a full YAML validator; a malformed block scalar can still pass and is read leniently.
        /// </summary>
        private static int FlowDepth(string value)
        {
            if (value.Length == 0 || (value[0] != '{' && value[0] != '[')) return 0;
            int depth = 0;
            char quote = '\0';
            foreach (char c in value)
            {
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '\'' || c == '"') quote = c;
                else if (c == '{' || c == '[') depth++;
                else if ((c == '}' || c == ']') && --depth < 0) return -1;
            }
            return depth;
        }

        private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
    }
}
