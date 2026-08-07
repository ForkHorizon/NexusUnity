using System;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for type resolution security, assembly/namespace filtering, and component/ScriptableObject allowlists.
    /// </summary>
    public class TypeSecurityTests
    {
        [SetUp]
        public void InitRegistry()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void FindType_AllowsValidUnityEngineTypes()
        {
            Type type = MCPServerMethods.FindType("BoxCollider");
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(BoxCollider), type);

            type = MCPServerMethods.FindType("Camera");
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(Camera), type);
        }

        [Test]
        public void FindType_BlocksDisallowedSystemAndInternalTypes()
        {
            Assert.IsNull(MCPServerMethods.FindType("System.String"));
            Assert.IsNull(MCPServerMethods.FindType("System.AppDomain"));
            Assert.IsNull(MCPServerMethods.FindType("UnityEditorInternal.InternalEditorUtility"));
            Assert.IsNull(MCPServerMethods.FindType("Mono.Cecil.TypeDefinition"));
        }

        [Test]
        public void FindComponentType_ResolvesValidConcreteComponents()
        {
            Type boxCollider = MCPServerMethods.FindComponentType("BoxCollider");
            Assert.IsNotNull(boxCollider);
            Assert.AreEqual(typeof(BoxCollider), boxCollider);

            Type camera = MCPServerMethods.FindComponentType("Camera");
            Assert.IsNotNull(camera);
            Assert.AreEqual(typeof(Camera), camera);
        }

        [Test]
        public void FindComponentType_RejectsAbstractComponentsAndNonComponents()
        {
            // Abstract Component base classes must be rejected
            Assert.IsNull(MCPServerMethods.FindComponentType("Component"));
            Assert.IsNull(MCPServerMethods.FindComponentType("MonoBehaviour"));
            Assert.IsNull(MCPServerMethods.FindComponentType("Collider"));

            // Non-components must be rejected
            Assert.IsNull(MCPServerMethods.FindComponentType("GameObject"));
            Assert.IsNull(MCPServerMethods.FindComponentType("System.String"));
        }

        [Test]
        public void FindScriptableObjectType_RejectsAbstractScriptableObjectsAndNonScriptableObjects()
        {
            // Abstract ScriptableObject base class must be rejected
            Assert.IsNull(MCPServerMethods.FindScriptableObjectType("ScriptableObject"));

            // Components or non-ScriptableObjects must be rejected
            Assert.IsNull(MCPServerMethods.FindScriptableObjectType("BoxCollider"));
            Assert.IsNull(MCPServerMethods.FindScriptableObjectType("System.String"));
        }
    }
}
