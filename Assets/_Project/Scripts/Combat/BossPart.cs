using System;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A piece of a boss you can shoot off (Stage 29): the Auditor's wing turrets, the Mainframe's
    /// fans, the Condenser's coolant tanks. Parts aren't armored like the boss's body, so they're
    /// the thing to shoot while its weak spot is shut. Knocking one off takes a chunk of the boss's
    /// health and takes away the attack it powered.
    /// </summary>
    public class BossPart : MonoBehaviour, IDamageable
    {
        [SerializeField] string partName = "TURRET";
        [SerializeField] float maxHealth = 120f;
        [Tooltip("Fraction of the boss's max health it loses when this part is destroyed.")]
        [Range(0f, 0.5f)] [SerializeField] float bossDamageOnDestroy = 0.08f;
        [Tooltip("Flash on hit (emission).")]
        [SerializeField] Renderer[] flashRenderers;
        [SerializeField] ParticleSystem destroyBurst;
        [Tooltip("Smoke that keeps pouring out once it's destroyed.")]
        [SerializeField] ParticleSystem smoke;
        [Tooltip("Hidden once destroyed (the turret barrel, the fan blades...). Their colliders go with them.")]
        [SerializeField] GameObject[] hideWhenDestroyed;

        public event Action<BossPart> Destroyed;

        public string PartName => partName;
        public bool Broken => health <= 0f;
        /// <summary>Only takes hits while its boss is awake (and not beaten).</summary>
        public bool IsAlive => !Broken && (boss == null || (boss.IsAwake && !boss.IsDefeated));
        public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
        public float BossDamageFraction => bossDamageOnDestroy;

        float health, flash;
        AirBoss boss;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            boss = GetComponentInParent<AirBoss>();
            block = new MaterialPropertyBlock();
            ResetPart();
        }

        public void ResetPart()
        {
            health = maxHealth;
            if (hideWhenDestroyed != null) foreach (var g in hideWhenDestroyed) if (g != null) g.SetActive(true);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void ApplyDamage(float amount, PlaneController source)
        {
            if (!IsAlive || amount <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            flash = 1f;
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, transform.position, 0.7f, 1.4f);
            if (health > 0f) return;

            if (destroyBurst != null) destroyBurst.Play();
            if (smoke != null) smoke.Play();
            if (hideWhenDestroyed != null) foreach (var g in hideWhenDestroyed) if (g != null) g.SetActive(false);
            Audio.AudioFx.PlayAt(Audio.Sfx.PencilImpact, transform.position, 1f, 0.8f);
            Destroyed?.Invoke(this);
            if (boss != null) boss.PartDestroyed(this);
        }

        void Update()
        {
            if (flashRenderers == null || flashRenderers.Length == 0) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
            Color c = Broken ? Color.black : new Color(1f, 0.35f, 0.15f) * (0.4f + 0.3f * Mathf.Sin(Time.time * 4f)) + Color.white * (flash * 2f);
            block.SetColor(EmissionId, c);
            foreach (var r in flashRenderers) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
