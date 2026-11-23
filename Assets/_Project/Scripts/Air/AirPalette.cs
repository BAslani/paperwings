using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>What a patch of air looks like to the player. One colour each, used everywhere.</summary>
    public enum AirLook
    {
        Heat,       // rising warm air: laptop, microwave, radiator, exhaust. Lifts you and dries paper.
        Steam,      // rising wet air: tea, kettles. Strong lift, soggy paper.
        Updraft,    // rising air that isn't heat: fan upwash, floor vents, the rooftop condenser.
        Draft,      // sideways wind: fans, wall vents, outdoor wind.
        Downdraft   // sinking air: ceiling AC, fan cores, slams. A hazard.
    }

    /// <summary>
    /// The air colour language (Stage 23). Every particle effect, gizmo and HUD label for air reads
    /// its colour from here, so a player learns once what orange, white, green, cyan and violet mean
    /// and can read a column of air from across the room before flying into it.
    /// Sources keep the alpha from their own settings (how faint the effect is), only the hue is fixed.
    /// </summary>
    public static class AirPalette
    {
        public static readonly Color Heat = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Steam = new Color(0.93f, 0.96f, 1f);
        public static readonly Color Updraft = new Color(0.45f, 1f, 0.55f);
        public static readonly Color Draft = new Color(0.3f, 0.85f, 1f);
        public static readonly Color Downdraft = new Color(0.62f, 0.45f, 1f);

        public static Color Of(AirLook look) => look switch
        {
            AirLook.Heat => Heat,
            AirLook.Steam => Steam,
            AirLook.Updraft => Updraft,
            AirLook.Downdraft => Downdraft,
            _ => Draft
        };

        /// <summary>The palette hue with the alpha of <paramref name="alphaFrom"/> (a source's tuned particle colour).</summary>
        public static Color Of(AirLook look, Color alphaFrom)
        {
            Color c = Of(look);
            c.a = alphaFrom.a;
            return c;
        }

        public static Color Of(AirLook look, float alpha)
        {
            Color c = Of(look);
            c.a = alpha;
            return c;
        }

        /// <summary>"#RRGGBB" for IMGUI rich text.</summary>
        public static string Hex(AirLook look) => "#" + ColorUtility.ToHtmlStringRGB(Of(look));

        /// <summary>The look of an air sample, for HUD labels (strongest component wins).</summary>
        public static AirLook LookOf(AirSample air)
        {
            if (air.downdraft > air.thermal && air.downdraft > air.draft) return AirLook.Downdraft;
            if (air.thermal >= air.draft && air.thermal > 0.05f)
            {
                if (air.moisture > 0.2f) return AirLook.Steam;
                return air.dryHeat > 0.05f ? AirLook.Heat : AirLook.Updraft;
            }
            return AirLook.Draft;
        }
    }
}
