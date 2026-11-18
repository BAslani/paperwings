namespace PaperWings.Flight
{
    /// <summary>
    /// Anything that can "fly" a plane: the player's controller (PlaneInput) or an AI pilot.
    /// The PlaneController only ever talks to this interface, so the same flight model
    /// works for both. Put exactly one component that implements it on each plane.
    /// </summary>
    public interface IFlightInput
    {
        /// <summary>-1..1, + = nose up.</summary>
        float PitchInput { get; }
        /// <summary>-1..1, + = bank right.</summary>
        float RollInput { get; }
        /// <summary>True once per boost request.</summary>
        bool ConsumeBoost();
    }
}
