using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A programmable straight beam of wind (Stage 25/26): a box of air blowing along the object's
    /// forward axis, whose strength eases toward a target and can run on a timed on/off cycle.
    /// Used for door gusts in the stairwell, the server room's cooling units, and the Mainframe's
    /// sweeping exhaust fans (the boss rotates the object to sweep it across the room).
    /// </summary>
    public class BeamAir : AirSource
    {
        [Header("Shape (local: X wide, Y tall, Z along the wind)")]
        public Vector2 size = new Vector2(12f, 12f);
        public float range = 60f;
        [Range(0.01f, 1f)] public float edgeSoftness = 0.35f;
        [Range(0f, 2f)] public float distanceFalloff = 0.6f;

        [Header("Air")]
        public float windSpeed = 12f;
        [Tooltip("Warm air (exhaust) dries paper and shows in the heat colour.")]
        public bool heat = false;

        [Header("Strength")]
        [Range(0f, 1.5f)] public float targetStrength = 1f;
        public float ramp = 3f;

        [Header("Cycle (0 = no cycle: the strength just follows Target Strength)")]
        [Tooltip("Seconds of gust.")]
        public float onTime = 0f;
        [Tooltip("Seconds of calm between gusts.")]
        public float offTime = 3f;
        public float cycleOffset = 0f;

        [Header("Visuals")]
        [SerializeField] ParticleSystem streaks;
        [SerializeField] float streakRate = 30f;
        [Range(0f, 1f)] [SerializeField] float streakAlpha = 0.35f;

        public float Strength { get; private set; }
        /// <summary>True while a cycled gust is blowing (for warnings).</summary>
        public bool Gusting => Strength > 0.2f;

        float cycleTime;

        public override AirKind Kind => transform.forward.y < -0.5f ? AirKind.Downdraft
                                      : transform.forward.y > 0.5f ? AirKind.Thermal : AirKind.Draft;
        public override bool IsHeat => heat;
        // Only a roughly square upward beam is something to circle in (not a long floor strip).
        public override bool OffersLiftCenter => Kind == AirKind.Thermal && Strength > 0.3f
            && Mathf.Min(size.x, size.y) >= 14f && Mathf.Max(size.x, size.y) < 4f * Mathf.Min(size.x, size.y);
        public override AirLook Look => heat && Kind != AirKind.Downdraft ? AirLook.Heat : base.Look;

        void Awake() => cycleTime = cycleOffset;

        void Update()
        {
            float target = targetStrength;
            if (onTime > 0f)
            {
                cycleTime += Time.deltaTime;
                target = Mathf.Repeat(cycleTime, onTime + offTime) < onTime ? targetStrength : 0f;
            }
            Strength = Mathf.MoveTowards(Strength, target, ramp * Time.deltaTime);
            SyncVisuals();
        }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (Strength <= 0.001f) return Vector3.zero;
            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            if (local.z < 0f || local.z > range) return Vector3.zero;
            float edge = Mathf.Max(Mathf.Abs(local.x) / (size.x * 0.5f), Mathf.Abs(local.y) / (size.y * 0.5f));
            if (edge > 1f) return Vector3.zero;

            float edgeFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - edgeSoftness, 1f, edge));
            float distanceFactor = Mathf.Pow(1f - local.z / range, distanceFalloff);
            influence = edgeFactor * distanceFactor * Mathf.Min(1f, Strength);
            return transform.forward * (windSpeed * edgeFactor * distanceFactor * Strength);
        }

        public override Bounds GetBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            Vector3 right = transform.right * size.x * 0.5f, up = transform.up * size.y * 0.5f, end = transform.forward * range;
            foreach (var c in new[] { right + up, right - up, -right + up, -right - up })
            {
                b.Encapsulate(transform.position + c);
                b.Encapsulate(transform.position + c + end);
            }
            return b;
        }

        void SyncVisuals()
        {
            if (streaks == null) return;
            var main = streaks.main;
            main.startColor = AirPalette.Of(Look, streakAlpha);
            main.startSpeed = Mathf.Max(1f, windSpeed);
            main.startLifetime = range * 0.7f / Mathf.Max(1f, windSpeed);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x * 0.85f, size.y * 0.85f, 0.1f);
            var emission = streaks.emission;
            emission.rateOverTime = streakRate * Mathf.Clamp01(Strength);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, Strength > 0.01f ? 1f : 0.3f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, range * 0.5f), new Vector3(size.x, size.y, range));
            Gizmos.matrix = old;
        }
    }
}
