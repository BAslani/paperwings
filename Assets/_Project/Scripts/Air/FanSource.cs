using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A desk fan: blows a cone of wind out of its head's forward (blue) axis,
    /// optionally swinging left and right.
    ///
    /// The cone is "truncated": it starts at the fan's blade radius and widens with distance,
    /// so the zone right in front of the fan isn't a tiny point.
    /// </summary>
    public class FanSource : AirSource
    {
        [SerializeField] FanSettings settings;
        [Tooltip("The part that oscillates. Wind blows along its forward axis.")]
        [SerializeField] Transform head;
        [Tooltip("Spins for show.")]
        [SerializeField] Transform blades;
        [SerializeField] ParticleSystem streaks;

        public FanSettings Settings => settings;
        public override AirKind Kind => AirKind.Draft;

        Quaternion headRestRotation;
        float oscillationTime;

        Transform Head => head != null ? head : transform;
        Vector3 Origin => Head.position;
        Vector3 Direction => Head.forward;

        void Awake()
        {
            headRestRotation = Head.localRotation;
            // Random start phase so multiple fans don't swing in sync.
            oscillationTime = Random.value * 10f;
        }

        void Start() => SyncVisuals();

        void Update()
        {
            if (settings == null) return;

            if (settings.oscillate && head != null)
            {
                oscillationTime += Time.deltaTime;
                float angle = Mathf.Sin(oscillationTime * settings.oscillateSpeed * Mathf.PI * 2f) * settings.oscillateAngle;
                head.localRotation = headRestRotation * Quaternion.Euler(0f, angle, 0f);
            }

            if (blades != null)
                blades.Rotate(0f, 0f, settings.bladeSpinSpeed * Time.deltaTime, Space.Self);

            // Cheap enough to do every frame, and it means tuning the asset in Play mode
            // updates the particles live.
            SyncVisuals();
        }

        float ConeRadiusAt(float distance)
            => settings.fanRadius + distance * Mathf.Tan(settings.coneHalfAngle * Mathf.Deg2Rad);

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null) return Vector3.zero;

            Vector3 dir = Direction;
            Vector3 toPoint = worldPosition - Origin;

            // How far along the fan's axis, and how far off-axis?
            float along = Vector3.Dot(toPoint, dir);
            if (along < 0f || along > settings.range) return Vector3.zero;

            float coneRadius = ConeRadiusAt(along);
            float offAxis = (toPoint - dir * along).magnitude;
            if (offAxis > coneRadius) return Vector3.zero;

            // Weaker with distance...
            float distanceFactor = Mathf.Pow(1f - along / settings.range, settings.distanceFalloff);
            // ...and softly faded near the cone's edge.
            float edge01 = Mathf.InverseLerp(1f - settings.edgeSoftness, 1f, offAxis / coneRadius);
            float edgeFactor = 1f - Mathf.SmoothStep(0f, 1f, edge01);

            influence = distanceFactor * edgeFactor;
            Vector3 windDir = (dir + Vector3.up * settings.upwardBias).normalized;
            return windDir * (settings.windSpeed * influence);
        }

        public override Bounds GetBounds()
        {
            if (settings == null) return new Bounds(Origin, Vector3.one);
            // Box around the cone, using the full oscillation sweep is overkill; current direction is fine.
            Vector3 end = Origin + Direction * settings.range;
            float r = ConeRadiusAt(settings.range);
            var b = new Bounds(Origin, Vector3.one * settings.fanRadius * 2f);
            b.Encapsulate(new Bounds(end, Vector3.one * r * 2f));
            return b;
        }

        /// <summary>Pushes the settings into the particle system so streaks match the real wind zone.</summary>
        public void SyncVisuals()
        {
            if (streaks == null || settings == null) return;

            var main = streaks.main;
            main.startSpeed = settings.windSpeed;
            main.startLifetime = settings.range / Mathf.Max(1f, settings.windSpeed);
            main.startColor = settings.streakColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // streaks swing with the head, matching the wind

            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = settings.coneHalfAngle * 0.8f;   // keep streaks inside the zone, not on its fuzzy edge
            shape.radius = settings.fanRadius * 0.8f;

            var emission = streaks.emission;
            emission.rateOverTime = settings.streakRate;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Gizmos.color = GizmoColor;
            Vector3 o = Origin, d = Direction;

            // Cone outline: rings along the axis, plus four edge lines.
            for (int i = 0; i <= 4; i++)
            {
                float along = settings.range * i / 4f;
                AirGizmos.DrawCircle(o + d * along, d, ConeRadiusAt(along));
            }
            Quaternion rot = Quaternion.LookRotation(d);
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = rot * (Quaternion.Euler(0f, 0f, i * 90f) * Vector3.right);
                Gizmos.DrawLine(o + side * settings.fanRadius,
                                o + d * settings.range + side * ConeRadiusAt(settings.range));
            }
            AirGizmos.DrawArrow(o, d * 4f);
        }
    }
}
