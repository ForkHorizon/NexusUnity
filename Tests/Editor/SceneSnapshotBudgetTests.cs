// EditMode tests for the dump_scene_graph node budget (max_nodes / truncated).
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class SceneSnapshotBudgetTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("nexus-budget-root");
            for (int i = 0; i < 5; i++) new GameObject("child-" + i).transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void MaxNodesTruncatesAndReportsIt()
        {
            var result = (JObject)MCPServerMethods.DumpSceneGraph(new JObject
            {
                ["root_id"] = _root.GetRawId(), ["compact"] = true, ["max_nodes"] = 3
            });
            Assert.AreEqual(3, result["node_count"].Value<int>());
            Assert.IsTrue(result["truncated"].Value<bool>());
            Assert.AreEqual(2, ((JArray)result["hierarchy"][0]["children"]).Count);
            Assert.IsNotNull(result["hint"]);
        }

        [Test]
        public void ZeroMaxNodesMeansNoLimit()
        {
            var result = (JObject)MCPServerMethods.DumpSceneGraph(new JObject
            {
                ["root_id"] = _root.GetRawId(), ["compact"] = true, ["max_nodes"] = 0
            });
            Assert.AreEqual(6, result["node_count"].Value<int>());
            Assert.IsFalse(result["truncated"].Value<bool>());
            Assert.IsNull(result["hint"]);
        }

        [Test]
        public void DefaultBudgetFitsSmallTrees()
        {
            var result = (JObject)MCPServerMethods.DumpSceneGraph(new JObject { ["root_id"] = _root.GetRawId() });
            Assert.AreEqual(6, result["node_count"].Value<int>());
            Assert.IsFalse(result["truncated"].Value<bool>());
        }
    }
}
