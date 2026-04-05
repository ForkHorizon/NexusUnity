using System;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterTimelineMethods()
        {
            _methods["get_editor_timeline"] = GetEditorTimeline;
        }

        private static JToken GetEditorTimeline(JToken p)
        {
            var timeline = MCPServer.GetTimeline();
            JArray result = new JArray();
            foreach (var ev in timeline)
            {
                result.Add(new JObject
                {
                    ["timestamp"] = ev.timestamp,
                    ["type"] = ev.type,
                    ["details"] = ev.details
                });
            }
            return new JObject { ["status"] = "Success", ["events"] = result };
        }
    }
}