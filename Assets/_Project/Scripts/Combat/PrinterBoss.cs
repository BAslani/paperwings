using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE PRINTER (Stage 33): the office's boss. The big laser printer on its stand in the corner
    /// wakes up and PRINTS Accounting fighters that hunt you around the office, while it shoots
    /// sheets of paper at you. It never leaves its stand; the fight is about everything it sends out:
    ///   - PRINT JOB: it prints a fighter from each paper tray and hides behind a toner shield until
    ///     every fighter has been shot down. In phase 3 each tray prints two.
    ///   - PAPER TRAYS (parts): shoot one off and that tray stops printing. With both gone it can't
    ///     print at all and falls back on its paper storms.
    ///   - SHEET FEED: whenever it's not jammed it fires aimed fans of paper sheets at you.
    ///   - PAPER STORM: spinning arms of sheets from the output slot. Weave between them.
    ///   - PAPER JAM: after every attack it jams, rattles and smokes, and its lid pops open: the hot
    ///     FUSER inside is the weak spot. Its heat rises in a column: ride it up and shoot down into it.
    /// </summary>
    public class PrinterBoss : AirBoss
    {
        [Header("The machine")]
        [Tooltip("Everything visible (shakes when it prints or jams).")]
        [SerializeField] Transform body;
        [Tooltip("The lid's hinge at the back: it swings open when the printer jams.")]
        [SerializeField] Transform lidHinge;
        [SerializeField] float lidOpenAngle = -70f;

        [Header("Paper trays (parts) and what they print")]
        [SerializeField] BossPart[] trays;
        [Tooltip("Each fighter spawner prints from the tray with the same index modulo the tray count. " +
                 "The first spawner of each tray prints in every job; the rest only in phase 3.")]
        [SerializeField] EnemySpawner[] printSpawners;

        [Header("Sheet feed")]
        [Tooltip("Seconds between aimed fans of sheets (shorter in later phases).")]
        [SerializeField] float feedInterval = 4f;
        [Tooltip("Only fires at you inside this range.")]
        [SerializeField] float feedRange = 150f;

        [Header("Looks")]
        [SerializeField] ParticleSystem printStream;
        [SerializeField] ParticleSystem jamSmoke;
        [Tooltip("Rises from the open fuser while it's jammed.")]
        [SerializeField] ColumnAir fuserHeat;
        [Tooltip("Scene objects hidden while the boss is out (the ordinary office printer on its stand).")]
        [SerializeField] GameObject[] hideWhileActive;

        protected override string BaseHint => "ARMORED: shoot down what it prints, shoot off its trays, hit the fuser when it jams";
        public override string WeakSpotText => "PAPER JAM! the lid is open: shoot the glowing fuser inside";

        Vector3 restPos;
        Quaternion lidClosed;
        float lid, shake, nextFeed;
        bool jammed;
        int round;

        protected override void Awake()
        {
            if (body != null) restPos = body.localPosition;
            if (lidHinge != null) lidClosed = lidHinge.localRotation;
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (hideWhileActive != null) foreach (var g in hideWhileActive) if (g != null) g.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hideWhileActive != null) foreach (var g in hideWhileActive) if (g != null) g.SetActive(true);
        }

        protected override void AirOff()
        {
            if (fuserHeat != null) fuserHeat.targetStrength = 0f;
            if (jamSmoke != null) jamSmoke.Stop();
        }

        protected override void OnResetBoss()
        {
            round = 0;
            jammed = false;
            lid = 0f;
            shake = 0f;
            if (body != null) body.localPosition = restPos;
            if (lidHinge != null) lidHinge.localRotation = lidClosed;
        }

        protected override void WakeTick(float t) => shake = 0.5f * (1f - t);

        int TraysLeft { get { int n = 0; if (trays != null) foreach (var t in trays) if (t != null && !t.Broken) n++; return n; } }

        bool Prints(EnemySpawner sp)
        {
            if (printSpawners == null || trays == null || trays.Length == 0) return false;
            int i = System.Array.IndexOf(printSpawners, sp);
            if (i < 0) return false;
            var tray = trays[i % trays.Length];
            if (tray == null || tray.Broken) return false;
            return i < trays.Length || Phase >= 3;
        }

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            float pace = Phase >= 3 ? 0.85f : 1f;

            if (TraysLeft > 0 && round % 2 == 1)
            {
                // PRINT JOB: fighters out of the slot, shielded until they're all down.
                yield return Telegraph(TraysLeft == 1 ? "PRINT JOB!  it's printing a fighter" : "PRINT JOB!  it's printing fighters", settings.telegraph);
                shake = 0.35f;
                if (printStream != null) printStream.Play();
                Audio.AudioFx.PlayAt(Audio.Sfx.Ding, transform.position, 0.8f, 0.7f);
                yield return DroneShield(26f, Prints);
                shake = 0f;
            }
            else
            {
                // PAPER STORM: spinning arms of sheets.
                yield return Telegraph("PAPER STORM!  weave between the sheets", settings.telegraph);
                shake = 0.25f;
                int arms = Phase >= 3 ? 3 : 2;
                yield return FireSpiral(settings.attackDuration * pace, Phase >= 2 ? 4.5f : 3.5f, 55f, arms, -4f, 0.5f);
                if (Phase >= 2) FireRing(10, 8f, 0.45f);
                shake = 0f;
            }

            // PAPER JAM: the lid pops open, the fuser glows and its heat rises.
            jammed = true;
            if (jamSmoke != null) jamSmoke.Play();
            if (fuserHeat != null) { fuserHeat.verticalSpeed = settings.secondaryAirSpeed; fuserHeat.targetStrength = 1f; }
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 1f, 0.7f);
            yield return WeakWindow(settings.vulnerableTime * (TraysLeft == 0 ? 1.25f : 1f));
            AirOff();
            jammed = false;
        }

        protected override void OnDefeated()
        {
            jammed = true;   // lid flops open, smoking
            shake = 0f;
            if (printStream != null) printStream.Stop();
            if (jamSmoke != null) jamSmoke.Play();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;

            // Lid: swings open on a jam (and when beaten), shuts again after.
            lid = Mathf.MoveTowards(lid, jammed ? 1f : 0f, dt * (jammed ? 3f : 1.5f));
            if (lidHinge != null) lidHinge.localRotation = lidClosed * Quaternion.Euler(lidOpenAngle * lid, 0f, 0f);

            // Rattles while printing, waking or jammed.
            if (body != null)
            {
                float s = Mathf.Max(shake, jammed && !IsDefeated ? 0.2f : 0f);
                body.localPosition = restPos + (s > 0f ? new Vector3(Mathf.Sin(Time.time * 57f), Mathf.Sin(Time.time * 71f) * 0.4f, Mathf.Cos(Time.time * 49f)) * s : Vector3.zero);
            }

            // SHEET FEED: aimed fans of paper whenever it's awake and not jammed.
            if (!IsAwake || Waking || IsDefeated || jammed || settings == null) return;
            if (Time.time < nextFeed) return;
            nextFeed = Time.time + feedInterval * (Phase >= 3 ? 0.65f : Phase >= 2 ? 0.8f : 1f);
            var player = FindFlyingPlayer();
            Vector3 from = muzzle != null ? muzzle.position : (body != null ? body.position : transform.position);
            if (player != null && Vector3.Distance(player.transform.position, from) < feedRange)
                FireVolley(player, settings.shotsPerVolley, settings.spreadAngle);
        }
    }
}
