using UnityEngine;
using PaperWings.Air;

namespace PaperWings.Combat
{
    /// <summary>
    /// The air blast from the Stapler's slam: a short-lived cylinder of air pushing DOWN and OUT
    /// around the boss. It's a normal AirSource, so planes feel it through the same air field as
    /// fans and thermals (and the HUD shows "DOWNDRAFT"). Idle until Pulse() is called.
    /// </summary>
    public class BossSlamAir : AirSource
    {
        public float radius = 28f;
        public float height = 45f;
        public float downSpeed = 14f;
        public float outwardSpeed = 9f;

        float strength;    // 0..1, fades out over the pulse
        float duration = 1f;

        public override AirKind Kind => AirKind.Downdraft;

        /// <summary>Starts a blast that fades out over 'seconds'.</summary>
        public void Pulse(float seconds)
        {
            duration = Mathf.Max(0.05f, seconds);
            strength = 1f;
        }

        void Update() => strength = Mathf.MoveTowards(strength, 0f, Time.deltaTime / duration);

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (strength <= 0f) return Vector3.zero;

            Vector3 local = worldPosition - transform.position;
            if (local.y < 0f || local.y > height) return Vector3.zero;
            Vector3 flat = new Vector3(local.x, 0f, local.z);
            float r = flat.magnitude;
            if (r > radius) return Vector3.zero;

            influence = strength * (1f - r / radius);
            Vector3 outward = r > 0.01f ? flat / r : Vector3.zero;
            return (Vector3.down * downSpeed + outward * outwardSpeed) * influence;
        }

        public override Bounds GetBounds() =>
            new Bounds(transform.position + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f));

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.45f, 1f, strength > 0f ? 1f : 0.25f);
            AirGizmos.DrawCircle(transform.position, Vector3.up, radius);
            AirGizmos.DrawCircle(transform.position + Vector3.up * height, Vector3.up, radius);
        }
    }
}
