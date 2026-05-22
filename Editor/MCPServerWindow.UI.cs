using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class NexusEditorUi
    {
        private static readonly Color RootBackground = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.82f, 0.82f, 0.82f);
        private static readonly Color PanelBackground = EditorGUIUtility.isProSkin ? new Color(0.23f, 0.23f, 0.23f) : new Color(0.94f, 0.94f, 0.94f);
        private static readonly Color BorderColor = EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f) : new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color TextColor = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color MutedTextColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.36f, 0.36f, 0.36f);
        private static readonly Color PrimaryColor = new Color(0.24f, 0.42f, 0.62f);

        internal static Color Primary => PrimaryColor;
        internal static Color Muted => MutedTextColor;

        internal static void SetupRoot(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = RootBackground;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 8;
        }

        internal static Label Label(string text, int size = 12, bool bold = false, Color? color = null, string name = null)
        {
            var label = new Label(text);
            if (!string.IsNullOrEmpty(name)) label.name = name;
            label.style.fontSize = size;
            label.style.color = color ?? TextColor;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }

        internal static VisualElement Panel(string name = null)
        {
            var panel = new VisualElement();
            if (!string.IsNullOrEmpty(name)) panel.name = name;
            panel.style.backgroundColor = PanelBackground;
            panel.style.borderBottomColor = BorderColor;
            panel.style.borderLeftColor = BorderColor;
            panel.style.borderRightColor = BorderColor;
            panel.style.borderTopColor = BorderColor;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingTop = 8;
            panel.style.marginBottom = 8;
            return panel;
        }

        internal static VisualElement Section(string title, string subtitle = null, string name = null)
        {
            var section = new VisualElement();
            if (!string.IsNullOrEmpty(name)) section.name = name;
            section.style.marginBottom = 8;
            section.Add(Label(title, 13, true));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Label(subtitle, 11, false, MutedTextColor);
                sub.style.marginTop = 1;
                sub.style.marginBottom = 5;
                section.Add(sub);
            }
            return section;
        }

        internal static VisualElement Row(bool wrap = false, string name = null)
        {
            var row = new VisualElement();
            if (!string.IsNullOrEmpty(name)) row.name = name;
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = wrap ? Wrap.Wrap : Wrap.NoWrap;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            return row;
        }

        internal static Button Button(string text, Action clicked, string tooltip = null, bool primary = false, string name = null)
        {
            var button = new Button(clicked) { text = text };
            if (!string.IsNullOrEmpty(name)) button.name = name;
            if (!string.IsNullOrEmpty(tooltip)) button.tooltip = tooltip;
            button.style.height = 30;
            button.style.minWidth = 124;
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.unityFontStyleAndWeight = primary ? FontStyle.Bold : FontStyle.Normal;
            if (primary)
            {
                button.style.backgroundColor = PrimaryColor;
                button.style.color = Color.white;
            }
            return button;
        }

        internal static Label Pill(string text, Color color, string name = null)
        {
            var pill = Label(text, 11, true, Color.white, name);
            pill.style.backgroundColor = color;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.paddingBottom = 3;
            pill.style.paddingLeft = 9;
            pill.style.paddingRight = 9;
            pill.style.paddingTop = 3;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            return pill;
        }

        internal static void SetPill(Label pill, string text, Color color)
        {
            if (pill == null) return;
            pill.text = text;
            pill.style.backgroundColor = color;
        }

    }
}
