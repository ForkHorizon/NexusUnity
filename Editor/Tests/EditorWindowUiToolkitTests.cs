using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    public class EditorWindowUiToolkitTests
    {
        [Test]
        public void ServerWindowBuildsResponsiveUiToolkitSurface()
        {
            var window = ScriptableObject.CreateInstance<MCPServerWindow>();
            try
            {
                window.CreateGUI();

                Assert.IsNotNull(window.rootVisualElement.Q("NexusServerWindowRoot"));
                Assert.IsNotNull(window.rootVisualElement.Q("NexusStatusPill"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusTabServer"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusTabIntegrations"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusTabResources"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusStartButton"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusStopButton"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusRestartButton"));
                Assert.AreEqual(320, MCPServerWindow.UsableMinSize.x);
                Assert.GreaterOrEqual(MCPServerWindow.UsableMinSize.y, 420);

                var headerStatus = window.rootVisualElement.Q("NexusHeaderStatus");
                var serverActions = window.rootVisualElement.Q("NexusServerActions");
                var bridgeActions = window.rootVisualElement.Q("NexusBridgeActions");
                Assert.IsNotNull(headerStatus);
                Assert.IsNotNull(serverActions);
                Assert.IsNotNull(bridgeActions);
                Assert.AreEqual(Wrap.Wrap, headerStatus.style.flexWrap.value);
                Assert.AreEqual(Wrap.Wrap, serverActions.style.flexWrap.value);
                Assert.AreEqual(Wrap.Wrap, bridgeActions.style.flexWrap.value);
                Assert.AreEqual(1, window.rootVisualElement.Q<Button>("NexusRestartButton").style.flexGrow.value);
                Assert.AreEqual(1, window.rootVisualElement.Q<Button>("NexusOpenBridgeFolderButton").style.flexGrow.value);
                Assert.IsNull(window.rootVisualElement.Q<Button>("NexusOpenTestWindowButton"));

                typeof(MCPServerWindow)
                    .GetField("_selectedTab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(window, 1);
                typeof(MCPServerWindow)
                    .GetMethod("DrawSelectedTab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(window, null);
                Assert.IsNotNull(window.rootVisualElement.Q("NexusIntegrationCards"));
                Assert.IsNotNull(window.rootVisualElement.Q("NexusIntegrationCardCodex"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusIntegrationAutoCodex"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusIntegrationCopyGenericJson"));
                Assert.AreEqual(Wrap.Wrap, window.rootVisualElement.Q("NexusIntegrationCards").style.flexWrap.value);

                typeof(MCPServerWindow)
                    .GetField("_selectedTab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(window, 2);
                typeof(MCPServerWindow)
                    .GetMethod("DrawSelectedTab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(window, null);
                Assert.IsNotNull(window.rootVisualElement.Q("NexusResources"));
                Assert.IsNotNull(window.rootVisualElement.Q<Foldout>("NexusAdvancedDiagnostics"));
                Assert.IsFalse(window.rootVisualElement.Q<Foldout>("NexusAdvancedDiagnostics").value);
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusOpenTestWindowButton"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void VerificationWindowBuildsNamedControls()
        {
            var window = ScriptableObject.CreateInstance<MCPVerificationWindow>();
            try
            {
                window.CreateGUI();

                Assert.IsNotNull(window.rootVisualElement.Q("NexusVerificationRoot"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("VerificationRunButton"));
                Assert.IsNotNull(window.rootVisualElement.Q<Label>("VerificationStatusLabel"));
                Assert.IsNotNull(window.rootVisualElement.Q<Label>("VerificationResultLabel"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TestWindowPreservesAutomationElementNames()
        {
            var window = ScriptableObject.CreateInstance<MCPTestWindow>();
            try
            {
                window.CreateGUI();
                window.ResetState();

                Assert.IsNotNull(window.rootVisualElement.Q("NexusTestWindowRoot"));
                Assert.IsNotNull(window.rootVisualElement.Q<TextField>("TestInput"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("TestButton"));
                Assert.IsNotNull(window.rootVisualElement.Q<Label>("TestLabel"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ServerWindowAppliesMinimumUsableSize()
        {
            var window = ScriptableObject.CreateInstance<MCPServerWindow>();
            try
            {
                window.position = new Rect(100, 100, 280, 300);
                typeof(MCPServerWindow)
                    .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(window, null);

                Assert.AreEqual(MCPServerWindow.UsableMinSize, window.minSize);
                Assert.GreaterOrEqual(window.position.width, MCPServerWindow.UsableMinSize.x);
                Assert.GreaterOrEqual(window.position.height, MCPServerWindow.UsableMinSize.y);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowMenuUsesSingleNexusUnityEntryPoint()
        {
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("Window/Nexus Unity"));

            var window = Resources.FindObjectsOfTypeAll<MCPServerWindow>().FirstOrDefault();
            try
            {
                Assert.IsNotNull(window);
                Assert.AreEqual("Nexus Unity", window.titleContent.text);

                var menuPaths = typeof(MCPServerWindow).Assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .SelectMany(method => method.GetCustomAttributes<MenuItem>())
                    .Select(attribute => attribute.menuItem)
                    .Where(path => path.StartsWith("Window/Nexus Unity"))
                    .ToArray();
                CollectionAssert.AreEquivalent(new[] { "Window/Nexus Unity" }, menuPaths);
            }
            finally
            {
                if (window != null) window.Close();
            }
        }
    }
}
