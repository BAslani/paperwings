using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Player control preferences that apply to every plane/fold (unlike FlightSettings,
    /// which describes one plane's handling). Open via PaperWings > Settings > Controls.
    /// </summary>
    [CreateAssetMenu(fileName = "Controls", menuName = "PaperWings/Control Settings")]
    public class ControlSettings : ScriptableObject
    {
        [Tooltip("OFF = flight-style (W / stick forward dives). ON = W / stick forward climbs.")]
        public bool invertPitch = false;
    }
}
