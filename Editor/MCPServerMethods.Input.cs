using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterInputMethods()
        {
            _methods["simulate_mouse"] = SimulateMouse;
            _methods["simulate_touch"] = SimulateTouch;
            _methods["click_object_in_game"] = ClickObjectInGame;
        }

        private static Vector2 GetScreenPosition(JToken p)
        {
            float x = p["x"]?.Value<float>() ?? 0;
            float y = p["y"]?.Value<float>() ?? 0;
            bool isNormalized = p["normalized"]?.Value<bool>() ?? false;

            if (isNormalized)
            {
                Vector2 size = Handles.GetMainGameViewSize();
                x *= size.x;
                y *= size.y;
            }

            return new Vector2(x, y);
        }

        private static JToken SimulateMouse(JToken p)
        {
            var mouse = Mouse.current;
            if (mouse == null) throw new Exception("No active mouse device found.");
            
            string action = p["action"]?.ToString() ?? "click"; 
            Vector2 pos = GetScreenPosition(p);
            int buttonIdx = p["button"]?.Value<int>() ?? 0;

            // Focus GameView to ensure InputSystem processes it for the game
            var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView != null) gameView.Focus();
            }

            MouseButton button = MouseButton.Left;
            if (buttonIdx == 1) button = MouseButton.Right;
            else if (buttonIdx == 2) button = MouseButton.Middle;

            var state = new MouseState();
            state.position = pos;

            switch (action.ToLower())
            {
                case "move":
                    InputSystem.QueueStateEvent(mouse, state);
                    break;
                case "press":
                    InputSystem.QueueStateEvent(mouse, state.WithButton(button, true));
                    break;
                case "release":
                    InputSystem.QueueStateEvent(mouse, state.WithButton(button, false));
                    break;
                case "click":
                    InputSystem.QueueStateEvent(mouse, state.WithButton(button, true));
                    if (EditorApplication.isPlaying) InputSystem.Update();
                    
                    System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => {
                        MCPServer.Enqueue(() => {
                            InputSystem.QueueStateEvent(mouse, state.WithButton(button, false));
                            if (EditorApplication.isPlaying) InputSystem.Update();
                        });
                    });
                    break;
            }

            if (EditorApplication.isPlaying) InputSystem.Update();

            return new JObject { ["status"] = "Success", ["position"] = new JObject { ["x"] = pos.x, ["y"] = pos.y } };
        }

        private static JToken SimulateTouch(JToken p)
        {
            var touch = Touchscreen.current;
            if (touch == null) 
            {
                touch = InputSystem.AddDevice<Touchscreen>();
            }

            string action = p["action"]?.ToString() ?? "press";
            Vector2 pos = GetScreenPosition(p);
            int touchId = p["id"]?.Value<int>() ?? 1;

            switch (action.ToLower())
            {
                case "press":
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = touchId, phase = UnityEngine.InputSystem.TouchPhase.Began, position = pos });
                    break;
                case "move":
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = touchId, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = pos });
                    break;
                case "release":
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = touchId, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = pos });
                    break;
            }

            if (EditorApplication.isPlaying) InputSystem.Update();

            return new JObject { ["status"] = "Success" };
        }

        private static JToken ClickObjectInGame(JToken p)
        {
            if (!EditorApplication.isPlaying) throw new Exception("ClickObjectInGame requires Play Mode.");

            string path = p["path"]?.ToString();
            GameObject target = GameObject.Find(path);
            if (target == null) throw new Exception($"Object not found: {path}");

            Camera cam = Camera.main;
            if (cam == null) throw new Exception("No Main Camera found.");

            Vector3 screenPos = cam.WorldToScreenPoint(target.transform.position);
            if (screenPos.z < 0) throw new Exception("Target is behind the camera.");

            var mouse = Mouse.current;
            if (mouse == null) throw new Exception("No mouse found.");

            var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView != null) gameView.Focus();
            }

            Vector2 finalPos = new Vector2(screenPos.x, screenPos.y);
            var state = new MouseState { position = finalPos };
            
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, true));
            if (EditorApplication.isPlaying) InputSystem.Update();
            
            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => {
                MCPServer.Enqueue(() => {
                    InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, false));
                    if (EditorApplication.isPlaying) InputSystem.Update();
                });
            });

            if (EditorApplication.isPlaying) InputSystem.Update();

            return new JObject 
            { 
                ["status"] = "Success", 
                ["screen_position"] = new JObject { ["x"] = finalPos.x, ["y"] = finalPos.y },
                ["object_name"] = target.name
            };
        }
    }
}