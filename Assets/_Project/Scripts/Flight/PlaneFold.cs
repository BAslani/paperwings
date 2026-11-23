using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// One paper fold (plane type): how it flies, how tough its paper is, and how it looks.
    /// A fold is just a bundle of existing assets, so a new fold is: duplicate a FlightSettings
    /// and a PaperSettings, tweak them, point a new PlaneFold at them, and add it to the
    /// FoldSelector's list (on the GameFlow object in each scene).
    /// </summary>
    [CreateAssetMenu(fileName = "Fold", menuName = "PaperWings/Plane Fold")]
    public class PlaneFold : ScriptableObject
    {
        public string displayName = "Dart";
        [Tooltip("Unlock id needed to pick this fold (see the Progression settings). Empty = always available.")]
        public string unlockId = "";
        [Tooltip("Short id for this fold's own upgrades, e.g. 'dart' gives 'dart:pencils'. Each fold's upgrades are bought separately.")]
        public string foldId = "";
        [TextArea(2, 4)] public string description = "Fast and steady. A good all-rounder.";

        [Header("Propeller upgrade")]
        [Tooltip("Where the pusher propeller sits on this fold (local to the plane's Visual), once bought for it.")]
        public Vector3 propellerMount = new Vector3(0f, 0f, -0.92f);

        /// <summary>The id used for this fold's upgrades (falls back to the display name).</summary>
        public string Key => !string.IsNullOrEmpty(foldId) ? foldId : displayName.ToLowerInvariant();

        [Header("Behaviour")]
        public FlightSettings flight;
        public PaperSettings paper;

        [Header("Looks")]
        public Mesh mesh;
        [Tooltip("Base paper colour (damage and wetness tint on top of this).")]
        public Color paperColor = new Color(0.96f, 0.95f, 0.9f);
        [Tooltip("Paper stock: lined, graph, cardstock... (optional; white-based, tinted by Paper Color).")]
        public Texture2D paperTexture;
        [Tooltip("Optional extra decoration attached while this fold is selected (the propeller is a separate upgrade). " +
                 "Spawned under the plane's Visual, so it wobbles with the paper.")]
        public GameObject attachment;
    }
}
