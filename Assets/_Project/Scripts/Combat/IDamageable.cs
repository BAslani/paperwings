using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Anything weapons can hurt: paper planes (PaperDamage), bosses (StaplerBoss), future turrets...
    /// Projectiles look for this on whatever they hit, so new enemy types just implement it.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        /// <summary>0..1 remaining health, for health bars.</summary>
        float Health01 { get; }
        /// <summary>'source' = the plane responsible, if any (for kill credit).</summary>
        void ApplyDamage(float amount, PlaneController source);
    }
}
