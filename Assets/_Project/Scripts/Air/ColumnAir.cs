using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A programmable column of air (Stage 26): a cylinder that can lift or sink, push out or pull in,
    /// and swirl, with its strength easing toward a target. Bosses drive these from their attack
    /// scripts (the Condenser's updraft and suction, the Mainframe's exhaust), and levels use them
    /// for things like rooftop exhaust stacks.
    ///
    /// Place it at the base of the column. It always points world-up.
    /// </summary>
    public class ColumnAir : AirSource
    {
        [Header("Shape")]
        public float radius = 15f;
        public float height = 60f;
        [Tooltip("Fraction of the radius with full strength; it fades to zero at the edge.")]
        [Range(0f, 1f)] public float coreFraction = 0.4f;

        [Header("Air (units/sec at full strength)")]
        [Tooltip("+ lifts, - pushes down.")]
        public float verticalSpeed = 8f;
        [Tooltip("+ pushes outward, - pulls toward the middle (suction).")]
        public float radialSpeed = 0f;
        public float swirlSpeed = 0f;
        [Tooltip("Warm air dries soggy paper (and shows in the heat colour).")]
        public bool heat = false;

        [Header("Strength")]
        [Tooltip("Where the strength is heading (0 = off, 1 = full).")]
        [Range(0f, 1.5f)] public float targetStrength = 1f;
        [Tooltip("How fast the strength moves toward the target (per second).")]
        public float ramp = 2f;

        [Header("Visuals")]
        [SerializeField] ParticleSystem streaks;
        [SerializeField] float streakRate = 25f;
        [Range(0f, 1f)] [SerializeField] float streakAlpha = 0.3f;

        public float Strength { get; private set; }

        public override AirKind Kind => Mathf.Abs(verticalSpeed) < 0.5f ? AirKind.Draft
                                      : verticalSpeed > 0f ? AirKind.Thermal : AirKind.Downdraft;
        public override bool OffersLiftCenter => verticalSpeed > 0.5f && Strength > 0.3f;
        public override bool IsHeat => heat && verticalSpeed > 0f;

        /// <summary>Jump straight to a strength (no ramp).</summary>
        public void SetStrengthNow(float s) { Strength = s; targetStrength = s; }

        void Update()
        {
            Strength = Mathf.MoveTowards(Strength, targetStrength, ramp * Time.deltaTime);
            SyncVisuals();
        }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (Strength <= 0.001f) return Vector3.zero;
            Vector3 local = worldPosition - transform.position;
            if (local.y < 0f || local.y > height) return Vector3.zero;
            Vector3 flat = new Vector3(local.x, 0f, local.z);
            float r = flat.magnitude;
            if (r > radius) return Vector3.zero;

            float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coreFraction, 1f, r / radius));
            float vertical = Mathf.Clamp01(local.y / 2f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, local.y / height)));
            influence = radial * vertical * Mathf.Min(1f, Strength);

            Vector3 outward = r > 0.01f ? flat / r : Vector3.zero;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            return (Vector3.up * verticalSpeed + outward * radialSpeed + tangent * swirlSpeed) * (radial * vertical * Strength);
        }

        public override Bounds GetBounds() =>
            new Bounds(transform.position + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f));

        void SyncVisuals()
        {
            if (streaks == null) return;
            var main = streaks.main;
            main.startColor = AirPalette.Of(Look, streakAlpha);
            float speed = Mathf.Max(1f, Mathf.Abs(verticalSpeed) * 0.8f);
            main.startSpeed = speed;
            // Rising air streams up from the base; sinking air pours down from the top.
            bool up = verticalSpeed >= 0f;
            streaks.transform.localPosition = new Vector3(0f, up ? 0f : height, 0f);
            streaks.transform.localRotation = Quaternion.Euler(up ? -90f : 90f, 0f, 0f);
            main.startLifetime = height * 0.8f / speed;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = radius * 0.7f;
            var emission = streaks.emission;
            emission.rateOverTime = streakRate * Mathf.Clamp01(Strength);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, Strength > 0.01f ? 1f : 0.3f);
            AirGizmos.DrawCircle(transform.position, Vector3.up, radius);
            AirGizmos.DrawCircle(transform.position + Vector3.up * height, Vector3.up, radius);
        }
    }
}
