using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Simple test window with input and buttons for UI interaction testing.
    /// </summary>
    public class MCPTestWindow : EditorWindow
    {
        public const string WindowTitle = "Nexus Unity Test";

        /// <summary>
        /// Stores the last input value for verification.
        /// </summary>
        public static string LastInputValue = "";

        /// <summary>
        /// Tracks if the test button has been clicked.
        /// </summary>
        public static bool ButtonClicked = false;

        /// <summary>
        /// Resets the window state for testing.
        /// </summary>
        public void ResetState()
        {
            LastInputValue = "";
            ButtonClicked = false;
            var textField = rootVisualElement.Q<TextField>("TestInput");
            if (textField != null) textField.value = "";
            var label = rootVisualElement.Q<Label>("TestLabel");
            if (label != null) label.text = "Initial State";
        }

        /// <summary>
        /// Shows the MCP Test window.
        /// </summary>
        [MenuItem("Window/Nexus Unity/Test Window")]
        public static MCPTestWindow ShowWindow()
        {
            MCPTestWindow wnd = GetWindow<MCPTestWindow>();
            wnd.titleContent = new GUIContent(WindowTitle);
            return wnd;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(360, 220);
        }

        /// <summary>
        /// Creates the UI for the test window using UI Toolkit.
        /// </summary>
        public void CreateGUI()
        {
            NexusEditorUi.SetupRoot(rootVisualElement);
            rootVisualElement.name = "NexusTestWindowRoot";

            LastInputValue = "";
            ButtonClicked = false;

            var header = NexusEditorUi.Panel("TestWindowHeader");
            header.Add(NexusEditorUi.Label("UI Automation Test", 16, true));
            header.Add(NexusEditorUi.Label("Named controls used by Nexus Unity UI automation checks.", 11, false, NexusEditorUi.Muted));
            rootVisualElement.Add(header);

            var panel = NexusEditorUi.Panel("TestWindowControls");

            var label = new Label("Initial State");
            label.name = "TestLabel";
            label.style.marginBottom = 8;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(label);

            var textField = new TextField("Input:");
            textField.name = "TestInput";
            textField.style.marginBottom = 8;
            textField.value = "";
            LastInputValue = textField.value;
            textField.RegisterValueChangedCallback(evt => LastInputValue = evt.newValue);
            panel.Add(textField);

            var button = new Button();
            button.name = "TestButton";
            button.text = "Click Me";
            button.style.height = 30;
            button.clicked += () =>
            {
                ButtonClicked = true;
                label.text = "Button Clicked!";
            };
            panel.Add(button);
            rootVisualElement.Add(panel);
        }
    }
}
