using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A party balloon tied to the furniture (Stage 29, the break room's office party). It bobs on
    /// its string and blocks the air like any obstacle; fly into it and you bounce off it. One staple
    /// pops it with a burst of confetti and a little puff of warm air, and a new one is blown up
    /// a while later. Clear a lane through a cluster, or leave them as cover in a dogfight.
    /// </summary>
    public class Balloon : MonoBehaviour, IDamageable
    {
        [SerializeField] Transform body;
        [SerializeField] ParticleSystem popBurst;
        [Tooltip("Optional warm puff released when it pops (a ColumnAir set to strength 0).")]
        [SerializeField] Air.ColumnAir puff;
        [SerializeField] float respawnTime = 25f;
        [SerializeField] float bobAmount = 0.8f;
        [SerializeField] float swayAmount = 6f;

        public bool IsAlive => popped == false;
        public float Health01 => popped ? 0f : 1f;

        bool popped;
        float respawnAt, phase, puffOff;
        Vector3 restPos;
        Collider[] colliders;
        Renderer[] renderers;

        void Awake()
        {
            if (body == null) body = transform;
            restPos = body.localPosition;
            phase = Random.value * 10f;
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            if (popped)
            {
                if (Time.time >= respawnAt) SetPopped(false);
            }
            else
            {
                float t = Time.time + phase;
                body.localPosition = restPos + new Vector3(Mathf.Sin(t * 0.7f) * 0.3f, Mathf.Sin(t * 1.3f) * bobAmount, 0f);
                body.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * swayAmount, 0f, Mathf.Sin(t * 0.6f) * swayAmount);
            }
            if (puff != null && puffOff > 0f && Time.time >= puffOff) { puff.targetStrength = 0f; puffOff = 0f; }
        }

        public void ApplyDamage(float amount, PlaneController source)
        {
            if (popped || amount <= 0f) return;
            if (popBurst != null) popBurst.Play();
            if (puff != null) { puff.SetStrengthNow(1f); puffOff = Time.time + 1.5f; }
            Audio.AudioFx.PlayAt(Audio.Sfx.Pop, body.position, 1f, Random.Range(0.9f, 1.2f));
            SetPopped(true);
        }

        void SetPopped(bool p)
        {
            popped = p;
            respawnAt = Time.time + respawnTime;
            foreach (var c in colliders) if (c != null) c.enabled = !p;
            foreach (var r in renderers) if (r != null && !(r is ParticleSystemRenderer)) r.enabled = !p;   // keep confetti and the puff visible
        }
    }
}
