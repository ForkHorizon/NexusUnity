using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace UnityMCP.Editor.Tests
{
    public class AssetPathSecurityTests
    {
        [Test]
        public void ValidateAssetPath_AllowsValidAsset()
        {
            Assert.DoesNotThrow(() => MCPServerMethods.ValidateAssetPath("Assets/MyFolder/MyAsset.asset"));
            Assert.DoesNotThrow(() => MCPServerMethods.ValidateAssetPath("Packages/com.foo/bar.txt"));
        }

        [Test]
        public void ValidateAssetPath_PreventsTraversal()
        {
            var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidateAssetPath("Assets/../../secrets.txt"));
            Assert.That(ex.Message, Does.Contain("Access denied"));

            var ex2 = Assert.Throws<Exception>(() => MCPServerMethods.ValidateAssetPath("Assets/../MyFolder/file.txt"));
            Assert.That(ex2.Message, Does.Contain("Asset path must be within"));
        }
    }
}
