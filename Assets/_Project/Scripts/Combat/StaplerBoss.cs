using System;
using System.Collections;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE STAPLER: a giant desk stapler boss.
    ///
    /// Core rhythm: its jaw is ARMORED while shut (hits mostly clink off). It must OPEN to fire,
    /// and while open its mouth glows: the WEAK SPOT. Dodge the barrage, then punish the opening.
    ///
    /// Phases (by remaining health):
    ///   1. Staple fans: open, fire a fan at you, stay open a moment, close.
    ///   2. + Slam: jaw rises (warning), snaps shut and blasts a radial downdraft (BossSlamAir).
    ///   3. + Reinforcements (two rival planes) and faster barrages.
    /// Defeated: slumps open with a paper burst, then returns after a while for a rematch.
    ///
    /// The attack loop is a coroutine so it reads top-to-bottom like a script.
    /// UNITY NOTE: a coroutine is a method that can pause ("yield return") and resume on later
    /// frames, which is handy for timed sequences like "open, wait, fire, wait, close".
    /// </summary>
    public class StaplerBoss : BossBase
    {
        [SerializeField] BossSettings settings;
        [Tooltip("The hinged top arm. Rotating it on X opens the jaw.")]
        [SerializeField] Transform jaw;
        [Tooltip("Renderers that glow when the weak spot is exposed.")]
        [SerializeField] Renderer[] weakSpotGlow;
        [Tooltip("Where staples come out (between the jaws, facing forward).")]
        [SerializeField] Transform muzzle;
        [SerializeField] StapleProjectile staplePrefab;
        [SerializeField] BossSlamAir slamAir;
        [Tooltip("Inactive object holding rival spawners; switched on in phase 3.")]
        [SerializeField] GameObject reinforcements;
        [SerializeField] ParticleSystem slamBurst;
        [SerializeField] ParticleSystem defeatBurst;

        public BossSettings Settings => settings;
        public override string DisplayName => settings != null ? settings.displayName : "THE STAPLER";
        public override string Title => settings != null ? settings.title : "";
        public override float AggroRange => settings != null ? settings.aggroRange : 45f;
        public override float Phase2At => settings != null ? settings.phase2At : 0.6f;
        public override float Phase3At => settings != null ? settings.phase3At : 0.3f;
        public override float WeakSpotMultiplier => settings != null ? settings.weakSpotMultiplier : 2f;
        public override float Health01 => settings != null ? health / settings.maxHealth : 0f;
        public override bool IsDefeated => defeated;
        public override bool IsAwake => awake;
        public override bool Waking => waking;
        /// <summary>True while the jaw is open enough to expose the weak spot.</summary>
        public override bool WeakSpotOpen => jawOpen01 > 0.45f && !IsDefeated;
        /// <summary>True during the slam wind-up (for HUD warnings).</summary>
        public bool SlamWarning { get; private set; }
        public override bool Warning => SlamWarning;
        public override string WarningText => "SLAM INCOMING!  get clear!";
        public override string ArmoredHint => "ARMORED: dodge, then hit it when the jaw opens";

        bool defeated, awake, waking;
        bool wasAwake;

        float health;
        float jawOpen01, jawTarget, jawSpeed;
        float flash;
        float nextSlamTime;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (slamAir != null && settings != null)
            {
                slamAir.radius = settings.slamRadius;
                slamAir.height = settings.slamHeight;
                slamAir.downSpeed = settings.slamDownSpeed;
                slamAir.outwardSpeed = settings.slamOutwardSpeed;
            }
            ResetBoss();
        }

        protected override void OnEnable() { base.OnEnable(); StartCoroutine(Brain()); }

        /// <summary>Full health, jaw shut, attack loop restarted. Used when a Boss Fight begins.</summary>
        public override void ResetFight()
        {
            StopAllCoroutines();   // cancels any pending rematch timer or attack in progress
            ResetBoss();
            jawOpen01 = 0f;
            wasAwake = false;
            SlamWarning = false;
            waking = false;
            if (isActiveAndEnabled) StartCoroutine(Brain());
        }

        void ResetBoss()
        {
            health = settings != null ? settings.maxHealth : 1f;
            defeated = false;
            jawTarget = 0f;
            jawSpeed = settings != null ? settings.jawSpeed : 3f;
            nextSlamTime = 0f;
            if (reinforcements != null) reinforcements.SetActive(false);
        }

        // ------------------------------------------------------------------ attack loop

        IEnumerator Brain()
        {
            while (true)
            {
                // Beaten: leave the jaw slumped open until the rematch.
                if (IsDefeated) { awake = false; yield return null; continue; }

                PlaneController player = FindPlayer();
                bool inRange = player != null &&
                               Vector3.Distance(transform.position, player.transform.position) < settings.aggroRange;
                // Fairness: no attacks in the first few seconds after a throw.
                bool graceOver = player != null && player.FlightTime >= settings.engageDelay;
                awake = inRange && graceOver;

                if (!IsAwake) { wasAwake = false; jawTarget = 0f; yield return null; continue; }

                // Just noticed you: rattle the jaw as a warning before the first attack.
                if (!wasAwake)
                {
                    wasAwake = true;
                    yield return WakeUp();
                    continue;
                }

                if (Phase >= 3 && reinforcements != null && !reinforcements.activeSelf) reinforcements.SetActive(true);

                if (Phase >= 2 && Time.time >= nextSlamTime)
                {
                    yield return Slam();
                    nextSlamTime = Time.time + settings.slamInterval;
                    continue;
                }

                yield return Barrage(player);
                float rest = settings.barrageInterval * (Phase >= 3 ? 0.6f : 1f);
                yield return new WaitForSeconds(rest);
            }
        }

        IEnumerator WakeUp()
        {
            waking = true;
            jawSpeed = 10f;
            float end = Time.time + settings.wakeUpTime;
            while (Time.time < end && !IsDefeated)
            {
                // Chatter the jaw open and shut: "clack-clack-clack".
                float target = Mathf.Repeat(Time.time, 0.25f) < 0.125f ? 0.18f : 0f;
                if (target > 0f && jawTarget == 0f) Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, transform.position, 0.8f);
                jawTarget = target;
                yield return null;
            }
            jawTarget = 0f;
            waking = false;
        }

        IEnumerator Barrage(PlaneController player)
        {
            // Open up (the warning), fire one fan (two from phase 2), linger open (your window), close.
            jawSpeed = settings.jawSpeed;
            jawTarget = 1f;
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 0.9f);   // jaw opening
            yield return new WaitForSeconds(settings.openTelegraph);

            int fans = Phase >= 2 ? 2 : 1;
            for (int i = 0; i < fans && !IsDefeated; i++)
            {
                FireFan(player);
                Audio.AudioFx.PlayAt(Audio.Sfx.StapleFire, muzzle != null ? muzzle.position : transform.position, 1f, 0.6f);   // big, low "CHUNK"
                yield return new WaitForSeconds(0.35f);
            }

            yield return new WaitForSeconds(settings.vulnerableTime);
            if (!IsDefeated) jawTarget = 0f;
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator Slam()
        {
            // Wind-up: jaw rises halfway and the HUD warns you.
            SlamWarning = true;
            jawSpeed = settings.jawSpeed;
            jawTarget = 0.6f;
            yield return new WaitForSeconds(settings.slamTelegraph);

            // SNAP shut and blast the air.
            jawSpeed = 12f;
            jawTarget = 0f;
            yield return new WaitForSeconds(0.08f);
            SlamWarning = false;
            if (IsDefeated) yield break;
            if (slamAir != null) slamAir.Pulse(settings.slamDuration);
            Audio.AudioFx.PlayAt(Audio.Sfx.BossSlam, transform.position, 1f, 1f, 0f);
            if (slamBurst != null) slamBurst.Play();
            yield return new WaitForSeconds(1f);
        }

        void FireFan(PlaneController player)
        {
            if (staplePrefab == null || muzzle == null || player == null) return;

            // Aim at where the player will be, then spread shots across the fan.
            Vector3 origin = muzzle.position;
            float travel = Vector3.Distance(origin, player.transform.position) / settings.projectileSpeed;
            Vector3 aim = (player.transform.position + player.Velocity * travel - origin).normalized;

            int n = Mathf.Max(1, settings.shotsPerFan);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                float yaw = Mathf.Lerp(-settings.fanAngle * 0.5f, settings.fanAngle * 0.5f, t);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * aim;
                var staple = Instantiate(staplePrefab, origin, Quaternion.LookRotation(dir));
                staple.transform.localScale *= 2f;   // boss staples are BIG
                staple.Launch(null, transform, Faction.Accounting, dir * settings.projectileSpeed,
                              settings.projectileDamage, settings.projectileLifetime);
            }
        }

        // ------------------------------------------------------------------ damage

        public override void ApplyDamage(float amount, PlaneController source)
        {
            if (IsDefeated || amount <= 0f) return;

            bool weak = WeakSpotOpen;
            float dealt = amount * (weak ? settings.weakSpotMultiplier : settings.armoredMultiplier);
            health = Mathf.Max(0f, health - dealt);
            flash = weak ? 1f : 0.4f;
            RaiseDamaged(dealt, weak);
            if (!weak) Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, transform.position, 0.6f, 2f);   // armor "clink"

            if (health <= 0f) Defeat();
        }

        void Defeat()
        {
            defeated = true;
            SlamWarning = false;
            jawSpeed = 1.5f;
            jawTarget = 1f;   // slumps open
            if (defeatBurst != null) defeatBurst.Play();
            Audio.AudioFx.PlayAt(Audio.Sfx.BossDefeated, transform.position, 1f, 1f, 0f);
            if (reinforcements != null) reinforcements.SetActive(false);
            CombatScore.AddKill();
            RaiseDefeated();
            StartCoroutine(RespawnLater());
        }

        IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(settings.respawnDelay);
            ResetBoss();
        }

        // ------------------------------------------------------------------ visuals

        void Update()
        {
            if (settings == null) return;

            // Jaw animation: ease toward the target opening.
            jawOpen01 = Mathf.MoveTowards(jawOpen01, jawTarget, jawSpeed * Time.deltaTime);
            if (jaw != null) jaw.localRotation = Quaternion.Euler(-settings.jawOpenAngle * jawOpen01, 0f, 0f);

            // Weak spot glow: bright when open, a red pulse during the slam warning, a flash on hits.
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            Color glow = WeakSpotOpen ? new Color(1f, 0.85f, 0.2f) * 2.5f : new Color(0.2f, 0.15f, 0.05f);
            if (SlamWarning) glow = new Color(1f, 0.2f, 0.1f) * (1.5f + Mathf.Sin(Time.time * 25f));
            glow += Color.white * (flash * 2f);
            block.SetColor(EmissionId, glow);
            if (weakSpotGlow != null)
                foreach (var r in weakSpotGlow) if (r != null) r.SetPropertyBlock(block);
        }

        static PlaneController FindPlayer()
        {
            foreach (var p in PlaneController.All)
                if (p.IsPlayer && p.State == PlaneState.Flying) return p;
            return null;
        }
    }
}
