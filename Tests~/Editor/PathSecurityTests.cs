using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for path security and validation to prevent path traversal.
    /// </summary>
    public class PathSecurityTests
    {
        /// <summary>
        /// Verifies that ValidatePath prevents access to sibling directories with similar prefixes.
        /// </summary>
        [Test]
        public void ValidatePath_PreventsSiblingDirectoryAccess()
        {
            // Setup paths based on current project environment
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string projectFolderName = Path.GetFileName(projectRoot);
            string parentDir = Path.GetDirectoryName(projectRoot).Replace('\\', '/');

            // Construct a path that is a sibling to the project root with a similar prefix
            // e.g. if project is "MyProject", try "MyProjectSecrets"
            string maliciousFolderName = projectFolderName + "Secrets";
            string maliciousPath = Path.Combine(parentDir, maliciousFolderName, "file.txt").Replace('\\', '/');

            // Verify that accessing this sibling path throws an exception
            var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidatePath(maliciousPath));
            Assert.That(ex.Message, Does.Contain("Access denied"));
        }

        /// <summary>
        /// Verifies that ValidatePath allows access to valid subpaths within the project.
        /// </summary>
        [Test]
        public void ValidatePath_AllowsValidAssetPath()
        {
            string validPath = Path.Combine(Application.dataPath, "TestAsset.txt").Replace('\\', '/');
            Assert.DoesNotThrow(() => MCPServerMethods.ValidatePath(validPath));
        }

        /// <summary>
        /// Verifies that ValidatePath allows the project root path itself.
        /// </summary>
        [Test]
        public void ValidatePath_AllowsProjectRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            Assert.DoesNotThrow(() => MCPServerMethods.ValidatePath(projectRoot));
        }

        /// <summary>
        /// Verifies that ValidatePath prevents access to the project's parent directory.
        /// </summary>
        [Test]
        public void ValidatePath_PreventsParentDirectoryAccess()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string parentPath = Path.GetDirectoryName(projectRoot); // One level up

            var ex = Assert.Throws<Exception>(() => MCPServerMethods.ValidatePath(parentPath));
            Assert.That(ex.Message, Does.Contain("Access denied"));
        }
    }
}
