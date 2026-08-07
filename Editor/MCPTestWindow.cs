using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// EditorWindow used by Nexus Unity UI automation tests to exercise named UI Toolkit controls and mutable window state.
    /// </summary>
    /// <remarks>
    /// The window builds a TextField, Button, and Label with stable names. Verification helpers show the window, reset state,
    /// send UI automation JSON-RPC calls, and assert that UI callbacks update this window's test state.
    /// </remarks>
    public class MCPTestWindow : EditorWindow
    {
        public const string WindowTitle = "Nexus Unity Test";

        /// <summary>
        /// Stores the last input value for verification.
        /// </summary>
        public string LastInputValue { get; private set; } = "";

        /// <summary>
        /// Tracks if the test button has been clicked.
        /// </summary>
        public bool ButtonClicked { get; private set; }

        /// <summary>
        /// Resets this window's test state and clears the UI Toolkit input/label elements in the current visual tree.
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
        /// Creates or focuses the MCP Test editor window and returns the instance for verification code.
        /// </summary>
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
        /// Creates named UI Toolkit controls and event handlers used by Nexus Unity UI automation smoke tests.
        /// </summary>
        /// <remarks>
        /// This method mutates <see cref="EditorWindow.rootVisualElement"/>, preserves instance test state, registers a text-change
        /// callback, and wires the button click to update the label and <see cref="ButtonClicked"/>.
        /// </remarks>
        public void CreateGUI()
        {
            NexusEditorUi.SetupRoot(rootVisualElement);
            rootVisualElement.name = "NexusTestWindowRoot";

            var header = NexusEditorUi.Panel("TestWindowHeader");
            header.Add(NexusEditorUi.Label("UI Automation Test", 16, true));
            header.Add(NexusEditorUi.Label("Named controls used by Nexus Unity UI automation checks.", 11, false, NexusEditorUi.Muted));
            rootVisualElement.Add(header);

            var panel = NexusEditorUi.Panel("TestWindowControls");

            var label = new Label(ButtonClicked ? "Button Clicked!" : "Initial State");
            label.name = "TestLabel";
            label.style.marginBottom = 8;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(label);

            var textField = new TextField("Input:");
            textField.name = "TestInput";
            textField.style.marginBottom = 8;
            textField.value = LastInputValue;
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
