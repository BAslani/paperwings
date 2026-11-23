using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A radiator under a window: a long curtain of warm air rising up the wall (Stage 24).
    ///
    /// Thermals are columns you circle in; a radiator is a strip you fly ALONG. Hug the wall, keep
    /// your wings level, and you climb steadily the whole length of it, like a glider soaring a ridge.
    /// Turn away and you drop out of it.
    ///
    /// Place the object at the middle of the radiator, at floor level, with its forward (blue) axis
    /// pointing away from the wall into the room. Its local X runs along the wall.
    /// </summary>
    public class RadiatorSource : AirSource
    {
        [SerializeField] RadiatorSettings settings;
        [Tooltip("Length of this radiator along the wall.")]
        [SerializeField] float length = 50f;
        [SerializeField] ParticleSystem shimmer;

        public override AirKind Kind => AirKind.Thermal;
        public override bool OffersLiftCenter => false;   // a strip along a wall: you fly along it, not round it
        public float Length { get => length; set => length = Mathf.Max(1f, value); }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null) return Vector3.zero;

            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            if (local.y < 0f || local.y > settings.height) return Vector3.zero;
            if (local.z < 0f || local.z > settings.depth) return Vector3.zero;
            float along01 = Mathf.Abs(local.x) / (length * 0.5f);
            if (along01 > 1f) return Vector3.zero;

            float ends = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settings.endSoftness, 1f, along01));
            float reach = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, local.z / settings.depth));
            float bottom = Mathf.Clamp01(local.y / 2f);
            float top = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settings.topFade, 1f, local.y / settings.height));

            influence = ends * reach * bottom * top;
            float lift = settings.liftSpeed * influence;
            return Vector3.up * lift + transform.forward * (lift * settings.outwardLean);
        }

        public override Bounds GetBounds()
        {
            float d = settings != null ? settings.depth : 10f, h = settings != null ? settings.height : 50f;
            var b = new Bounds(transform.position, Vector3.zero);
            Vector3 x = transform.right * length * 0.5f, z = transform.forward * d, y = Vector3.up * h;
            foreach (var c in new[] { x, -x, x + z, -x + z })
            {
                b.Encapsulate(transform.position + c);
                b.Encapsulate(transform.position + c + y);
            }
            return b;
        }

        void Start() => SyncVisuals();
        void Update() => SyncVisuals();

        /// <summary>Shimmer rises along the whole length, in the palette's heat colour.</summary>
        public void SyncVisuals()
        {
            if (shimmer == null || settings == null) return;
            var main = shimmer.main;
            main.startColor = AirPalette.Of(Look, settings.shimmerColor);
            main.startSpeed = settings.liftSpeed * 0.7f;
            main.startLifetime = settings.height * 0.75f / Mathf.Max(1f, settings.liftSpeed * 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = shimmer.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(length * 0.9f, settings.depth * 0.5f, 0.5f);

            var emission = shimmer.emission;
            emission.rateOverTime = settings.shimmerPer10Units * length / 10f;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Gizmos.color = GizmoColor;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, settings.height * 0.5f, settings.depth * 0.5f),
                                new Vector3(length, settings.height, settings.depth));
            Gizmos.matrix = old;
            AirGizmos.DrawArrow(transform.position + Vector3.up * 3f, Vector3.up * 6f);
        }
    }
}
