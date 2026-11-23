using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A ceiling fan (Stage 24). Two kinds of air in one source:
    ///   - under the blades, a DOWNDRAFT that pushes you toward the floor;
    ///   - in a ring just outside them, UPWASH (the pushed air curling back up) that lifts you,
    ///     plus a swirl that carries you round.
    /// Fly the ring and you climb toward the ceiling; cut across the middle and you drop.
    /// With an on/off cycle it becomes a timing puzzle: cross underneath while it's winding down.
    ///
    /// Place the object at the fan hub, under the ceiling. The air always goes world-down from it.
    /// </summary>
    public class CeilingFanSource : AirSource
    {
        [SerializeField] CeilingFanSettings settings;
        [Tooltip("Spins for show (around its local Y).")]
        [SerializeField] Transform blades;
        [Tooltip("Streaks falling under the blades (downdraft colour).")]
        [SerializeField] ParticleSystem coreStreaks;
        [Tooltip("Streaks rising around the blades (updraft colour).")]
        [SerializeField] ParticleSystem ringStreaks;
        [Tooltip("Start offset into the on/off cycle, so several fans don't stop together.")]
        [SerializeField] float cycleOffset = 0f;

        public override AirKind Kind => AirKind.Downdraft;
        public override bool IsHeat => false;
        public override AirLook Look => AirLook.Downdraft;

        /// <summary>0 = stopped, 1 = full speed.</summary>
        public float Strength01 { get; private set; } = 1f;

        float cycleTime;

        void Awake() => cycleTime = cycleOffset;

        void Update()
        {
            if (settings == null) return;

            // Run / rest cycle, with a smooth spin-up and wind-down.
            float target = 1f;
            if (settings.onTime > 0f)
            {
                cycleTime += Time.deltaTime;
                float period = settings.onTime + settings.offTime;
                target = Mathf.Repeat(cycleTime, period) < settings.onTime ? 1f : 0f;
            }
            Strength01 = Mathf.MoveTowards(Strength01, target, Time.deltaTime / Mathf.Max(0.05f, settings.spinUpTime));

            if (blades != null) blades.Rotate(0f, settings.bladeSpin * Strength01 * Time.deltaTime, 0f, Space.Self);
            SyncVisuals();
        }

        /// <summary>Core (downdraft) and ring (upwash) strength at a point, 0..1 each, before the cycle.</summary>
        void Evaluate(Vector3 worldPosition, out float core, out float ring, out float depth, out Vector3 flat)
        {
            core = ring = 0f;
            Vector3 local = worldPosition - transform.position;
            depth = -local.y;
            flat = new Vector3(local.x, 0f, local.z);
            if (settings == null || depth < 0f || depth > Mathf.Max(settings.coreReach, settings.ringReach)) return;

            float r = flat.magnitude, R = settings.bladeRadius;

            if (r < R && depth < settings.coreReach)
            {
                float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(R * 0.55f, R, r));
                core = radial * Mathf.Pow(1f - depth / settings.coreReach, 0.7f);
            }

            float t = (r - R) / Mathf.Max(0.1f, settings.ringWidth);
            if (t > 0f && t < 1f && depth < settings.ringReach)
            {
                float across = Mathf.Sin(t * Mathf.PI);
                float nearCeiling = Mathf.Clamp01(depth / 8f);   // eases off just under the ceiling, so it never pins you there
                float bottom = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, depth / settings.ringReach));
                ring = across * nearCeiling * bottom;
            }
        }

        public override AirKind KindAt(Vector3 worldPosition)
        {
            Evaluate(worldPosition, out float core, out float ring, out _, out _);
            if (ring > core) return AirKind.Thermal;
            return core > 0f ? AirKind.Downdraft : AirKind.Draft;
        }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null || Strength01 <= 0f) return Vector3.zero;
            Evaluate(worldPosition, out float core, out float ring, out _, out Vector3 flat);
            if (core <= 0f && ring <= 0f) return Vector3.zero;

            influence = Mathf.Max(core, ring) * Strength01;
            float r = flat.magnitude;
            Vector3 tangent = r > 0.01f ? Vector3.Cross(Vector3.up, flat / r) : Vector3.zero;
            Vector3 wind = Vector3.down * (settings.coreDownSpeed * core)
                         + Vector3.up * (settings.ringUpSpeed * ring)
                         + tangent * (settings.swirlSpeed * Mathf.Max(core, ring));
            return wind * Strength01;
        }

        public override Bounds GetBounds()
        {
            float R = settings != null ? settings.bladeRadius + settings.ringWidth : 20f;
            float h = settings != null ? Mathf.Max(settings.coreReach, settings.ringReach) : 60f;
            return new Bounds(transform.position + Vector3.down * h * 0.5f, new Vector3(R * 2f, h, R * 2f));
        }

        public void SyncVisuals()
        {
            if (settings == null) return;
            Sync(coreStreaks, AirLook.Downdraft, settings.coreDownSpeed, settings.coreReach, settings.bladeRadius * 0.8f, 0f);
            Sync(ringStreaks, AirLook.Updraft, settings.ringUpSpeed, settings.ringReach, settings.bladeRadius + settings.ringWidth * 0.5f, settings.ringWidth * 0.4f);
        }

        void Sync(ParticleSystem ps, AirLook look, float speed, float reach, float radius, float thickness)
        {
            if (ps == null) return;
            var main = ps.main;
            main.startColor = AirPalette.Of(look, settings.streakAlpha);
            main.startSpeed = Mathf.Max(0.5f, speed * 0.8f);
            main.startLifetime = reach * 0.7f / Mathf.Max(1f, speed * 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            // A cone with no spread fires particles straight along the emitter's forward axis
            // (the builder points the core emitter down and the ring emitter up).
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = radius;
            shape.radiusThickness = thickness > 0f ? Mathf.Clamp01(thickness / radius) : 1f;
            var emission = ps.emission;
            emission.rateOverTime = settings.streakRate * Strength01;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Gizmos.color = AirPalette.Downdraft;
            AirGizmos.DrawCircle(transform.position, Vector3.up, settings.bladeRadius);
            AirGizmos.DrawCircle(transform.position + Vector3.down * settings.coreReach, Vector3.up, settings.bladeRadius * 0.6f);
            Gizmos.color = AirPalette.Updraft;
            AirGizmos.DrawCircle(transform.position, Vector3.up, settings.bladeRadius + settings.ringWidth);
            AirGizmos.DrawCircle(transform.position + Vector3.down * settings.ringReach, Vector3.up, settings.bladeRadius + settings.ringWidth);
        }
    }
}
