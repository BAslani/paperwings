using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// A dripping tap (Stage 47, Tiny War's bathroom): a thin column under the tap where drops fall.
    /// It barely pushes you, but it is WET: fly through it and your paper soaks up water (heavier,
    /// sluggish) just like steam, without steam's lift. Drops fall in bursts, so there are dry gaps.
    /// </summary>
    public class DripAir : AirSource
    {
        public float radius = 3.5f;
        [Tooltip("How far down the drops fall (to the basin or the floor).")]
        public float height = 30f;
        [Tooltip("Downward push while drops are falling (units/s).")]
        public float fallSpeed = 4f;
        [Tooltip("Seconds of dripping, then of dry, in turn.")]
        public float dripTime = 2.5f, dryTime = 1.5f;
        public float cycleOffset;
        [SerializeField] ParticleSystem drops;

        public bool Dripping { get; private set; }
        float strength;

        public override AirKind Kind => AirKind.Downdraft;
        public override float Moisture => strength;
        public override AirLook Look => AirLook.Steam;
        public override bool IsHeat => false;

        void Update()
        {
            float cycle = dripTime + dryTime;
            Dripping = Mathf.Repeat(Time.time + cycleOffset, cycle) < dripTime;
            strength = Mathf.MoveTowards(strength, Dripping ? 1f : 0f, Time.deltaTime * 3f);
            if (drops != null)
            {
                var em = drops.emission;
                em.enabled = Dripping;
            }
        }

        /// <summary>Sampled from the tap's spout (this transform) downward.</summary>
        public override Vector3 GetWind(Vector3 worldPosition, out float influence)
        {
            Vector3 local = worldPosition - transform.position;
            influence = 0f;
            if (local.y > 0.5f || local.y < -height || strength <= 0.01f) return Vector3.zero;
            float r = new Vector2(local.x, local.z).magnitude;
            if (r > radius) return Vector3.zero;
            influence = strength * (1f - r / radius);
            return Vector3.down * fallSpeed * influence;
        }

        public override Bounds GetBounds() =>
            new Bounds(transform.position + Vector3.down * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f));
    }
}
