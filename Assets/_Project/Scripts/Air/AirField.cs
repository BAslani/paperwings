using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Air
{
    public enum AirKind
    {
        Draft,     // mostly sideways push: fans, wall vents
        Thermal,   // mostly upward lift: laptops, coffee steam, radiators
        Downdraft  // mostly downward push: ceiling AC vents (a hazard)
    }

    /// <summary>The combined air at one point in space.</summary>
    public struct AirSample
    {
        /// <summary>Sum of all wind velocities at this point (units/sec).</summary>
        public Vector3 wind;
        /// <summary>Strongest thermal influence here, 0 (none) to 1 (core).</summary>
        public float thermal;
        /// <summary>Strongest draft influence here, 0 (none) to 1 (core).</summary>
        public float draft;
        /// <summary>Strongest downdraft influence here, 0 (none) to 1 (core).</summary>
        public float downdraft;
        /// <summary>How wet the air is here, 0..1 (strongest moisture x influence of any source).</summary>
        public float moisture;
        /// <summary>Strongest DRY thermal here, 0..1 (e.g. the laptop, not steam). Dries paper out.</summary>
        public float dryHeat;

        public bool InThermal => thermal > 0.05f;
        public bool InDraft => draft > 0.05f;
        public bool InDowndraft => downdraft > 0.05f;
    }

    /// <summary>
    /// The air field: a simple registry of every active AirSource.
    /// Anything that wants to know "what is the air doing here?" calls AirField.Sample(position).
    ///
    /// Sources register themselves when enabled, so there's no manager object to place in a scene.
    /// To add a new kind of source (AC vent, steam, radiator): subclass AirSource and implement GetWind.
    /// </summary>
    public static class AirField
    {
        static readonly List<AirSource> sources = new List<AirSource>();

        public static IReadOnlyList<AirSource> Sources => sources;

        /// <summary>Multiplies every wind and updraft (campaign "rough air" nights raise it). 1 = normal.</summary>
        public static float WindScale = 1f;

        public static void Register(AirSource s)
        {
            if (!sources.Contains(s)) sources.Add(s);
        }

        public static void Unregister(AirSource s) => sources.Remove(s);

        public static AirSample Sample(Vector3 position)
        {
            AirSample result = default;
            for (int i = 0; i < sources.Count; i++)
            {
                AirSource s = sources[i];
                Vector3 wind = s.GetWind(position, out float influence);
                if (influence <= 0f) continue;

                result.wind += wind * WindScale;
                result.moisture = Mathf.Max(result.moisture, s.Moisture * influence);
                switch (s.KindAt(position))
                {
                    case AirKind.Thermal:
                        result.thermal = Mathf.Max(result.thermal, influence);
                        if (s.IsHeat) result.dryHeat = Mathf.Max(result.dryHeat, influence * (1f - s.Moisture));   // steam and fan upwash aren't dry heat
                        break;
                    case AirKind.Downdraft: result.downdraft = Mathf.Max(result.downdraft, influence); break;
                    default:                result.draft = Mathf.Max(result.draft, influence); break;
                }
            }
            return result;
        }

        // UNITY NOTE: If "Enter Play Mode Options" skips domain reload, static fields survive
        // between Play sessions. This clears the list at the start of every Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { sources.Clear(); WindScale = 1f; }
    }
}
