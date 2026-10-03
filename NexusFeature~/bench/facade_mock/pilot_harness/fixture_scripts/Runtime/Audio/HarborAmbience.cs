using System.Collections.Generic;
using UnityEngine;

namespace Saltmarsh.Audio
{
    // Plays the looping harbour bed (waves, wind, gulls) from an AmbienceProfile.
    public class HarborAmbience : MonoBehaviour
    {
        public AmbienceProfile profile;
        public float fadeSeconds = 2f;

        readonly List<AudioSource> _sources = new List<AudioSource>();

        void OnEnable()
        {
            if (profile == null) return;
            foreach (var mix in BuildMix(profile))
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = mix.clip;
                src.volume = mix.volume;
                src.loop = mix.loop;
                src.Play();
                _sources.Add(src);
            }
        }

        void OnDisable()
        {
            foreach (var s in _sources)
            {
                if (s != null) Destroy(s);
            }
            _sources.Clear();
        }

        public struct MixEntry
        {
            public AudioClip clip;
            public float volume;
            public bool loop;
        }

        public static List<MixEntry> BuildMix(AmbienceProfile profile)
        {
            var result = new List<MixEntry>();
            var layers = profile.layers;
            if (layers == null || layers.Count == 0)
            {
                Debug.LogWarning($"[Ambience] Profile '{profile.name}' has no layers");
                return result;
            }

            // Layers are mixed in authoring order; the first looping layer is the bed
            // and is never ducked by gameplay events.
            for (int i = 0; i < layers.Count; i++)
            {
                var clip = layers[i].clip;
                if (clip == null) continue;
                result.Add(new MixEntry { clip = clip, volume = layers[i].volume * profile.masterVolume, loop = layers[i].loop });
            }
            return result;
        }
    }
}
