using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class ObjectIdCompatibilityTests
    {
        [Test]
        public void LegacyInstanceIdRoundTripsThroughEntityIdHelpers()
        {
            var go = new GameObject("NexusIdCompat");
            try
            {
                int rawId = go.GetRawId();
                Assert.AreNotEqual(0, rawId);

                var entityId = MCPServerMethods.ExtractIdFromToken(JToken.FromObject(rawId));
                Assert.AreSame(go, MCPServerMethods.IdToObject(entityId));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SerializedObjectReferenceIdMatchesReferencedObject()
        {
            var probe = ScriptableObject.CreateInstance<NexusObjectReferenceProbe>();
            var target = new GameObject("NexusIdCompatTarget");
            try
            {
                probe.target = target;

                using (var serializedObject = new SerializedObject(probe))
                {
                    var prop = serializedObject.FindProperty("target");
                    Assert.IsNotNull(prop);

                    var propId = MCPServerMethods.GetObjectReferenceId(prop);
                    Assert.AreSame(target, MCPServerMethods.IdToObject(propId));
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(target);
            }
        }
    }

    public sealed class NexusObjectReferenceProbe : ScriptableObject
    {
        public GameObject target;
    }
}
