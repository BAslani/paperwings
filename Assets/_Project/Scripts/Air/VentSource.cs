using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// An AC vent: blows a straight, rectangular column of air along its forward (blue) axis.
    /// Rotate it to point down for a ceiling downdraft, or sideways for a wall draft.
    /// Its AirKind follows its direction, so the HUD labels it correctly either way.
    /// </summary>
    public class VentSource : AirSource
    {
        [SerializeField] VentSettings settings;
        [SerializeField] ParticleSystem streaks;

        public override AirKind Kind => transform.forward.y < -0.5f ? AirKind.Downdraft : AirKind.Draft;

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null) return Vector3.zero;

            // Into the vent's local space (ignoring scale): x/y across the opening, z along the airflow.
            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            if (local.z < 0f || local.z > settings.range) return Vector3.zero;

            float halfW = settings.size.x * 0.5f, halfH = settings.size.y * 0.5f;
            float edge = Mathf.Max(Mathf.Abs(local.x) / halfW, Mathf.Abs(local.y) / halfH);   // 0 center, 1 edge
            if (edge > 1f) return Vector3.zero;

            float edgeFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settings.edgeSoftness, 1f, edge));
            float distanceFactor = Mathf.Pow(1f - local.z / settings.range, settings.distanceFalloff);
            influence = edgeFactor * distanceFactor;
            return transform.forward * (settings.windSpeed * influence);
        }

        public override Bounds GetBounds()
        {
            if (settings == null) return new Bounds(transform.position, Vector3.one);
            var b = new Bounds(transform.position, Vector3.zero);
            Vector3 right = transform.right * settings.size.x * 0.5f, up = transform.up * settings.size.y * 0.5f;
            Vector3 end = transform.forward * settings.range;
            foreach (var c in new[] { right + up, right - up, -right + up, -right - up })
            {
                b.Encapsulate(transform.position + c);
                b.Encapsulate(transform.position + c + end);
            }
            return b;
        }

        void Start() => SyncVisuals();
        void Update() => SyncVisuals();

        public void SyncVisuals()
        {
            if (streaks == null || settings == null) return;
            var main = streaks.main;
            main.startSpeed = settings.windSpeed;
            // Streaks die out partway, where the air has weakened.
            main.startLifetime = settings.range * 0.7f / Mathf.Max(1f, settings.windSpeed);
            main.startColor = settings.streakColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(settings.size.x * 0.85f, settings.size.y * 0.85f, 0.1f);

            var emission = streaks.emission;
            emission.rateOverTime = settings.streakRate;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Gizmos.color = GizmoColor;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, settings.range * 0.5f),
                                new Vector3(settings.size.x, settings.size.y, settings.range));
            Gizmos.matrix = old;
            AirGizmos.DrawArrow(transform.position, transform.forward * 5f);
        }
    }
}
