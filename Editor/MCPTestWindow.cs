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
        public static MCPTestWindow ShowWindow()
        {
            MCPTestWindow wnd = GetWindow<MCPTestWindow>();
            wnd.titleContent = new GUIContent("MCPTestWindow");
            return wnd;
        }

        /// <summary>
        /// Creates the UI for the test window using UI Toolkit.
        /// </summary>
        public void CreateGUI()
        {
            // Reset state
            LastInputValue = "";
            ButtonClicked = false;

            VisualElement root = rootVisualElement;

            var label = new Label("Initial State");
            label.name = "TestLabel";
            root.Add(label);

            var textField = new TextField("Input:");
            textField.name = "TestInput";
            textField.value = ""; // Force it to empty for tests
            LastInputValue = textField.value;
            textField.RegisterValueChangedCallback(evt => LastInputValue = evt.newValue);
            root.Add(textField);

            var button = new Button();
            button.name = "TestButton";
            button.text = "Click Me";
            button.clicked += () =>
            {
                ButtonClicked = true;
                label.text = "Button Clicked!";
            };
            root.Add(button);
        }
    }
}
