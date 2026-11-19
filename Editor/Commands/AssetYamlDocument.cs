// Parsed Unity YAML document and asset models used by the asset impact index.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Commands
{
    /// <summary>One <c>--- !u!class &amp;fileID</c> document of a Unity YAML file.</summary>
    internal sealed class YamlDoc
    {
        public int ClassId;
        public long FileId;
        public bool Stripped;
        public int Line;
        public string ClassName;
        /// <summary>Scalar values keyed by dotted key path, outside list items (<c>maxHealth</c>, <c>m_Script</c>).</summary>
        public readonly Dictionary<string, YamlValue> Fields = new Dictionary<string, YamlValue>(StringComparer.Ordinal);
        /// <summary>List items keyed by the list's dotted path. Each item keeps only its first-level keys.</summary>
        public readonly Dictionary<string, List<Dictionary<string, YamlValue>>> Lists =
            new Dictionary<string, List<Dictionary<string, YamlValue>>>(StringComparer.Ordinal);
        /// <summary>Every <c>guid:</c> reference in the document with its 1-based line.</summary>
        public readonly List<YamlValue> GuidRefs = new List<YamlValue>();

        /// <summary>Scalar at a dotted key path, or null.</summary>
        public string Field(string key) => Fields.TryGetValue(key, out YamlValue v) ? v.Text : null;
        /// <summary>Items of the list at a dotted key path, or an empty list.</summary>
        public List<Dictionary<string, YamlValue>> List(string key) =>
            Lists.TryGetValue(key, out var items) ? items : new List<Dictionary<string, YamlValue>>();
    }

    /// <summary>A scanned asset. <c>Status</c> is <c>ok</c>, <c>skipped</c> (binary) or <c>unknown</c> (unparsable).</summary>
    internal sealed class YamlAsset
    {
        public string Path;
        public string Guid;
        public string Status = "ok";
        public string Reason;
        public string[] Lines = Array.Empty<string>();
        public readonly Dictionary<long, YamlDoc> Docs = new Dictionary<long, YamlDoc>();
    }
}
