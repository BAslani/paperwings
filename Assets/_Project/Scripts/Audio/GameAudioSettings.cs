using UnityEngine;

namespace PaperWings.Audio
{
    /// <summary>Volume mix. PaperWings > Settings > Audio. Changes apply live in Play mode.</summary>
    [CreateAssetMenu(fileName = "Audio", menuName = "PaperWings/Audio Settings")]
    public class GameAudioSettings : ScriptableObject
    {
        public bool mute = false;
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Tooltip("Hits, weapons, boss, rewards.")]
        [Range(0f, 1f)] public float effectsVolume = 1f;
        [Tooltip("Wind, flutter, thermal hum, draft rush.")]
        [Range(0f, 1f)] public float ambienceVolume = 0.7f;
        [Tooltip("Menu clicks.")]
        [Range(0f, 1f)] public float uiVolume = 0.7f;
    }
}
