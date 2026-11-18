using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// One ring on a route (a sticky-note hoop). It has a trigger volume; when the plane flies
    /// through it, it tells its RaceRoute. The route decides if it was the right ring.
    ///
    /// The frame has NO solid colliders on purpose. Clipping a ring should never crash you.
    ///
    /// UNITY NOTE: A collider with "Is Trigger" ticked doesn't block anything; it just reports
    /// OnTriggerEnter when another collider (attached to a Rigidbody, like our plane) enters it.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        public enum RingState { Pending, Next, Passed }

        [SerializeField] Renderer[] frameRenderers;

        public int Index { get; set; }
        public RaceRoute Route { get; set; }
        public RingState State { get; private set; } = RingState.Pending;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        static readonly Color PendingColor = new Color(0.95f, 0.85f, 0.35f);
        static readonly Color NextColor = new Color(1f, 0.9f, 0.2f);
        static readonly Color PassedColor = new Color(0.4f, 1f, 0.5f);

        MaterialPropertyBlock block;
        float popTimer;
        Vector3 baseScale;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Route == null) return;
            var plane = other.GetComponentInParent<PlaneController>();
            if (plane != null) Route.OnCheckpointHit(this, plane);
        }

        public void SetState(RingState state)
        {
            if (state == RingState.Passed && State != RingState.Passed) popTimer = 1f;
            State = state;
        }

        void Update()
        {
            // Colour per state. The NEXT ring pulses so your eye finds it.
            Color color, emission;
            switch (State)
            {
                case RingState.Next:
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
                    color = NextColor;
                    emission = NextColor * (1.5f * pulse);
                    break;
                case RingState.Passed:
                    color = PassedColor;
                    emission = PassedColor * 0.3f;
                    break;
                default:
                    color = PendingColor * 0.7f;
                    emission = Color.black;
                    break;
            }

            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionId, emission);
            foreach (var r in frameRenderers) if (r != null) r.SetPropertyBlock(block);

            // Quick "pop" when passed: scale up then settle.
            popTimer = Mathf.MoveTowards(popTimer, 0f, Time.deltaTime * 3f);
            transform.localScale = baseScale * (1f + 0.25f * popTimer * popTimer);
        }
    }
}
