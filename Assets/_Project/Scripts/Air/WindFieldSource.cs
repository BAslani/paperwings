using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Outdoor wind (Stage 25, the rooftop): a big box of moving air with gusts. Unlike indoor air it
    /// is everywhere at once, so the whole level leans one way: fly into it to slow down and climb
    /// steeply, run with it to cover ground fast. Gusts come and go on a slow noise curve, and the
    /// direction wanders a little, so you keep correcting.
    ///
    /// The box is centred on the object (axis-aligned with it). Wind fades in near the box's edges.
    /// </summary>
    public class WindFieldSource : AirSource
    {
        [Tooltip("Size of the windy region.")]
        public Vector3 size = new Vector3(300f, 150f, 300f);
        [Tooltip("Steady wind (world space, units/sec).")]
        public Vector3 wind = new Vector3(-6f, 0f, 0f);
        [Tooltip("Extra wind on top in a gust (fraction of the steady wind).")]
        [Range(0f, 2f)] public float gustAmount = 0.7f;
        [Tooltip("How often gusts come (per second). Low = long, rolling gusts.")]
        public float gustFrequency = 0.12f;
        [Tooltip("How far the direction wanders, in degrees.")]
        [Range(0f, 60f)] public float wanderAngle = 15f;
        [Tooltip("Units over which the wind fades in from the box edge.")]
        public float edgeFade = 20f;

        [Header("Visuals")]
        [SerializeField] ParticleSystem streaks;
        [SerializeField] float streakRate = 60f;
        [Range(0f, 1f)] [SerializeField] float streakAlpha = 0.25f;

        public override AirKind Kind => AirKind.Draft;

        /// <summary>0..1 gust strength right now (for HUD / sound).</summary>
        public float Gust01 { get; private set; }
        Vector3 currentWind;

        void Update()
        {
            float t = Time.time * gustFrequency;
            Gust01 = Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(t, 0.37f));
            float wander = (Mathf.PerlinNoise(0.71f, t * 0.6f) * 2f - 1f) * wanderAngle;
            currentWind = Quaternion.AngleAxis(wander, Vector3.up) * wind * (1f + gustAmount * Gust01);
            SyncVisuals();
        }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            Vector3 half = size * 0.5f;
            float dx = half.x - Mathf.Abs(local.x), dy = half.y - Mathf.Abs(local.y), dz = half.z - Mathf.Abs(local.z);
            if (dx < 0f || dy < 0f || dz < 0f) return Vector3.zero;
            float fade = Mathf.Clamp01(Mathf.Min(dx, Mathf.Min(dy, dz)) / Mathf.Max(0.1f, edgeFade));
            influence = fade * Mathf.Clamp01(currentWind.magnitude / 10f);
            return currentWind * fade;
        }

        public override Bounds GetBounds() => new Bounds(transform.position, size);

        void SyncVisuals()
        {
            if (streaks == null) return;
            var main = streaks.main;
            main.startColor = AirPalette.Of(Look, streakAlpha * (0.5f + 0.5f * Gust01));
            main.startSpeed = 0f;
            main.startLifetime = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = streaks.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(currentWind.x);
            vel.y = new ParticleSystem.MinMaxCurve(currentWind.y);
            vel.z = new ParticleSystem.MinMaxCurve(currentWind.z);
            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size * 0.9f;
            var emission = streaks.emission;
            emission.rateOverTime = streakRate * (0.4f + 0.6f * Gust01);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = old;
            AirGizmos.DrawArrow(transform.position, wind * 2f);
        }
    }
}
