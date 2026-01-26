using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public class MCPTestWindow : EditorWindow
    {
        public static string LastInputValue = "";
        public static bool ButtonClicked = false;

        [MenuItem("Tools/MCP Test Window")]
        public static void ShowWindow()
        {
            MCPTestWindow wnd = GetWindow<MCPTestWindow>();
            wnd.titleContent = new GUIContent("MCPTestWindow");
        }

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
