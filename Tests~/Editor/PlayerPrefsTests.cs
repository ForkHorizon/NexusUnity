using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace UnityMCP.Editor.Tests
{
    public class PlayerPrefsTests
    {
        [Test]
        public void UnescapeWindowsRegistryKeyName_StripsHashSuffix_WhenValidHashPresent()
        {
            string input = "my_setting_key_h1234567890";
            string result = MCPServerMethods.UnescapeWindowsRegistryKeyName(input);
            Assert.AreEqual("my_setting_key", result);
        }

        [Test]
        public void UnescapeWindowsRegistryKeyName_PreservesKey_WhenNoHashSuffix()
        {
            string input = "user_profile_volume_level";
            string result = MCPServerMethods.UnescapeWindowsRegistryKeyName(input);
            Assert.AreEqual("user_profile_volume_level", result);
        }

        [Test]
        public void ParseMacOsDefaultsOutput_ExtractsQuotedAndUnquotedKeys()
        {
            string mockOutput = @"{
    ""player_session_count"" = 5;
    music_volume = 0.8;
    ""custom.key.name"" = ""some_value"";
    ""escaped\\\""quote"" = ""val"";
}";
            List<string> keys = MCPServerMethods.ParseMacOsDefaultsOutput(mockOutput);
            Assert.Contains("player_session_count", keys);
            Assert.Contains("music_volume", keys);
            Assert.Contains("custom.key.name", keys);
            Assert.Contains("escaped\"quote", keys);
        }

        [Test]
        public void ReadPlayerPrefValue_CorrectlyIdentifiesIntMinValue()
        {
            string testKey = "NexusTest_IntMin_" + System.Guid.NewGuid().ToString("N");
            try
            {
                PlayerPrefs.SetInt(testKey, int.MinValue);
                PlayerPrefs.Save();

                JToken val = MCPServerMethods.ReadPlayerPrefValue(testKey);
                Assert.AreEqual(int.MinValue, val.Value<int>());
            }
            finally
            {
                PlayerPrefs.DeleteKey(testKey);
            }
        }

        [Test]
        public void ReadPlayerPrefValue_CorrectlyIdentifiesString()
        {
            string testKey = "NexusTest_String_" + System.Guid.NewGuid().ToString("N");
            try
            {
                PlayerPrefs.SetString(testKey, "hello_world");
                PlayerPrefs.Save();

                JToken val = MCPServerMethods.ReadPlayerPrefValue(testKey);
                Assert.AreEqual("hello_world", val.ToString());
            }
            finally
            {
                PlayerPrefs.DeleteKey(testKey);
            }
        }

        [Test]
        public void ReadPlayerPrefValue_DoesNotConfuseIntForString()
        {
            string testKey = "NexusTest_IntForStr_" + System.Guid.NewGuid().ToString("N");
            try
            {
                PlayerPrefs.SetInt(testKey, 42);
                PlayerPrefs.Save();

                JToken val = MCPServerMethods.ReadPlayerPrefValue(testKey);
                Assert.AreEqual(JTokenType.Integer, val.Type);
                Assert.AreEqual(42, val.Value<int>());
            }
            finally
            {
                PlayerPrefs.DeleteKey(testKey);
            }
        }
    }
}
