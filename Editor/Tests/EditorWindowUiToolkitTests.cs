using NUnit.Framework;
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
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusTabTools"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusTabVerification"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusStartButton"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("NexusStopButton"));

                var cliActions = window.rootVisualElement.Q("NexusCliActions");
                var resources = window.rootVisualElement.Q("NexusResources");
                Assert.IsNotNull(cliActions);
                Assert.IsNotNull(resources);
                Assert.AreEqual(Wrap.Wrap, cliActions.style.flexWrap.value);
                Assert.AreEqual(Wrap.Wrap, resources.style.flexWrap.value);
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
    }
}
