using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saltmarsh.Audio
{
    [Serializable]
    public class AmbienceLayer
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        public bool loop = true;
    }

    [CreateAssetMenu(menuName = "Saltmarsh/Ambience Profile")]
    public class AmbienceProfile : ScriptableObject
    {
        [SerializeReference] public List<AmbienceLayer> layers = new List<AmbienceLayer>();
        public float masterVolume = 0.8f;
    }
}
