using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// A little paper propeller on a pin. Purely visual: it "windmills" in the airflow, so it spins
    /// faster the faster you fly, whirls up hard on a boost, idles lazily in hand, and stops when
    /// you're down. At high speed a faint blur disc fades in, like a real spinning prop.
    /// Lives on a fold attachment (see PlaneFold.attachment); finds its plane automatically.
    /// </summary>
    public class Propeller : MonoBehaviour
    {
        [Tooltip("The part that spins (blades + hub). Spins around its local Z (the plane's nose axis).")]
        [SerializeField] Transform rotor;
        [Tooltip("Optional translucent disc shown when spinning fast.")]
        [SerializeField] Renderer blurDisc;

        [Header("Spin (degrees per second)")]
        [SerializeField] float idleSpin = 120f;
        [Tooltip("Extra spin per unit of airspeed (windmilling).")]
        [SerializeField] float spinPerSpeed = 70f;
        [Tooltip("Extra spin right after a boost; fades out.")]
        [SerializeField] float boostSpin = 2600f;
        [SerializeField] float boostFade = 1.5f;

        PlaneController plane;
        MaterialPropertyBlock block;
        float spin, boostExtra;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            plane = GetComponentInParent<PlaneController>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable() { if (plane != null) plane.Boosted += OnBoost; }
        void OnDisable() { if (plane != null) plane.Boosted -= OnBoost; }

        void OnBoost() => boostExtra = boostSpin;

        void Update()
        {
            if (rotor == null) return;
            if (plane == null)
            {
                // On a display stand (the hangar preview): turn lazily, even while the game is paused.
                rotor.Rotate(0f, 0f, idleSpin * 2f * Time.unscaledDeltaTime, Space.Self);
                if (blurDisc != null) blurDisc.enabled = false;
                return;
            }
            float dt = Time.deltaTime;

            float target = plane.State switch
            {
                PlaneState.Flying => idleSpin + spinPerSpeed * plane.Speed + boostExtra,
                PlaneState.Held => idleSpin,
                _ => 0f   // down or landed: winds down to a stop
            };
            boostExtra = Mathf.MoveTowards(boostExtra, 0f, boostSpin / boostFade * dt);
            spin = Mathf.Lerp(spin, target, 1f - Mathf.Exp(-4f * dt));
            rotor.Rotate(0f, 0f, spin * dt, Space.Self);

            if (blurDisc != null)
            {
                float a = Mathf.InverseLerp(900f, 3000f, spin) * 0.35f;
                blurDisc.enabled = a > 0.01f;
                block.SetColor(BaseColorId, new Color(1f, 1f, 1f, a));
                blurDisc.SetPropertyBlock(block);
            }
        }
    }
}
