namespace PaperWings.Combat
{
    /// <summary>
    /// The departments at war after hours. Every combatant belongs to one; weapons, lock-on,
    /// AI targeting and HUD markers all work by faction ("is this on my side?").
    /// Add more departments here later (IT, HR, Legal...).
    /// </summary>
    public enum Faction
    {
        Marketing,    // the player and wingmen
        Accounting,   // rivals and The Stapler
        Neutral       // room hazards (vending-machine cans): they hit everybody, nobody targets them
    }
}
