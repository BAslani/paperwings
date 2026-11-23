using System;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Pencil missiles for the player's plane.
    ///  - Pencils hang visibly in wing slots (one pencil per slot, 4 on the dart).
    ///  - Lock-on: keep a rival inside the lock cone for 'lockTime' seconds. The lock holds until
    ///    it leaves a wider cone, so you don't lose it from a little wobble.
    ///  - Fire (G / RMB / LT) while locked: the next pencil detaches from its slot and homes in.
    ///    No lock = no launch. Slots refill when the plane is back in hand.
    /// </summary>
    public class PencilLauncher : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] PlaneInput input;
        [SerializeField] PencilSettings settings;
        [SerializeField] PencilMissile pencilPrefab;
        [Tooltip("Mount points under the wings, in firing order (alternate sides so it stays balanced).")]
        [SerializeField] Transform[] slots;

        /// <summary>The target being locked (or locked): a rival or a boss. Null if none in front of you.</summary>
        public CombatTarget Candidate { get; private set; }
        /// <summary>0..1 lock progress on the Candidate.</summary>
        public float LockProgress { get; private set; }
        public bool IsLocked => Candidate != null && LockProgress >= 1f;
        public int PencilsLeft { get; private set; }
        /// <summary>Pencils carried: the wing slots, capped by what's unlocked (0 = not unlocked yet).</summary>
        public int MaxPencils => slots != null ? Mathf.Min(slots.Length, slotLimit) : 0;

        int slotLimit = int.MaxValue;

        /// <summary>How many slots may hold pencils (set by progression). Re-arms right away if in hand.</summary>
        public void SetSlotLimit(int limit)
        {
            slotLimit = Mathf.Max(0, limit);
            if (mounted != null && (plane == null || plane.State != PlaneState.Flying)) Rearm();
        }
        public PencilSettings Settings => settings;

        public event Action<PencilMissile> Fired;
        /// <summary>Fire was pressed without a lock (or with no pencils left).</summary>
        public event Action FireRefused;

        PencilMissile[] mounted;
        float nextFireTime;

        void Start() => Rearm();

        void Update()
        {
            if (plane == null || settings == null) return;

            if (plane.State == PlaneState.Held)
            {
                if (PencilsLeft != MaxPencils) Rearm();   // refill, or drop pencils a mission bans
                ClearLock();
                return;
            }
            if (plane.State != PlaneState.Flying || MaxPencils == 0) { ClearLock(); return; }   // not flying, or pencils not unlocked yet

            UpdateLock(Time.deltaTime);

            if (input != null && input.MissilePressed)
            {
                if (IsLocked && PencilsLeft > 0 && Time.time >= nextFireTime) Fire();
                else FireRefused?.Invoke();
            }
        }

        // ------------------------------------------------------------------ lock-on

        void UpdateLock(float dt)
        {
            if (PencilsLeft == 0) { ClearLock(); return; }

            // Keep the current candidate while it's still reasonably in front (wider cone once locked).
            float keepAngle = IsLocked ? settings.keepLockAngle : settings.lockAngle;
            if (Candidate != null && !InCone(Candidate, keepAngle)) ClearLock();

            if (Candidate == null)
            {
                Candidate = FindCandidate();
                LockProgress = 0f;
            }
            if (Candidate != null)
                LockProgress = Mathf.MoveTowards(LockProgress, 1f, dt / Mathf.Max(0.01f, settings.lockTime));
        }

        void ClearLock()
        {
            Candidate = null;
            LockProgress = 0f;
        }

        // Any enemy-side CombatTarget can be locked: rival planes, bosses...
        CombatTarget FindCandidate() =>
            CombatTarget.FindBestInCone(plane.transform.position, plane.transform.forward,
                                        settings.lockAngle, settings.lockRange, CombatTarget.FactionOfPlane(plane));

        bool InCone(CombatTarget other, float angle)
        {
            if (other == null || !other.IsAlive) return false;
            Vector3 to = other.Position - plane.transform.position;
            return to.magnitude <= settings.lockRange && Vector3.Angle(plane.transform.forward, to) <= angle;
        }

        // ------------------------------------------------------------------ pencils

        void Fire()
        {
            for (int i = 0; i < mounted.Length; i++)
            {
                if (mounted[i] == null) continue;
                PencilMissile pencil = mounted[i];
                mounted[i] = null;
                PencilsLeft--;
                nextFireTime = Time.time + settings.fireCooldown;
                pencil.Launch(plane, Candidate, settings);
                Fired?.Invoke(pencil);

                var wobble = plane.GetComponent<PaperWobble>();
                if (wobble != null) wobble.Kick(3f);   // a little lurch as the weight drops off
                return;
            }
        }

        /// <summary>Puts a fresh pencil in every empty slot.</summary>
        public void Rearm()
        {
            if (slots == null || pencilPrefab == null) return;
            if (mounted == null || mounted.Length != slots.Length) mounted = new PencilMissile[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                // Slots beyond the unlocked count stay empty (and lose any pencil they had).
                if (i >= MaxPencils)
                {
                    if (mounted[i] != null) { Destroy(mounted[i].gameObject); mounted[i] = null; }
                    continue;
                }
                if (mounted[i] != null || slots[i] == null) continue;
                mounted[i] = Instantiate(pencilPrefab, slots[i], false);   // parented, zero local offset
                mounted[i].name = "Pencil (mounted)";
            }
            PencilsLeft = 0;
            foreach (var m in mounted) if (m != null) PencilsLeft++;
        }
    }
}
