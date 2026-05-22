using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public partial class MCPServerWindow : EditorWindow
    {
        private string _cliStatusMessage = "Checking link...";
        private int _selectedTab = 0;
        private VisualElement _content;
        private Button[] _tabButtons;
        private Button _startButton;
        private Button _stopButton;
        private Label _statusPill;
        private Label _portLabel;
        private Label _stateLabel;
        private Label _sessionLabel;
        private Label _editorStateLabel;
        private Label _cliStatusLabel;
        private Label _errorLabel;
        private Label _footerLabel;
        private IVisualElementScheduledItem _refreshItem;

        [MenuItem("Window/Nexus Unity/Server Control Panel")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPServerWindow>();
            window.titleContent = new GUIContent("Nexus Unity");
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Nexus Unity");
            minSize = new Vector2(560, 420);
            CheckCliLinkStatus();
        }

        public void CreateGUI()
        {
            NexusEditorUi.SetupRoot(rootVisualElement);
            rootVisualElement.name = "NexusServerWindowRoot";
            rootVisualElement.Add(BuildHeader());
            rootVisualElement.Add(BuildTabs());

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "NexusContentScroll" };
            scroll.style.flexGrow = 1;
            _content = new VisualElement { name = "NexusContent" };
            _content.style.flexGrow = 1;
            scroll.Add(_content);
            rootVisualElement.Add(scroll);

            _footerLabel = NexusEditorUi.Label($"v{MCPServer.Version}", 10, false, NexusEditorUi.Muted, "NexusFooterVersion");
            _footerLabel.style.alignSelf = Align.FlexEnd;
            rootVisualElement.Add(_footerLabel);

            DrawSelectedTab();
            UpdateDynamicState();
            _refreshItem?.Pause();
            _refreshItem = rootVisualElement.schedule.Execute(UpdateDynamicState).Every(1000);
        }

        private void OnDisable()
        {
            _refreshItem?.Pause();
            _refreshItem = null;
        }

        private VisualElement BuildHeader()
        {
            var header = NexusEditorUi.Panel("NexusHeader");
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var titleBlock = new VisualElement();
            titleBlock.style.flexGrow = 1;
            titleBlock.style.minWidth = 220;
            titleBlock.Add(NexusEditorUi.Label("Server Control Panel", 18, true));
            titleBlock.Add(NexusEditorUi.Label($"Nexus Unity editor automation server v{MCPServer.Version}", 11, false, NexusEditorUi.Muted));
            header.Add(titleBlock);

            var statusBlock = NexusEditorUi.Row(true, "NexusHeaderStatus");
            statusBlock.style.justifyContent = Justify.FlexEnd;
            statusBlock.style.flexGrow = 1;
            statusBlock.style.minWidth = 260;

            _statusPill = NexusEditorUi.Pill("STOPPED", Color.gray, "NexusStatusPill");
            _statusPill.style.marginRight = 6;
            _statusPill.style.marginBottom = 6;
            statusBlock.Add(_statusPill);

            _portLabel = NexusEditorUi.Label("Port: 8081", 12, true, null, "NexusPortLabel");
            _portLabel.style.marginRight = 8;
            _portLabel.style.marginBottom = 6;
            statusBlock.Add(_portLabel);

            statusBlock.Add(NexusEditorUi.Button("Copy URL", CopyServerUrl, "Copy server URL to clipboard", false, "NexusCopyUrlButton"));
            header.Add(statusBlock);
            return header;
        }

        private VisualElement BuildTabs()
        {
            var tabs = NexusEditorUi.Row(false, "NexusTabs");
            tabs.style.marginBottom = 8;
            _tabButtons = new[]
            {
                CreateTabButton("Server", 0, "NexusTabServer"),
                CreateTabButton("Tools", 1, "NexusTabTools"),
                CreateTabButton("Verification", 2, "NexusTabVerification")
            };

            foreach (var tab in _tabButtons)
            {
                tab.style.flexGrow = 1;
                tab.style.marginBottom = 0;
                tabs.Add(tab);
            }

            UpdateTabStyles();
            return tabs;
        }

        private Button CreateTabButton(string text, int index, string name)
        {
            return NexusEditorUi.Button(text, () =>
            {
                _selectedTab = index;
                UpdateTabStyles();
                DrawSelectedTab();
            }, $"{text} tab", false, name);
        }

        private void UpdateTabStyles()
        {
            if (_tabButtons == null) return;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                bool selected = i == _selectedTab;
                _tabButtons[i].style.backgroundColor = selected ? NexusEditorUi.Primary : new Color(0.30f, 0.30f, 0.30f);
                _tabButtons[i].style.color = selected ? Color.white : new Color(0.86f, 0.86f, 0.86f);
                _tabButtons[i].style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void DrawSelectedTab()
        {
            if (_content == null) return;
            _content.Clear();
            switch (_selectedTab)
            {
                case 0: DrawServerTab(); break;
                case 1: DrawToolsTab(); break;
                case 2: DrawVerificationTab(); break;
            }
            UpdateDynamicState();
        }

    }
}
