using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// Something that moves on its own without a PlaneController (a courier glider, a flying boss).
    /// CombatTarget reads its velocity so aim assist, pencils and AI can lead their shots.
    /// </summary>
    public interface IMovingTarget
    {
        Vector3 Velocity { get; }
    }
}
