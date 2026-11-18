using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A thermal: a vertical column of rising air above a heat source (laptop, coffee, radiator).
    /// Place the object at the base of the column. The column always points world-up,
    /// whatever the object's rotation.
    ///
    /// Lift is full strength in the core, fades softly toward the edge, and fades out near
    /// the top so you level off gently instead of being launched into the ceiling.
    /// A small inward pull helps you stay inside while circling.
    /// </summary>
    public class ThermalSource : AirSource
    {
        [SerializeField] ThermalSettings settings;
        [SerializeField] ParticleSystem shimmer;

        public ThermalSettings Settings => settings;
        public override AirKind Kind => AirKind.Thermal;

        void Start() => SyncVisuals();
        void Update() => SyncVisuals();   // live-updates particles while tuning

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null) return Vector3.zero;

            Vector3 local = worldPosition - transform.position;
            float h = local.y;
            if (h < 0f || h > settings.height) return Vector3.zero;

            Vector3 flat = new Vector3(local.x, 0f, local.z);
            float r = flat.magnitude;
            if (r > settings.radius) return Vector3.zero;

            // Radial: 1 inside the core, smoothly down to 0 at the edge.
            float r01 = r / settings.radius;
            float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.coreFraction, 1f, r01));

            // Vertical: quick fade-in just above the source, gentle fade-out near the top.
            float bottom = Mathf.Clamp01(h / 1.5f);
            float top = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settings.topFade, 1f, h / settings.height));
            float vertical = bottom * top;

            influence = radial * vertical;

            Vector3 lift = Vector3.up * (settings.liftSpeed * influence);
            Vector3 centering = r > 0.01f
                ? -flat / r * (settings.centeringAssist * r01 * vertical)
                : Vector3.zero;
            return lift + centering;
        }

        public override Bounds GetBounds()
        {
            float r = settings != null ? settings.radius : 1f;
            float h = settings != null ? settings.height : 1f;
            return new Bounds(transform.position + Vector3.up * h * 0.5f, new Vector3(r * 2f, h, r * 2f));
        }

        public void SyncVisuals()
        {
            if (shimmer == null || settings == null) return;

            // Particles drift up a bit slower than the real lift, so they read as "warm air" not "geyser".
            float riseSpeed = Mathf.Max(1f, settings.liftSpeed * 0.6f);

            var main = shimmer.main;
            main.startSpeed = riseSpeed;
            main.startLifetime = settings.height / riseSpeed;
            main.startColor = settings.shimmerColor;
            main.startSize = new ParticleSystem.MinMaxCurve(settings.shimmerSize.x, settings.shimmerSize.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Visual spread is its own knob, so you can tighten the look without shrinking the lift zone.
            var shape = shimmer.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = settings.radius * settings.shimmerSpread;

            var emission = shimmer.emission;
            emission.rateOverTime = settings.shimmerRate;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Vector3 p = transform.position;
            Color c = GizmoColor;

            // Outer column.
            Gizmos.color = c;
            for (int i = 0; i <= 4; i++)
                AirGizmos.DrawCircle(p + Vector3.up * settings.height * i / 4f, Vector3.up, settings.radius);
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * settings.radius;
                Gizmos.DrawLine(p + side, p + side + Vector3.up * settings.height);
            }

            // Full-strength core (fainter).
            Gizmos.color = new Color(c.r, c.g, c.b, 0.4f);
            float core = settings.radius * settings.coreFraction;
            AirGizmos.DrawCircle(p, Vector3.up, core);
            AirGizmos.DrawCircle(p + Vector3.up * settings.height * (1f - settings.topFade), Vector3.up, core);
            AirGizmos.DrawArrow(p, Vector3.up * 5f);
        }
    }
}
