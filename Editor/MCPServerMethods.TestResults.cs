using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken GetTestResults(JToken p)
        {
            string resultPath = ResolveTestResultsPath(p);
            if (string.IsNullOrEmpty(resultPath) || !File.Exists(resultPath))
                return CreateMissingTestResults(resultPath);

            try
            {
                XDocument document = XDocument.Load(resultPath);
                XElement root = document.Root;
                if (root == null)
                    return new JObject { ["status"] = "Error", ["result_path"] = resultPath, ["message"] = "TestResults XML is empty." };
                return BuildTestResultsResponse(document, root, resultPath);
            }
            catch (Exception e)
            {
                return new JObject
                {
                    ["status"] = "Error",
                    ["result_path"] = resultPath,
                    ["message"] = $"Failed to parse TestResults XML: {e.Message}"
                };
            }
        }

        private static JObject CreateMissingTestResults(string resultPath)
        {
            return new JObject
            {
                ["status"] = "NotFound",
                ["result_path"] = resultPath ?? GetDefaultTestResultsPath(),
                ["message"] = "No Unity TestResults XML file was found."
            };
        }

        private static JObject BuildTestResultsResponse(XDocument document, XElement root, string resultPath)
        {
            return new JObject
            {
                ["status"] = "Success",
                ["result_path"] = resultPath,
                ["timestamp_utc"] = File.GetLastWriteTimeUtc(resultPath).ToString("o"),
                ["result"] = Attr(root, "result"),
                ["total"] = IntAttr(root, "total"),
                ["passed"] = IntAttr(root, "passed"),
                ["failed"] = IntAttr(root, "failed"),
                ["inconclusive"] = IntAttr(root, "inconclusive"),
                ["skipped"] = IntAttr(root, "skipped"),
                ["duration"] = DoubleAttr(root, "duration"),
                ["failed_tests"] = CollectFailedTests(document)
            };
        }

        private static JArray CollectFailedTests(XDocument document)
        {
            var failedTests = new JArray();
            foreach (XElement testCase in document.Descendants("test-case"))
            {
                string result = Attr(testCase, "result");
                if (!IsSuccessfulTestResult(result)) failedTests.Add(CreateFailedTest(testCase, result));
            }
            return failedTests;
        }

        private static bool IsSuccessfulTestResult(string result)
        {
            return string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result, "Skipped", StringComparison.OrdinalIgnoreCase);
        }

        private static JObject CreateFailedTest(XElement testCase, string result)
        {
            string message = testCase.Element("failure")?.Element("message")?.Value
                ?? testCase.Element("reason")?.Element("message")?.Value
                ?? testCase.Element("output")?.Value
                ?? result;
            return new JObject
            {
                ["name"] = Attr(testCase, "name"),
                ["fullname"] = Attr(testCase, "fullname"),
                ["result"] = result,
                ["message"] = message
            };
        }

        private static string ResolveTestResultsPath(JToken p)
        {
            string requested = p?["result_path"]?.ToString();
            if (!string.IsNullOrEmpty(requested))
                return ValidateTestResultsPath(requested);

            string latest = FindLatestTestResultsPath();
            return !string.IsNullOrEmpty(latest) ? latest : GetDefaultTestResultsPath();
        }

        private static string FindLatestTestResultsPath()
        {
            var candidates = new List<string>();
            AddTestResultCandidates(candidates, Path.GetDirectoryName(Application.dataPath));
            AddTestResultCandidates(candidates, Application.persistentDataPath);

            return candidates
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void AddTestResultCandidates(List<string> candidates, string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            try
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, "TestResults*.xml", SearchOption.TopDirectoryOnly));
            }
            catch
            {
                // Some platform-specific persistent directories can be temporarily unavailable.
            }
        }

        private static string ValidateTestResultsPath(string path)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path));

            if (IsPathWithin(fullPath, projectRoot) || IsPathWithin(fullPath, Application.persistentDataPath))
                return fullPath;

            throw new Exception("Access denied: Test result path must be inside the Unity project or Unity persistent data path.");
        }

        private static bool IsPathWithin(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path);
            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDefaultTestResultsPath()
        {
            return Path.Combine(Application.persistentDataPath, "TestResults.xml");
        }

        private static string Attr(XElement element, string name)
        {
            return element.Attribute(name)?.Value;
        }

        private static int IntAttr(XElement element, string name)
        {
            return int.TryParse(Attr(element, name), out int value) ? value : 0;
        }

        private static double DoubleAttr(XElement element, string name)
        {
            return double.TryParse(Attr(element, name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : 0d;
        }
    }
}
