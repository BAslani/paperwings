using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Steam rising from a hot drink: a thermal column that puffs (lift pulses) and sways
    /// (the column leans more the higher you go). Place the object at the cup's rim.
    ///
    /// This is a good example of extending the air field: it's just another AirSource
    /// with its own GetWind, and the plane, HUD and gizmos pick it up automatically.
    /// </summary>
    public class SteamSource : AirSource
    {
        [SerializeField] SteamSettings settings;
        [SerializeField] ParticleSystem steam;

        public override AirKind Kind => AirKind.Thermal;

        float seed;

        void Awake() => seed = Random.value * 50f;

        /// <summary>Current lift multiplier from puffing, around 1.</summary>
        float PuffStrength()
        {
            float n = Mathf.PerlinNoise(Time.time * settings.puffFrequency, seed) * 2f - 1f;   // -1..1
            return 1f + settings.puffAmount * n;
        }

        /// <summary>Column center at a given height: sways more toward the top.</summary>
        Vector3 CenterAt(float heightAboveBase)
        {
            float t = Time.time * settings.swayFrequency;
            Vector3 sway = new Vector3(Mathf.PerlinNoise(t, seed + 7f) - 0.5f, 0f, Mathf.PerlinNoise(seed + 13f, t) - 0.5f) * 2f;
            float lean = Mathf.Clamp01(heightAboveBase / settings.height);
            return transform.position + sway * (settings.swayAmount * lean);
        }

        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            influence = 0f;
            if (settings == null) return Vector3.zero;

            float h = worldPosition.y - transform.position.y;
            if (h < 0f || h > settings.height) return Vector3.zero;

            Vector3 center = CenterAt(h);
            Vector3 flat = new Vector3(worldPosition.x - center.x, 0f, worldPosition.z - center.z);
            float r = flat.magnitude;
            if (r > settings.radius) return Vector3.zero;

            // Same shape as the laptop thermal: soft core, fade in at the bottom, fade out at the top.
            float r01 = r / settings.radius;
            float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.coreFraction, 1f, r01));
            float bottom = Mathf.Clamp01(h / 1.5f);
            float top = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settings.topFade, 1f, h / settings.height));
            influence = radial * bottom * top;

            Vector3 lift = Vector3.up * (settings.liftSpeed * PuffStrength() * influence);
            Vector3 centering = r > 0.01f ? -flat / r * (settings.centeringAssist * r01 * bottom * top) : Vector3.zero;
            return lift + centering;
        }

        public override Bounds GetBounds()
        {
            float r = settings != null ? settings.radius + settings.swayAmount : 1f;
            float h = settings != null ? settings.height : 1f;
            return new Bounds(transform.position + Vector3.up * h * 0.5f, new Vector3(r * 2f, h, r * 2f));
        }

        void Update()
        {
            if (steam == null || settings == null) return;
            // Particles rise a bit slower than the air and puff with it, so you can SEE the gusts.
            float puff = PuffStrength();
            float rise = Mathf.Max(1f, settings.liftSpeed * 0.5f);

            var main = steam.main;
            main.startSpeed = rise * puff;
            main.startLifetime = settings.height / rise;
            main.startColor = settings.steamColor;
            main.startSize = new ParticleSystem.MinMaxCurve(settings.steamSize.x, settings.steamSize.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Visual spread is its own knob, so you can tighten the look without shrinking the lift zone.
            var shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = settings.radius * settings.steamSpread;

            var emission = steam.emission;
            emission.rateOverTime = settings.steamRate * puff;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;
            Gizmos.color = GizmoColor;
            const int rings = 5;
            for (int i = 0; i <= rings; i++)
            {
                float h = settings.height * i / rings;
                AirGizmos.DrawCircle(CenterAt(h) + Vector3.up * h, Vector3.up, settings.radius);
            }
            AirGizmos.DrawArrow(transform.position, Vector3.up * 5f);
        }
    }
}
