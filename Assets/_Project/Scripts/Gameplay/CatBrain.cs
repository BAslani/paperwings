using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Biscuit the cat (Tiny War): neutral, never hurt, swats anything that flies too low, any side.
    /// She sits and grooms (the model's own animation); everything else is done here, on top of it.
    ///
    /// She hunts like a sitting cat, reacting to planes rather than a timer:
    ///  - WATCH: a plane nearby: she stops grooming, her face follows it.
    ///  - STALK: a plane low and close: she sinks into a crouch (front paws stay planted), her eyes
    ///    widen, the tip of her tail twitches, her hips start to wiggle. The longer it stays, the
    ///    readier she gets: that build-up IS the warning.
    ///  - STRIKE: when she's ready and it's in reach: a one-paw bat with the paw on its side, often a
    ///    second bat with the other paw if it's still there, or, for something passing just above her,
    ///    she rears up and grabs at it with both paws. Hitboxes ride on her paws, and a short downdraft
    ///    slams down where she strikes.
    ///  - RECOVER: she watches it go, tail flicking; now and then she pretends she didn't care and grooms.
    ///
    /// Staying natural (nothing bends that shouldn't):
    ///  - Legs are moved by two-bone IK (upper arm and forearm reach for a point), so their lengths
    ///    never change, and each elbow bends the way the animation already bends it.
    ///  - While she crouches, her front paws are pinned where the animation put them, flat on the floor.
    ///  - Spine bends are shared across three bones and limited; hind legs are never touched.
    ///  - Every change is eased; strikes are fast but continuous curves, never jumps.
    ///
    /// UNITY NOTE: the Animator writes the animated bones every frame, then LateUpdate runs, so this
    /// layer goes in LateUpdate. Each bone first goes back to its clean pose (Bone.Reset) so nothing
    /// we wrote last frame builds up. Bone references and clip poses are filled in by Stage 44.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CatBrain : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Her idle loop (sitting, grooming).")]
        public AnimationClip idleClip;
        [Tooltip("Seconds into the idle clip where she sits still with her paws down: the pose she holds while watching.")]
        public float restTime;

        [Header("Bones")]
        public Transform hips;
        public Transform chest;
        [Tooltip("Lower, middle and upper spine: crouching and rearing are shared between them.")]
        public Transform[] spine;
        public Transform neck;
        public Transform head;
        [Tooltip("Not moved (the head turn carries them); kept for a later eye pass.")]
        public Transform eyeL, eyeR;
        public Transform lidL, lidR;
        public Transform earL, earR;
        [Tooltip("Front legs: upper arm, forearm (elbow) and wrist, left and right.")]
        public Transform upperL, lowerL, pawL, upperR, lowerR, pawR;
        public Transform[] tail;

        [Tooltip("The head bone's own axis that points out of her face (found by Stage 44; rigs differ).")]
        public Vector3 headFace = Vector3.forward;
        [Tooltip("The head bone's own axis that points out of the top of her head.")]
        public Vector3 headUp = Vector3.up;

        [Header("Poses from the clip (local rotations)")]
        public Quaternion lidOpenL = Quaternion.identity, lidClosedL = Quaternion.identity;
        public Quaternion lidOpenR = Quaternion.identity, lidClosedR = Quaternion.identity;
        public Quaternion earBackL = Quaternion.identity, earBackR = Quaternion.identity;

        [Header("Hunting")]
        [Tooltip("The downdraft that slams down where she strikes (strength is driven from here).")]
        public BeamAir slam;
        [Tooltip("How close a plane has to be before she pays attention.")]
        public float watchRange = 170f;
        [Tooltip("Stalking starts when a plane is this many front-leg lengths away (horizontally) and low.")]
        public float stalkRange = 10f;
        [Tooltip("She strikes when a plane is within this many front-leg lengths of her shoulders.")]
        public float strikeRange = 3f;
        [Tooltip("Seconds of stalking before she's ready to strike.")]
        public float buildUp = 1.1f;
        [Tooltip("Seconds between strikes.")]
        public float cooldown = 1.3f;
        public float maxYaw = 75f, maxPitch = 40f;

        // ------------------------------------------------------------------ bones

        sealed class Bone
        {
            public readonly Transform t;
            Quaternion written, baseRot;
            bool has;
            public Bone(Transform t) { this.t = t; }
            /// <summary>This frame's pose before our tweaks: the animation's, or our last base if the animation didn't touch it.</summary>
            public Quaternion Base()
            {
                if (t == null) return Quaternion.identity;
                if (!has || t.localRotation != written) baseRot = t.localRotation;
                return baseRot;
            }
            public void Write(Quaternion q) { if (t == null) return; t.localRotation = q; written = q; has = true; }
            /// <summary>Back to the clean pose, so nothing written last frame builds up.</summary>
            public void Reset() { if (t != null) Write(Base()); }
            /// <summary>Applies a world-space rotation on top of the bone's current pose (it turns about its own pivot).</summary>
            public void Turn(Quaternion worldDelta) { if (t != null) SetWorld(worldDelta * t.rotation); }
            public void SetWorld(Quaternion world)
            {
                if (t == null) return;
                var parent = t.parent != null ? t.parent.rotation : Quaternion.identity;
                Write(Quaternion.Inverse(parent) * world);
            }
        }

        enum State { Idle, Stalk, Strike, Recover }
        enum Kind { BatLeft, BatRight, Rear }

        // Strike timings (seconds).
        const float WindUp = 0.16f, Lash = 0.10f, Hold = 0.08f, Back = 0.32f;          // one-paw bat
        const float Rise = 0.35f, Grab = 0.12f, RearHold = 0.15f, Down = 0.45f;          // rear-up grab
        const float MaxSpineBend = 13f;                                                  // degrees per spine bone

        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        Bone bHips, bChest, bNeck, bHead, bLidL, bLidR, bEarL, bEarR, bUpperL, bLowerL, bPawL, bUpperR, bLowerR, bPawR;
        Bone[] bSpine, bTail, all;

        PlaneController[] planes = new PlaneController[0];
        float nextScan;
        Transform target;
        State state;
        Kind kind;
        bool secondBat;
        float strikeStart, cooldownEnd, ignoreUntil, readiness;
        Vector3 strikePoint;
        float legLength;

        float watch, crouch, rear, yaw, pitch, yawVel, pitchVel;
        float blinkStart, blinkLength, nextBlink, earTwitch, nextTwitch;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bHips = new Bone(hips); bChest = new Bone(chest); bNeck = new Bone(neck); bHead = new Bone(head);
            bLidL = new Bone(lidL); bLidR = new Bone(lidR); bEarL = new Bone(earL); bEarR = new Bone(earR);
            bUpperL = new Bone(upperL); bLowerL = new Bone(lowerL); bPawL = new Bone(pawL);
            bUpperR = new Bone(upperR); bLowerR = new Bone(lowerR); bPawR = new Bone(pawR);
            bSpine = Wrap(spine);
            bTail = Wrap(tail);
            // Parents before children, so resetting a parent never disturbs a child's clean pose.
            var list = new System.Collections.Generic.List<Bone> { bHips };
            list.AddRange(bSpine);
            list.AddRange(new[] { bChest, bNeck, bHead, bLidL, bLidR, bEarL, bEarR, bUpperL, bLowerL, bPawL, bUpperR, bLowerR, bPawR });
            list.AddRange(bTail);
            all = list.ToArray();
            nextBlink = Time.time + Random.Range(1.5f, 4f);
            nextTwitch = Time.time + Random.Range(2f, 6f);
            if (slam != null) { slam.targetStrength = 0f; slam.ramp = 15f; }
        }

        static Bone[] Wrap(Transform[] ts)
        {
            var b = new Bone[ts != null ? ts.Length : 0];
            for (int i = 0; i < b.Length; i++) b[i] = new Bone(ts[i]);
            return b;
        }

        void OnEnable()
        {
            if (idleClip == null) return;
            // Two copies of the idle clip: one playing (grooming), one frozen at the rest pose (watching).
            graph = PlayableGraph.Create("Biscuit");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var groom = AnimationClipPlayable.Create(graph, idleClip);
            var rest = AnimationClipPlayable.Create(graph, idleClip);
            rest.SetTime(restTime);
            rest.SetSpeed(0);
            graph.Connect(groom, 0, mixer, 0);
            graph.Connect(rest, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            var output = AnimationPlayableOutput.Create(graph, "Biscuit", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            if (slam != null) slam.targetStrength = 0f;
        }

        // ------------------------------------------------------------------ deciding

        Vector3 Shoulders => upperL != null && upperR != null ? (upperL.position + upperR.position) * 0.5f : transform.position;

        void Update()
        {
            ScanForPlanes();
            float dt = Time.deltaTime, now = Time.time;
            float reach = Mathf.Max(0.01f, legLength);

            bool inZone = false, inStrike = false;
            if (target != null && head != null)
            {
                var flat = Vector3.ProjectOnPlane(target.position - transform.position, transform.up);
                inZone = flat.magnitude < stalkRange * reach && target.position.y < head.position.y + 5f * reach;
                inStrike = Vector3.Distance(target.position, Shoulders) < strikeRange * reach;
            }

            switch (state)
            {
                case State.Idle:
                case State.Stalk:
                    readiness = Mathf.Clamp01(readiness + (inZone ? dt / buildUp : -dt / 0.8f));
                    state = readiness > 0.02f ? State.Stalk : State.Idle;
                    if (state == State.Stalk && readiness >= 1f && inStrike && now >= cooldownEnd) BeginStrike(false);
                    break;
                case State.Strike:
                    if (now - strikeStart >= StrikeLength())
                    {
                        // Still there after a bat? Often a second one with the other paw.
                        if (kind != Kind.Rear && !secondBat && inStrike && Random.value < 0.6f) BeginStrike(true);
                        else
                        {
                            state = State.Recover;
                            cooldownEnd = now + cooldown;
                            readiness = 0.4f;
                            if (Random.value < 0.3f) ignoreUntil = now + Random.Range(1.5f, 3f);   // "I didn't want it anyway"
                        }
                    }
                    break;
                case State.Recover:
                    if (now >= cooldownEnd) state = readiness > 0.02f ? State.Stalk : State.Idle;
                    break;
            }

            float k = 1f - Mathf.Exp(-4f * dt);
            bool busy = state != State.Idle;
            watch = Mathf.Lerp(watch, target != null || busy ? 1f : 0f, k);
            float wantCrouch = state == State.Stalk ? readiness : state == State.Strike && kind != Kind.Rear ? 0.35f : 0f;
            crouch = Mathf.Lerp(crouch, wantCrouch, 1f - Mathf.Exp(-6f * dt));
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, 1f - watch);
                mixer.SetInputWeight(1, watch);
            }
        }

        void BeginStrike(bool second)
        {
            state = State.Strike;
            strikeStart = Time.time;
            secondBat = second;
            var local = transform.InverseTransformPoint(target.position);
            float reach = Mathf.Max(0.01f, legLength);
            float above = target.position.y - Shoulders.y;
            if (!second && above > 0.8f * reach && Mathf.Abs(local.x) < 1.8f * reach && Random.value < 0.7f) kind = Kind.Rear;
            else if (second) kind = kind == Kind.BatLeft ? Kind.BatRight : Kind.BatLeft;
            else kind = local.x < 0f ? Kind.BatLeft : Kind.BatRight;
            strikePoint = target.position;
        }

        float StrikeLength() => kind == Kind.Rear ? Rise + Grab + RearHold + Down : (secondBat ? WindUp * 0.5f : WindUp) + Lash + Hold + Back;

        void ScanForPlanes()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                planes = FindObjectsByType<PlaneController>(FindObjectsSortMode.None);
            }
            target = null;
            if (head == null || Time.time < ignoreUntil) return;
            float best = watchRange * watchRange;
            foreach (var p in planes)
            {
                if (p == null || !p.isActiveAndEnabled) continue;
                var d = p.transform.position - head.position;
                if (Vector3.Dot(d, transform.forward) < -0.3f * d.magnitude) continue;   // she can't turn round on the spot
                float sq = d.sqrMagnitude;
                if (sq < best) { best = sq; target = p.transform; }
            }
        }

        // ------------------------------------------------------------------ posing

        void LateUpdate()
        {
            foreach (var b in all) b.Reset();
            float t = Time.time;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;

            // Where the animation put her front paws (and how the elbows bend), before the body moves.
            var plantL = pawL != null ? pawL.position : Vector3.zero;
            var plantR = pawR != null ? pawR.position : Vector3.zero;
            var plantRotL = pawL != null ? pawL.rotation : Quaternion.identity;
            var plantRotR = pawR != null ? pawR.rotation : Quaternion.identity;
            var poleL = Pole(upperL, lowerL, pawL);
            var poleR = Pole(upperR, lowerR, pawR);
            if (upperL != null && lowerL != null && pawL != null)
                legLength = Vector3.Distance(upperL.position, lowerL.position) + Vector3.Distance(lowerL.position, pawL.position);
            float reach = Mathf.Max(0.01f, legLength);

            // ---- Strike timeline ----
            float st = t - strikeStart;
            float wind = secondBat ? WindUp * 0.5f : WindUp;
            float rearAmount = 0f, slamOn = 0f;
            Vector3? pawTargetL = null, pawTargetR = null;
            if (state == State.Strike)
            {
                var shoulder = Shoulders;
                var aim = ClampReach(shoulder, strikePoint, reach, up, fwd);
                if (kind == Kind.Rear)
                {
                    rearAmount = st < Rise ? Ease(st / Rise) : st < Rise + Grab + RearHold ? 1f : 1f - Ease((st - Rise - Grab - RearHold) / Down);
                    Vector3 high = shoulder + up * 0.6f * reach + fwd * 0.3f * reach;
                    Vector3 Path(Vector3 plant, float side)
                    {
                        var cocked = high + right * side * 0.18f * reach;
                        var grab = aim + right * side * 0.15f * reach;
                        if (st < Rise) return Vector3.Lerp(plant, cocked, Ease(st / Rise));
                        if (st < Rise + Grab) return Vector3.Lerp(cocked, grab, EaseOut((st - Rise) / Grab));
                        if (st < Rise + Grab + RearHold) return grab;
                        return Vector3.Lerp(grab, plant, Ease((st - Rise - Grab - RearHold) / Down));
                    }
                    pawTargetL = Path(plantL, -1f);
                    pawTargetR = Path(plantR, 1f);
                    slamOn = st >= Rise && st < Rise + Grab + RearHold ? 1f : 0f;
                }
                else
                {
                    bool left = kind == Kind.BatLeft;
                    var plant = left ? plantL : plantR;
                    float side = left ? -1f : 1f;
                    var cocked = shoulder + up * 0.45f * reach + fwd * 0.35f * reach + right * side * 0.15f * reach;
                    Vector3 p;
                    if (st < wind) p = Vector3.Lerp(plant, cocked, Ease(st / wind));
                    else if (st < wind + Lash) p = Vector3.Lerp(cocked, aim, EaseOut((st - wind) / Lash));
                    else if (st < wind + Lash + Hold) p = aim;
                    else p = Vector3.Lerp(aim, plant, Ease((st - wind - Lash - Hold) / Back));
                    if (left) pawTargetL = p; else pawTargetR = p;
                    slamOn = st >= wind && st < wind + Lash + Hold ? 1f : 0f;
                }
                if (slam != null && slamOn > 0f)
                    slam.transform.SetPositionAndRotation(aim + up * slam.range * 0.6f, Quaternion.LookRotation(-up, fwd));
            }
            rear = rearAmount;
            if (slam != null) slam.targetStrength = slamOn;

            // ---- Body: breathing, the crouch (front lowers) or the rear-up, shared along the spine ----
            float breathe = Mathf.Sin(t * Mathf.PI * 2f / 3.2f) * 1.0f;
            float bend = 14f * crouch - 36f * rear;   // turning about her right tips her front DOWN: positive = crouch
            for (int i = 0; i < bSpine.Length; i++)
            {
                float share = bSpine.Length > 1 ? Mathf.Lerp(0.4f, 0.25f, i / (float)(bSpine.Length - 1)) : 1f;   // more at the base
                bSpine[i].Turn(Quaternion.AngleAxis(Mathf.Clamp(bend * share * 1.3f, -MaxSpineBend, MaxSpineBend), right));
            }
            bChest.Turn(Quaternion.AngleAxis(breathe, right));
            float wiggle = state == State.Stalk ? Mathf.InverseLerp(0.6f, 1f, readiness) : 0f;
            bHips.Turn(Quaternion.AngleAxis(Mathf.Sin(t * 15f) * 5f * wiggle, up));

            // ---- Her face turns to the plane (or where she's striking), head kept upright ----
            float wantYaw = 0f, wantPitch = 0f;
            Vector3? lookAt = state == State.Strike ? strikePoint : target != null ? target.position : (Vector3?)null;
            if (lookAt.HasValue && head != null)
            {
                var d = lookAt.Value - head.position;
                var flat = Vector3.ProjectOnPlane(d, up);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    wantYaw = Mathf.Clamp(Vector3.SignedAngle(fwd, flat, up), -maxYaw, maxYaw);
                    wantPitch = Mathf.Clamp(Vector3.SignedAngle(flat, d, Vector3.Cross(flat, up)), -maxPitch, maxPitch);   // positive = above her
                }
            }
            float lookSpeed = state == State.Strike ? 0.08f : 0.18f;
            yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVel, lookSpeed);
            pitch = Mathf.SmoothDampAngle(pitch, wantPitch, ref pitchVel, lookSpeed * 1.2f);
            if (head != null && watch > 0.001f)
            {
                var wantDir = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(-pitch, right) * fwd;
                var desired = Quaternion.LookRotation(wantDir, up) * Quaternion.Inverse(Quaternion.LookRotation(headFace, headUp));
                bNeck.Turn(Quaternion.Slerp(Quaternion.identity, desired * Quaternion.Inverse(head.rotation), 0.4f * watch));
                bHead.Turn(Quaternion.Slerp(Quaternion.identity, desired * Quaternion.Inverse(head.rotation), watch));
            }

            // ---- Front legs: planted unless striking; the IK keeps their lengths and elbow direction ----
            // A sitting cat's front legs are nearly straight, so the pose alone says little about which way
            // an elbow bends: lean every pole backward (a cat's elbows point back) so an elbow can never flip.
            poleL += -fwd * 0.25f * reach;
            poleR += -fwd * 0.25f * reach;
            float plantWeight = watch;   // while grooming, the clip's own paw moves stand
            Leg(bUpperL, bLowerL, bPawL, pawTargetL ?? plantL, poleL, pawTargetL.HasValue ? 1f : plantWeight, pawTargetL.HasValue ? (Quaternion?)null : plantRotL, pawTargetL.HasValue ? 0.97f : 0.999f);
            Leg(bUpperR, bLowerR, bPawR, pawTargetR ?? plantR, poleR, pawTargetR.HasValue ? 1f : plantWeight, pawTargetR.HasValue ? (Quaternion?)null : plantRotR, pawTargetR.HasValue ? 0.97f : 0.999f);

            // ---- Eyes: blinks; wider while hunting ----
            if (t >= nextBlink)
            {
                blinkStart = t;
                blinkLength = watch > 0.5f ? 0.16f : Random.Range(0.45f, 0.8f);
                nextBlink = t + (state == State.Stalk ? Random.Range(4f, 7f) : watch > 0.5f ? Random.Range(2f, 4.5f) : Random.Range(3f, 7f));
            }
            float bt = (t - blinkStart) / Mathf.Max(0.01f, blinkLength);
            float blink = bt >= 0f && bt <= 1f ? Mathf.Sin(bt * Mathf.PI) : 0f;
            float hunting = state == State.Stalk || state == State.Strike ? 1f : 0f;
            float lid = Mathf.Lerp(blink, -0.15f, hunting * (1f - blink));   // negative = wider than open
            if (lidL != null) bLidL.Write(Quaternion.SlerpUnclamped(lidOpenL, lidClosedL, lid));
            if (lidR != null) bLidR.Write(Quaternion.SlerpUnclamped(lidOpenR, lidClosedR, lid));

            // ---- Ears: a twitch now and then (one ear at a time); still and forward while hunting ----
            if (t >= nextTwitch) { earTwitch = 1f; nextTwitch = t + Random.Range(2.5f, 7f); }
            earTwitch = Mathf.MoveTowards(earTwitch, 0f, Time.deltaTime * 5f);
            float twitch = earTwitch * 0.5f * (1f - hunting);
            if (earL != null) bEarL.Write(Quaternion.Slerp(bEarL.Base(), earBackL, twitch));

            // ---- Tail: a lazy sway; hunting, the base goes still and the tip twitches ----
            float amp = Mathf.Lerp(5f + 3f * watch, 9f, hunting);
            float freq = Mathf.Lerp(0.35f, 2.4f, hunting);
            for (int i = 0; i < bTail.Length; i++)
            {
                float along = bTail.Length > 1 ? i / (float)(bTail.Length - 1) : 1f;
                float weight = Mathf.Lerp(0.4f + 0.6f * along, along * along, hunting);
                bTail[i].Turn(Quaternion.AngleAxis(amp * weight * Mathf.Sin(t * freq * Mathf.PI * 2f - i * 0.6f), up));
            }
        }

        /// <summary>The way an elbow bends, from its pose now: from the shoulder-to-paw line out to the elbow.</summary>
        static Vector3 Pole(Transform upper, Transform lower, Transform paw)
        {
            if (upper == null || lower == null || paw == null) return Vector3.back;
            return lower.position - (upper.position + paw.position) * 0.5f;
        }

        /// <summary>Keeps a strike inside her reach, in front of her and off the floor.</summary>
        Vector3 ClampReach(Vector3 shoulder, Vector3 point, float reach, Vector3 up, Vector3 fwd)
        {
            var d = point - shoulder;
            if (d.magnitude > 0.95f * reach) d = d.normalized * 0.95f * reach;
            float ahead = Vector3.Dot(d, fwd);
            if (ahead < 0.15f * reach) d += fwd * (0.15f * reach - ahead);
            var p = shoulder + d;
            float floor = transform.position.y + 0.15f * reach;
            if (p.y < floor) p.y = floor;
            return p;
        }

        /// <summary>
        /// Two-bone IK: turns the upper arm, then the forearm, so the paw reaches the target. The elbow
        /// stays on the side it already bends to (pole), a strike stops short of a locked-straight arm
        /// (maxReach), and a planted paw keeps its flat-on-the-floor rotation.
        /// </summary>
        void Leg(Bone upper, Bone lower, Bone paw, Vector3 target, Vector3 pole, float weight, Quaternion? pawWorld, float maxReach)
        {
            if (upper.t == null || lower.t == null || paw.t == null || weight <= 0.001f) return;
            var a = upper.t.position;
            var b = lower.t.position;
            var c = paw.t.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            var toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.1f * (l1 + l2), maxReach * (l1 + l2));
            var dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (c - a).normalized;
            var bendDir = Vector3.ProjectOnPlane(pole, dir);
            if (bendDir.sqrMagnitude < 1e-8f) bendDir = Vector3.ProjectOnPlane(-transform.forward, dir);
            bendDir.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            var elbow = a + (dir * cosA + bendDir * Mathf.Sqrt(1f - cosA * cosA)) * l1;
            var end = a + dir * d;
            elbow = Vector3.Lerp(b, elbow, weight);
            end = Vector3.Lerp(c, end, weight);

            upper.Turn(Quaternion.FromToRotation(b - a, elbow - a));
            b = lower.t.position;
            c = paw.t.position;
            lower.Turn(Quaternion.FromToRotation(c - b, end - b));
            if (pawWorld.HasValue) paw.SetWorld(Quaternion.Slerp(paw.t.rotation, pawWorld.Value, weight));
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); }
    }
}
