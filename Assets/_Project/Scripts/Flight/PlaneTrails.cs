using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Wingtip trails that fade in with speed. They make dives feel fast and leave a
    /// readable spiral when you circle in a thermal. Visual only.
    /// </summary>
    public class PlaneTrails : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] TrailRenderer[] trails;

        void LateUpdate()
        {
            if (plane == null || trails == null) return;
            FlightSettings s = plane.Settings;

            bool flying = plane.State == PlaneState.Flying;
            float t = flying && s.trailsEnabled
                ? Mathf.InverseLerp(s.trailMinSpeed, s.trailFullSpeed, plane.Speed)
                : 0f;
            // Thermals get a faint trail even when slow, so the climbing spiral is visible.
            if (flying && s.trailsEnabled && plane.Air.InThermal) t = Mathf.Max(t, 0.35f * plane.Air.thermal);

            foreach (var trail in trails)
            {
                if (trail == null) continue;
                trail.emitting = t > 0.01f;
                trail.widthMultiplier = s.trailWidth * t;
                trail.time = s.trailTime;
            }
        }

        /// <summary>Call on reset/teleport so trails don't streak across the level.</summary>
        public void ClearTrails()
        {
            if (trails == null) return;
            foreach (var trail in trails) if (trail != null) trail.Clear();
        }
    }
}
