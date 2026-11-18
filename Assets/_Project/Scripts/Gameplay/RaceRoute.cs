using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// A timed route: fly through every ring in order, then land on a landing target to finish.
    /// The rings are this object's Checkpoint children, in hierarchy order. To change the route,
    /// just move, add, remove or reorder the ring children in the Hierarchy.
    ///
    /// The timer is the plane's flight time (throw to touchdown). Best time is saved per scene
    /// in PlayerPrefs (a tiny key/value store Unity keeps per project on this machine).
    /// </summary>
    public class RaceRoute : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [Tooltip("Speed added when you fly through the correct ring. A small reward for good lines.")]
        [SerializeField] float ringSpeedBonus = 3f;

        public event Action<Checkpoint> RingPassed;

        readonly List<Checkpoint> rings = new List<Checkpoint>();

        public int RingCount => rings.Count;
        public int NextIndex { get; private set; }
        public bool AllRingsPassed => NextIndex >= rings.Count;
        public Checkpoint NextRing => AllRingsPassed ? null : rings[NextIndex];
        public float BestTime { get; private set; }
        /// <summary>Result text after the last landing (null while flying).</summary>
        public string LastResult { get; private set; }
        public bool LastWasNewBest { get; private set; }

        FoldSelector foldSelector;

        /// <summary>Name of the fold the best time belongs to (empty if there's no fold picker).</summary>
        public string BestTimeFold => foldSelector != null && foldSelector.Current != null ? foldSelector.Current.displayName : "";

        // Best times are kept per scene AND per fold: a Glider time shouldn't compete with a Stunt time.
        string BestKey => $"PaperWings.BestRoute.{SceneManager.GetActiveScene().name}.{BestTimeFold}";

        void Awake()
        {
            GetComponentsInChildren(true, rings);
            for (int i = 0; i < rings.Count; i++)
            {
                rings[i].Index = i;
                rings[i].Route = this;
            }
        }

        void Start()
        {
            foldSelector = FindAnyObjectByType<FoldSelector>();
            if (foldSelector != null) foldSelector.FoldChanged += _ => LoadBestTime();
            LoadBestTime();
            ResetRoute();
        }

        void LoadBestTime() => BestTime = PlayerPrefs.GetFloat(BestKey, 0f);

        void OnEnable() => LandingZone.AnyLanded += OnLanded;
        void OnDisable() => LandingZone.AnyLanded -= OnLanded;

        void Update()
        {
            // Back in hand = new attempt.
            if (plane != null && plane.State == PlaneState.Held && (NextIndex > 0 || LastResult != null))
                ResetRoute();
        }

        public void ResetRoute()
        {
            NextIndex = 0;
            LastResult = null;
            LastWasNewBest = false;
            for (int i = 0; i < rings.Count; i++)
                rings[i].SetState(i == 0 ? Checkpoint.RingState.Next : Checkpoint.RingState.Pending);
        }

        public void OnCheckpointHit(Checkpoint ring, PlaneController who)
        {
            if (who != plane || plane.State != PlaneState.Flying) return;
            if (ring.Index != NextIndex) return;   // out-of-order rings are simply ignored

            ring.SetState(Checkpoint.RingState.Passed);
            NextIndex++;
            if (!AllRingsPassed) rings[NextIndex].SetState(Checkpoint.RingState.Next);

            plane.AddSpeed(ringSpeedBonus);
            RingPassed?.Invoke(ring);
        }

        void OnLanded(PlaneController who, string message)
        {
            if (who != plane) return;

            if (!AllRingsPassed)
            {
                LastResult = $"{NextIndex}/{rings.Count} rings. Fly through them all, then land, to set a time.";
                return;
            }

            float time = plane.FlightTime;
            LastWasNewBest = BestTime <= 0f || time < BestTime;
            if (LastWasNewBest)
            {
                BestTime = time;
                PlayerPrefs.SetFloat(BestKey, time);
                PlayerPrefs.Save();
            }
            LastResult = LastWasNewBest
                ? $"ROUTE COMPLETE  {time:0.00}s   NEW BEST!"
                : $"ROUTE COMPLETE  {time:0.00}s   (best {BestTime:0.00}s)";
        }

        [ContextMenu("Clear Best Time")]
        void ClearBestTime()
        {
            PlayerPrefs.DeleteKey(BestKey);
            BestTime = 0f;
        }

        void OnDrawGizmos()
        {
            // Scene view: dotted-ish line through the rings in order.
            var children = GetComponentsInChildren<Checkpoint>(true);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            for (int i = 0; i + 1 < children.Length; i++)
                Gizmos.DrawLine(children[i].transform.position, children[i + 1].transform.position);
        }
    }
}
