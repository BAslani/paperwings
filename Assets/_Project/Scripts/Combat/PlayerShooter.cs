using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>Connects the player's Fire button to their staple gun. Hold to auto-fire.</summary>
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] PlaneInput input;
        [SerializeField] StapleGun gun;

        void Update()
        {
            if (input != null && gun != null && input.FireHeld) gun.TryFire();
        }
    }
}
