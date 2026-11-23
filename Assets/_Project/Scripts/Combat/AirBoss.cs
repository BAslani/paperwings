using System.Collections;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Shared machinery for bosses built on air (Stage 26): health, armor and weak spot, the
    /// wake-up warning, volleys, defeat and rematch, and the weak-spot glow. A boss subclass only
    /// writes its attack round as a coroutine (<see cref="Round"/>) and switches its air on and off.
    /// </summary>
    public abstract class AirBoss : BossBase
    {
        [SerializeField] protected AirBossSettings settings;
        [Tooltip("Renderers that glow when the weak spot is open.")]
        [SerializeField] protected Renderer[] weakSpotGlow;
        [Tooltip("Where volleys come from.")]
        [SerializeField] protected Transform muzzle;
        [SerializeField] protected StapleProjectile projectilePrefab;
        [Tooltip("Inactive object holding rival spawners; switched on in phase 3.")]
        [SerializeField] protected GameObject reinforcements;
        [SerializeField] protected ParticleSystem defeatBurst;
        [Tooltip("Optional: lights that turn warning-red during a telegraph.")]
        [SerializeField] protected Light[] moodLights;
        [Header("Stage 29: drones, shield, parts")]
        [Tooltip("Inactive object holding rival spawners: the drones it launches to shield itself.")]
        [SerializeField] protected GameObject drones;
        [Tooltip("Shown while the drone shield is up (a translucent bubble).")]
        [SerializeField] protected GameObject shieldVisual;

        public AirBossSettings Settings => settings;
        public override string DisplayName => settings != null ? settings.displayName : name;
        public override string Title => settings != null ? settings.title : "";
        public override float AggroRange => settings != null ? settings.aggroRange : 80f;
        public override float Phase2At => settings != null ? settings.phase2At : 0.6f;
        public override float Phase3At => settings != null ? settings.phase3At : 0.3f;
        public override float WeakSpotMultiplier => settings != null ? settings.weakSpotMultiplier : 2f;
        public override float Health01 => settings != null ? health / settings.maxHealth : 0f;
        public override bool IsDefeated => defeated;
        public override bool IsAwake => awake;
        public override bool Waking => waking;
        public override bool WeakSpotOpen => weakOpen && !defeated;
        public override bool Warning => warning;
        public override string WarningText => warningText;
        /// <summary>True while its drones shield it: hits do nothing until they're shot down.</summary>
        public bool Shielded => shielded;
        public override string ArmoredHint => shielded
            ? $"SHIELDED: shoot down its drones ({dronesDowned}/{dronesNeeded})"
            : (parts.Length > 0 && PartsLeft > 0 ? $"{BaseHint}   ·   shoot off its {parts[0].PartName.ToLowerInvariant()}s ({PartsLeft} left)" : BaseHint);
        /// <summary>The fight's normal armored hint (subclasses override this, not ArmoredHint).</summary>
        protected virtual string BaseHint => "ARMORED: dodge, then hit the weak spot";
        public int PartsLeft { get { int n = 0; foreach (var p in parts) if (p != null && !p.Broken) n++; return n; } }
        protected BossPart[] Parts => parts;

        protected bool weakOpen, warning;
        protected string warningText = "INCOMING!";
        bool shielded;
        int dronesDowned, dronesNeeded;
        BossPart[] parts = new BossPart[0];
        float health, flash;
        bool defeated, awake, waking, wasAwake;
        MaterialPropertyBlock block;
        Color[] moodBase;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected virtual void Awake()
        {
            parts = GetComponentsInChildren<BossPart>(true);
            block = new MaterialPropertyBlock();
            if (moodLights != null)
            {
                moodBase = new Color[moodLights.Length];
                for (int i = 0; i < moodLights.Length; i++) if (moodLights[i] != null) moodBase[i] = moodLights[i].color;
            }
            ResetBoss();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            StartCoroutine(Brain());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAllCoroutines();
            AirOff();
        }

        public override void ResetFight()
        {
            StopAllCoroutines();
            ResetBoss();
            wasAwake = false;
            if (isActiveAndEnabled) StartCoroutine(Brain());
        }

        void ResetBoss()
        {
            health = settings != null ? settings.maxHealth : 1f;
            defeated = false;
            weakOpen = warning = waking = false;
            if (reinforcements != null) reinforcements.SetActive(false);
            EndShield();
            foreach (var p in parts) if (p != null) p.ResetPart();
            AirOff();
            OnResetBoss();
        }

        /// <summary>Switch every air source off (between rounds, on reset and on defeat).</summary>
        protected abstract void AirOff();
        /// <summary>Back to the idle pose.</summary>
        protected virtual void OnResetBoss() { }
        /// <summary>One attack round: telegraph, attack, weak window. Loops while the player is near.</summary>
        protected abstract IEnumerator Round(PlaneController player);
        /// <summary>Little "I see you" animation while waking (called every frame of the wake-up).</summary>
        protected virtual void WakeTick(float t) { }

        IEnumerator Brain()
        {
            while (true)
            {
                if (defeated || settings == null) { awake = false; yield return null; continue; }

                var player = FindFlyingPlayer();
                bool inRange = player != null && Vector3.Distance(transform.position, player.transform.position) < settings.aggroRange;
                awake = inRange && player.FlightTime >= settings.engageDelay;
                if (!awake) { wasAwake = false; AirOff(); weakOpen = warning = false; yield return null; continue; }

                if (!wasAwake)
                {
                    wasAwake = true;
                    waking = true;
                    float end = Time.time + settings.wakeUpTime;
                    while (Time.time < end && !defeated)
                    {
                        WakeTick(1f - (end - Time.time) / settings.wakeUpTime);
                        yield return null;
                    }
                    waking = false;
                    continue;
                }

                if (Phase >= 3 && reinforcements != null && !reinforcements.activeSelf) reinforcements.SetActive(true);
                yield return Round(player);
                if (defeated) continue;
                yield return new WaitForSeconds(settings.restTime * (Phase >= 3 ? 0.5f : 1f));
            }
        }

        /// <summary>Warn the player (HUD text + red mood lights) for the telegraph time.</summary>
        protected IEnumerator Telegraph(string text, float seconds)
        {
            warning = true;
            warningText = text;
            yield return new WaitForSeconds(seconds * (Phase >= 3 ? 0.75f : 1f));
            warning = false;
        }

        /// <summary>Opens the weak spot for the vulnerable window.</summary>
        protected IEnumerator WeakWindow(float seconds)
        {
            weakOpen = true;
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 0.9f);
            float end = Time.time + seconds;
            while (Time.time < end && !defeated) yield return null;
            weakOpen = false;
        }

        /// <summary>A fan of projectiles aimed where the player is going.</summary>
        protected void FireVolley(PlaneController player, int shots, float spread)
        {
            if (projectilePrefab == null || settings == null || player == null) return;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 5f;
            float travel = Vector3.Distance(origin, player.transform.position) / settings.projectileSpeed;
            Vector3 aim = (player.transform.position + player.Velocity * travel - origin).normalized;
            Vector3 axis = Vector3.Cross(aim, Vector3.up).sqrMagnitude > 0.01f ? Vector3.up : Vector3.right;
            int n = Mathf.Max(1, shots);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                Vector3 dir = Quaternion.AngleAxis(Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t), axis) * aim;
                var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
                p.transform.localScale *= settings.projectileScale;
                p.Launch(null, transform, Faction.Accounting, dir * settings.projectileSpeed, settings.projectileDamage, settings.projectileLifetime);
            }
            Audio.AudioFx.PlayAt(Audio.Sfx.StapleFire, origin, 1f, 0.7f);
        }

        public override void ApplyDamage(float amount, PlaneController source)
        {
            if (defeated || amount <= 0f || settings == null) return;
            if (shielded)
            {
                flash = 0.3f;
                Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, transform.position, 0.5f, 2.6f);
                return;
            }
            bool weak = WeakSpotOpen;
            float dealt = amount * (weak ? settings.weakSpotMultiplier : settings.armoredMultiplier);
            health = Mathf.Max(0f, health - dealt);
            flash = weak ? 1f : 0.4f;
            RaiseDamaged(dealt, weak);
            if (!weak) Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, transform.position, 0.6f, 2f);
            if (health <= 0f) Defeat();
        }

        /// <summary>A part was shot off: the boss loses a chunk of health (armor doesn't help).</summary>
        public void PartDestroyed(BossPart part)
        {
            if (defeated || settings == null) return;
            float dealt = settings.maxHealth * part.BossDamageFraction;
            health = Mathf.Max(0f, health - dealt);
            flash = 1f;
            RaiseDamaged(dealt, true);
            OnPartDestroyed(part);
            if (health <= 0f) Defeat();
        }

        /// <summary>React to losing a part (stop the fan it powered, etc.).</summary>
        protected virtual void OnPartDestroyed(BossPart part) { }

        // ------------------------------------------------------------------ Stage 29 attacks

        /// <summary>
        /// Launches its drones and stays shielded until each has been shot down once (or the
        /// timeout). The drones are regular rivals from the spawners under 'drones'.
        /// </summary>
        protected IEnumerator DroneShield(float timeout)
        {
            if (drones == null) yield break;
            var spawners = drones.GetComponentsInChildren<EnemySpawner>(true);
            if (spawners.Length == 0) yield break;
            shielded = true;
            dronesDowned = 0;
            dronesNeeded = spawners.Length;
            if (shieldVisual != null) shieldVisual.SetActive(true);
            drones.SetActive(true);
            foreach (var sp in spawners) if (sp.Current != null) sp.Current.Downed += OnDroneDowned;
            float end = Time.time + timeout;
            while (dronesDowned < dronesNeeded && Time.time < end && !defeated) yield return null;
            foreach (var sp in spawners) if (sp.Current != null) sp.Current.Downed -= OnDroneDowned;
            EndShield();
        }

        void OnDroneDowned(bool crash) => dronesDowned++;

        void EndShield()
        {
            shielded = false;
            if (shieldVisual != null) shieldVisual.SetActive(false);
            if (drones != null) drones.SetActive(false);
        }

        Vector3 MuzzlePos => muzzle != null ? muzzle.position : transform.position + Vector3.up * 5f;

        void Shoot(Vector3 origin, Vector3 dir, float speedScale)
        {
            if (projectilePrefab == null || settings == null) return;
            var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
            p.transform.localScale *= settings.projectileScale;
            p.Launch(null, transform, Faction.Accounting, dir * settings.projectileSpeed * speedScale, settings.projectileDamage, settings.projectileLifetime * 1.6f);
        }

        /// <summary>A flat ring of shots outward from the muzzle (tilted up/down by 'elevation' degrees). Fly through the gaps.</summary>
        protected void FireRing(int shots, float elevation = 0f, float speedScale = 0.6f, float phaseDeg = 0f)
        {
            Vector3 origin = MuzzlePos;
            for (int i = 0; i < shots; i++)
            {
                float yaw = phaseDeg + i * 360f / shots;
                Shoot(origin, Quaternion.Euler(-elevation, yaw, 0f) * Vector3.forward, speedScale);
            }
            Audio.AudioFx.PlayAt(Audio.Sfx.StapleFire, origin, 1f, 0.55f);
        }

        /// <summary>Spinning arms of shots for 'duration' seconds: a spiral to weave through.</summary>
        protected IEnumerator FireSpiral(float duration, float shotsPerSecond, float degPerSecond, int arms, float elevation = 0f, float speedScale = 0.55f)
        {
            float end = Time.time + duration, next = 0f, angle = Random.value * 360f;
            while (Time.time < end && !defeated)
            {
                angle += degPerSecond * Time.deltaTime;
                if (Time.time >= next)
                {
                    next = Time.time + 1f / Mathf.Max(0.5f, shotsPerSecond);
                    Vector3 origin = MuzzlePos;
                    for (int a = 0; a < arms; a++)
                        Shoot(origin, Quaternion.Euler(-elevation, angle + a * 360f / arms, 0f) * Vector3.forward, speedScale);
                    Audio.AudioFx.PlayAt(Audio.Sfx.StapleFire, origin, 0.6f, 0.6f);
                }
                yield return null;
            }
        }

        /// <summary>Aimed burst from any muzzle (turrets).</summary>
        protected void FireAimed(Transform from, PlaneController player, int shots, float spread)
        {
            if (from == null || player == null || settings == null) return;
            float travel = Vector3.Distance(from.position, player.transform.position) / settings.projectileSpeed;
            Vector3 aim = (player.transform.position + player.Velocity * travel - from.position).normalized;
            for (int i = 0; i < shots; i++)
                Shoot(from.position, Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * aim, 1f);
            Audio.AudioFx.PlayAt(Audio.Sfx.StapleFire, from.position, 0.8f, 0.8f);
        }

        /// <summary>Straight down from a point (bombs).</summary>
        protected void Drop(Vector3 from, Vector3 carrierVelocity, float fallSpeed)
        {
            Vector3 v = carrierVelocity * 0.5f + Vector3.down * fallSpeed;
            if (projectilePrefab == null || settings == null) return;
            var p = Instantiate(projectilePrefab, from, Quaternion.LookRotation(v));
            p.transform.localScale *= settings.projectileScale * 1.4f;
            p.Launch(null, transform, Faction.Accounting, v, settings.projectileDamage * 1.5f, 4f);
        }

        void Defeat()
        {
            defeated = true;
            weakOpen = warning = false;
            EndShield();
            StopAllCoroutines();
            AirOff();
            if (defeatBurst != null) defeatBurst.Play();
            Audio.AudioFx.PlayAt(Audio.Sfx.BossDefeated, transform.position, 1f, 1f, 0f);
            if (reinforcements != null) reinforcements.SetActive(false);
            OnDefeated();
            CombatScore.AddKill();
            RaiseDefeated();
            StartCoroutine(RespawnLater());
        }

        protected virtual void OnDefeated() { }

        IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(settings != null ? settings.respawnDelay : 20f);
            ResetBoss();
            wasAwake = false;
            StartCoroutine(Brain());
        }

        protected virtual void Update()
        {
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            Color glow = WeakSpotOpen ? new Color(1f, 0.85f, 0.2f) * (2.2f + 0.6f * Mathf.Sin(Time.time * 10f)) : new Color(0.15f, 0.12f, 0.05f);
            if (warning) glow = new Color(1f, 0.2f, 0.1f) * (1.5f + Mathf.Sin(Time.time * 25f));
            glow += Color.white * (flash * 2f);
            block.SetColor(EmissionId, glow);
            if (weakSpotGlow != null) foreach (var r in weakSpotGlow) if (r != null) r.SetPropertyBlock(block);

            if (moodLights != null && moodBase != null)
                for (int i = 0; i < moodLights.Length; i++)
                    if (moodLights[i] != null)
                        moodLights[i].color = Color.Lerp(moodLights[i].color, warning ? new Color(1f, 0.25f, 0.15f) : moodBase[i], 1f - Mathf.Exp(-6f * Time.deltaTime));
        }
    }
}
