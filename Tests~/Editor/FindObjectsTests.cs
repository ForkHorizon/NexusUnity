using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class FindObjectsTests
    {
        [SetUp]
        public void SetUp()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void FindObjectsHandlesInvalidRegexAndPerformsLiteralSearch()
        {
            var go = new GameObject("TestObject (1)");
            try
            {
                JObject result = RpcResult("find_objects", new JObject { ["name"] = "TestObject (1)" });
                JArray objects = (JArray)result["objects"];
                Assert.IsNotNull(objects);
                Assert.IsTrue(objects.Any(o => o["name"]?.ToString() == "TestObject (1)"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FindObjectsMatchesValidRegexPattern()
        {
            var go = new GameObject("RegexObject123");
            try
            {
                JObject result = RpcResult("find_objects", new JObject { ["name"] = "^RegexObject\\d+$" });
                JArray objects = (JArray)result["objects"];
                Assert.IsNotNull(objects);
                Assert.IsTrue(objects.Any(o => o["name"]?.ToString() == "RegexObject123"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static JObject RpcResult(string method, JObject parameters = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters ?? new JObject(),
                ["id"] = 1
            };

            JObject response = JObject.Parse(MCPServerMethods.ProcessJsonRpc(request.ToString(Formatting.None)));
            Assert.IsNull(response["error"], response.ToString(Formatting.None));
            return (JObject)response["result"];
        }
    }
}
