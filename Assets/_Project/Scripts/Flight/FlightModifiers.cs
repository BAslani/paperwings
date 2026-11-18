namespace PaperWings.Flight
{
    /// <summary>
    /// Temporary multipliers on top of FlightSettings, written by other systems
    /// (paper damage and sogginess now; plane folds or power-ups later).
    /// 1 = no change for scales, 0 = no change for offsets.
    ///
    /// Keeping these separate means FlightSettings stays the "healthy plane" baseline
    /// and other systems never edit the asset at runtime.
    /// </summary>
    public class FlightModifiers
    {
        /// <summary>Multiplies glide ratio. Below 1 = sinks faster.</summary>
        public float glideScale = 1f;
        /// <summary>Multiplies pitch and turn response. Below 1 = sluggish.</summary>
        public float controlScale = 1f;
        /// <summary>Degrees of bank the plane drifts to with no input (a bent wing). +right / -left.</summary>
        public float rollBias = 0f;
        /// <summary>Extra sink in units/sec (e.g. soggy paper is heavier).</summary>
        public float extraSink = 0f;

        public void Reset()
        {
            glideScale = 1f;
            controlScale = 1f;
            rollBias = 0f;
            extraSink = 0f;
        }
    }
}
