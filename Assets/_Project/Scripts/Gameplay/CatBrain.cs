using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Biscuit the cat (Tiny War): neutral, never hurt, a hazard to anything that flies too low, any side.
    /// Stage 45 turns her from a cat on one spot into a cat that lives in the room.
    ///
    /// HER LIFE (this file, the mind). She's a playful hunter and a lazy loaf:
    ///  - She wanders between the places she knows (CatSpots): the floor in front of the counter, under
    ///    the window, by the fridge, a chair, the counter top, the warm windowsill. Linked spots at
    ///    different heights are JUMPS: she walks up, gathers, leaps and lands.
    ///  - Awake, she sits and grooms, sits and watches, loafs, stretches and yawns, looks around.
    ///  - Planes flying low get her attention: she stalks them across the floor (low crouch, slow
    ///    careful steps, tail low with a twitching tip), gathers with a butt wiggle and POUNCES, paws
    ///    out. Sitting, she bats at anything in reach (one paw, a double bat, or a rear-up grab). Planes
    ///    out of reach overhead get watched and chattered at.
    ///  - Hunting tires her. When she's tired she goes to a nap spot, kneads it, circles, lies down and
    ///    SLEEPS. Asleep she's harmless, but planes buzzing near wake her in stages: an ear flicks, an eye
    ///    opens, her head comes up. Keep pestering her and she gets up, wide awake and keen.
    ///  - Things she's scared of (the robot vacuum registers itself with <see cref="Scare"/>) send her
    ///    trotting to the highest perch away from them, ears flat, back arched.
    ///
    /// HER BODY (CatBrain.Body.cs): the model has only one animation (sitting and grooming), so
    /// standing, walking, trotting, stalking, jumping, lying and stretching are all posed here on her
    /// rig, every frame, with leg IK, planted feet and a gait. See that file.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public partial class CatBrain : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Her idle loop (sitting, grooming).")]
        public AnimationClip idleClip;
        [Tooltip("Seconds into the idle clip where she sits still with her paws down: the pose she holds while watching.")]
        public float restTime;

        [Header("Bones (filled by Stage 44 / 45)")]
        public Transform hips;
        public Transform chest;
        [Tooltip("Lower, middle and upper spine.")]
        public Transform[] spine;
        public Transform neck;
        public Transform head;
        [Tooltip("Not moved (the head turn carries them).")]
        public Transform eyeL, eyeR;
        public Transform lidL, lidR;
        public Transform earL, earR;
        [Tooltip("Front legs: upper arm, forearm (elbow) and wrist, left and right.")]
        public Transform upperL, lowerL, pawL, upperR, lowerR, pawR;
        public Transform[] tail;
        [Tooltip("Stage 45: the whole body (parent of the spine and the hips).")]
        public Transform body;
        [Tooltip("Stage 45: the top of the spine (the shoulders and the neck hang off it).")]
        public Transform spineTop;
        public Transform neckBase, jaw;
        [Tooltip("Stage 45: front paws (palm, toes).")]
        public Transform palmL, fingerL, palmR, fingerR;
        [Tooltip("Stage 45: hind legs: thigh, knee, ankle (hock), ball of the foot, toes.")]
        public Transform femurL, kneeL, ankleL, ballL, toeL, femurR, kneeR, ankleR, ballR, toeR;

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
        [Tooltip("Sitting: stalking (crouching to bat) starts when a plane is this many front-leg lengths away and low.")]
        public float stalkRange = 10f;
        [Tooltip("Sitting: she bats when a plane is within this many front-leg lengths of her shoulders.")]
        public float strikeRange = 3f;
        [Tooltip("Seconds of crouching before she's ready to bat.")]
        public float buildUp = 1.1f;
        [Tooltip("Seconds between strikes.")]
        public float cooldown = 1.3f;
        public float maxYaw = 75f, maxPitch = 40f;
        [Tooltip("Stage 45: on the floor she goes after planes lower than this above her (and pounces under about two thirds of it).")]
        public float huntCeiling = 34f;
        [Tooltip("Stage 45: how far (horizontally) she'll go after a low plane.")]
        public float huntRange = 140f;

        [Header("Her room (Stage 45)")]
        [Tooltip("The places she knows. Empty = she stays where she was put and only sits.")]
        public CatSpot[] spots = new CatSpot[0];
        [Tooltip("Boxes she never walks into on the floor (table legs, the fort, the fridge...), in world space.")]
        public Bounds[] keepOut = new Bounds[0];
        [Tooltip("Start each mode asleep on a nap spot (else awake where she stands).")]
        public bool startAsleep = true;
        [Tooltip("Testing only: she treats this object as a plane to hunt (leave empty).")]
        public Transform debugTarget;

        [Header("Personality")]
        [Tooltip("Roughly how many seconds of being awake before she wants a nap.")]
        public float awakeFor = 150f;
        [Tooltip("Roughly how many seconds a nap lasts if nobody bothers her.")]
        public float napFor = 60f;
        [Tooltip("Walking speed (units per second; she's about 12 long).")]
        public float walkSpeed = 16f;
        public float trotSpeed = 38f;
        public float stalkSpeed = 6f;

        // ------------------------------------------------------------------ what other code reads

        /// <summary>Short words for the HUD: "asleep", "hunting", ...</summary>
        public string Mood { get; private set; } = "watching";
        public bool Asleep => act == Act.Nap && sleeping;
        public static readonly List<CatBrain> All = new List<CatBrain>();

        static readonly List<Transform> scares = new List<Transform>();
        /// <summary>Something she's frightened of (the robot vacuum). Unregister when it stops.</summary>
        public static void Scare(Transform t, bool on) { if (on) { if (!scares.Contains(t)) scares.Add(t); } else scares.Remove(t); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); scares.Clear(); }

        // ------------------------------------------------------------------ the mind

        enum Act { Idle, Travel, Hunt, Nap, Flee }
        Act act = Act.Idle;
        Coroutine running;
        bool sleeping;
        float energy = 1f;            // 1 rested .. 0 exhausted
        float disturbance;            // while asleep: how bothered she is
        float lastPlaneSeen = -99f;
        CatSpot at;                   // the spot she's on (null when off the map, e.g. stalking)
        float surfaceY;               // the height her feet stand on
        Vector3 startPos;
        Quaternion startRot;

        PlaneController[] planes = new PlaneController[0];
        float nextScan;
        Transform target;             // the plane she's interested in
        Vector3 targetVel;
        Vector3 lastTargetPos;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            startPos = transform.position;
            startRot = transform.rotation;
            BuildBones();
            nextBlink = Time.time + Random.Range(1.5f, 4f);
            nextTwitch = Time.time + Random.Range(2f, 6f);
            if (slam != null) { slam.targetStrength = 0f; slam.ramp = 15f; }
        }

        void OnEnable()
        {
            All.Add(this);
            StartGraph();
            Restart();
        }

        void OnDisable()
        {
            All.Remove(this);
            if (graph.IsValid()) graph.Destroy();
            if (slam != null) slam.targetStrength = 0f;
            StopAllCoroutines();
            running = null;
        }

        /// <summary>Back to her start: asleep on a nap spot (or awake where she was put). Called when the room's mode starts.</summary>
        public void Restart(bool awake = false)
        {
            StopAllCoroutines();
            running = null;
            energy = Random.Range(0.75f, 1f);
            disturbance = 0f;
            CatSpot nap = startAsleep && !awake ? PickNapSpot() : null;
            if (nap != null)
            {
                Place(nap.Position, nap.Facing);
                at = nap;
                energy = 0.1f;
                SetPose(sit: 0f, lie: 1f, snap: true);
                sleepyGoal = 1f; sleepy = 1f;
                sleeping = true;
                act = Act.Nap;
                running = StartCoroutine(Sleep(Random.Range(0.5f, 1f) * napFor));
            }
            else
            {
                Place(startPos, startRot * Vector3.forward);
                at = Nearest(transform.position, sameHeight: true);
                SetPose(sit: 1f, lie: 0f, snap: true);
                Begin(Act.Idle, Idle());
            }
        }

        /// <summary>
        /// Dev and test hook: "wake", "nap", "sit", "lie", "stand", "stretch", "spot:NAME" (go there), "hunt".
        /// Used by the dev panel and by test scripts; the game never calls it.
        /// </summary>
        public string DebugDo(string what)
        {
            if (what.StartsWith("spot:"))
            {
                var name = what.Substring(5);
                foreach (var s in spots) if (s != null && s.name == name)
                {
                    Begin(Act.Travel, Seq(Travel(s, walkSpeed), Settle(s)));
                    return "going to " + name;
                }
                return "no spot " + name;
            }
            switch (what)
            {
                case "wake": energy = 1f; Begin(Act.Idle, Seq(StandUp(), Stretch())); break;
                case "nap": energy = 0.1f; Begin(Act.Nap, Nap()); break;
                case "sit": Begin(Act.Idle, Seq(SitDown(), Look(30f))); break;
                case "lie": Begin(Act.Idle, Seq(LieDown(), Look(30f))); break;
                case "stand": Begin(Act.Idle, Seq(StandUp(), Look(30f))); break;
                case "stretch": Begin(Act.Idle, Seq(StandUp(), Stretch(), Look(30f))); break;
                case "hunt": energy = 1f; Begin(Act.Hunt, Hunt()); break;
                default: return "unknown: " + what;
            }
            return what;
        }

        static IEnumerator Seq(params IEnumerator[] steps) { foreach (var s in steps) yield return s; }

        /// <summary>For tests and the dev panel: what she's doing and her pose blend.</summary>
        public string DebugState => $"{act} mood={Mood} sit={sitW:0.00} lie={lieW:0.00} sleepy={sleepy:0.00} energy={energy:0.00} at={(at != null ? at.name : "-")} speed={new Vector2(velocity.x, velocity.z).magnitude:0.0}";

        void Place(Vector3 pos, Vector3 facing)
        {
            transform.position = pos;
            if (facing.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            surfaceY = pos.y;
            lastRootPos = pos;
            feetValid = false;
        }

        void Begin(Act a, IEnumerator routine)
        {
            if (running != null) StopCoroutine(running);
            act = a;
            sleeping = false;
            knead = false;
            chatter = false;
            arch = 0f;
            pounceStrike = false;
            moveSpeed = 0f;
            running = StartCoroutine(Run(routine));
        }

        IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            running = null;
            act = Act.Idle;
            Begin(Act.Idle, Idle());
        }

        void Update()
        {
            ScanForPlanes();
            float dt = Time.deltaTime, now = Time.time;

            // Energy: awake costs, sleep restores.
            if (act == Act.Nap && sleeping) energy = Mathf.Min(1f, energy + dt / Mathf.Max(5f, napFor));
            else energy = Mathf.Max(0f, energy - dt / Mathf.Max(10f, awakeFor) * (act == Act.Hunt ? 1.6f : 1f));

            if (target != null) lastPlaneSeen = now;

            // Interrupts, most urgent first (never in mid-air: a jump always finishes).
            var scare = NearestScare(out float scareDist);
            if (airborne || pushOff) { }
            else if (scare != null && scareDist < 70f && act != Act.Flee) { Begin(Act.Flee, Flee(scare)); }
            else if (act == Act.Nap && sleeping) Disturb(dt);
            else if (act != Act.Flee && act != Act.Hunt && act != Act.Nap && WantsToHunt()) Begin(Act.Hunt, Hunt());

            UpdateSeatedSwat(dt);
            Mood = MoodWords();
        }

        string MoodWords()
        {
            switch (act)
            {
                case Act.Nap: return sleeping ? (disturbance > 0.5f ? "stirring" : disturbance > 0.2f ? "dozing" : "asleep") : "sleepy";
                case Act.Hunt: return pouncing ? "POUNCING" : stalking ? "stalking" : "hunting";
                case Act.Flee: return "spooked";
                case Act.Travel: return "wandering";
            }
            return target != null ? "watching" : "relaxed";
        }

        // ------------------------------------------------------------------ planes

        void ScanForPlanes()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.4f;
                planes = FindObjectsByType<PlaneController>(FindObjectsSortMode.None);
            }
            var old = target;
            target = null;
            if (debugTarget != null && debugTarget.gameObject.activeInHierarchy)
            {
                // Test hook: chase this instead of planes.
                target = debugTarget;
                targetVel = target == old && Time.deltaTime > 0f ? Vector3.Lerp(targetVel, (target.position - lastTargetPos) / Time.deltaTime, 0.3f) : Vector3.zero;
                lastTargetPos = target.position;
                return;
            }
            if (head == null || Time.time < ignoreUntil) return;
            float best = watchRange * watchRange;
            bool standing = sitW < 0.5f;
            foreach (var p in planes)
            {
                if (p == null || !p.isActiveAndEnabled || p.State != PlaneState.Flying) continue;
                var d = p.transform.position - head.position;
                if (!standing && Vector3.Dot(d, transform.forward) < -0.3f * d.magnitude) continue;   // sitting, she can't see behind her
                float sq = d.sqrMagnitude;
                if (sq < best) { best = sq; target = p.transform; }
            }
            if (target != null)
            {
                if (target == old && Time.deltaTime > 0f) targetVel = Vector3.Lerp(targetVel, (target.position - lastTargetPos) / Time.deltaTime, 0.3f);
                else targetVel = Vector3.zero;
                lastTargetPos = target.position;
            }
        }

        float HeightAboveMe(Vector3 p) => p.y - surfaceY;
        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        bool WantsToHunt()
        {
            if (target == null || energy < 0.12f) return false;
            float h = HeightAboveMe(target.position);
            float d = Flat(target.position, transform.position);
            if (at != null && at.IsHigh) return false;   // on a perch she bats from where she sits (the seated swat)
            return h < huntCeiling && d < huntRange;
        }

        CatSpot.Kind Surface => at != null ? at.kind : CatSpot.Kind.Floor;

        // ------------------------------------------------------------------ sleeping and waking

        void Disturb(float dt)
        {
            float push = 0f;
            foreach (var p in planes)
            {
                if (p == null || !p.isActiveAndEnabled || p.State != PlaneState.Flying) continue;
                float d = Vector3.Distance(p.transform.position, head != null ? head.position : transform.position);
                if (d > 80f) continue;
                float close = 1f - d / 80f;
                push += close * close * (0.35f + 0.02f * p.Velocity.magnitude);
            }
            disturbance = Mathf.Clamp(disturbance + (push - 0.08f) * dt, 0f, 1.05f);
            if (disturbance >= 1f)
            {
                // Pestered awake: up and keen.
                energy = Mathf.Max(energy, 0.85f);
                Begin(Act.Hunt, WakeAndHunt());
            }
        }

        IEnumerator WakeAndHunt()
        {
            sleepyGoal = 0f;
            yield return new WaitForSeconds(0.4f);
            yield return StandUp();
            yield return Hunt();
        }

        IEnumerator Sleep(float seconds)
        {
            sleeping = true;
            sleepyGoal = 1f;
            float end = Time.time + seconds;
            while (Time.time < end || energy < 0.9f)
            {
                // The stages of being disturbed: an ear, an eye, her head.
                earAlert = Mathf.Clamp01(disturbance / 0.25f);
                eyeOpen = Mathf.Clamp01((disturbance - 0.25f) / 0.25f);
                sleepyGoal = disturbance > 0.75f ? 0.15f : disturbance > 0.5f ? 0.55f : 1f;
                yield return null;
            }
            sleeping = false;
            earAlert = 0f; eyeOpen = 0f;
            // Waking up on her own: head up, yawn, up, stretch.
            sleepyGoal = 0f;
            yield return new WaitForSeconds(1.2f);
            yield return Yawn();
            yield return StandUp();
            yield return Stretch();
        }

        // ------------------------------------------------------------------ idle life

        IEnumerator Idle()
        {
            Mood = "relaxed";
            while (true)
            {
                if (energy < 0.25f) { yield return Nap(); continue; }

                float r = Random.value;
                if (spots.Length > 1 && r < 0.38f)
                {
                    var next = PickWanderSpot();
                    if (next != null)
                    {
                        act = Act.Travel;
                        yield return Travel(next, walkSpeed);
                        act = Act.Idle;
                        yield return Settle(next);
                        continue;
                    }
                }
                if (r < 0.62f)
                {
                    // Sit and groom (the clip), or sit and watch the room.
                    yield return SitDown();
                    groomGoal = Random.value < 0.6f ? 1f : 0f;
                    yield return Look(Random.Range(4f, 9f));
                    groomGoal = 0f;
                }
                else if (r < 0.8f)
                {
                    // A loaf: lie down, paws tucked, watching.
                    yield return LieDown();
                    yield return Look(Random.Range(6f, 12f));
                }
                else if (r < 0.9f)
                {
                    yield return StandUp();
                    yield return Stretch();
                }
                else
                {
                    yield return StandUp();
                    yield return Look(Random.Range(2f, 4f));
                }
            }
        }

        /// <summary>Holds her pose and lets her head wander: random glances, planes if there are any.</summary>
        IEnumerator Look(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                if (Random.value < Time.deltaTime * 0.4f) glance = new Vector2(Random.Range(-50f, 50f), Random.Range(-15f, 20f));
                if (Random.value < Time.deltaTime * 0.15f && target == null) yield return SlowBlink();
                yield return null;
            }
            glance = Vector2.zero;
        }

        IEnumerator SlowBlink()
        {
            slowBlink = true;
            yield return new WaitForSeconds(1.4f);
            slowBlink = false;
        }

        /// <summary>At a spot: face its way and do what it's for.</summary>
        IEnumerator Settle(CatSpot s)
        {
            if (s == null) yield break;
            yield return TurnTo(s.Facing);
            if (s.napSpot && energy < 0.5f) { yield return Nap(); yield break; }
            yield return SitDown();
            groomGoal = Random.value < 0.5f ? 1f : 0f;
            yield return Look(Random.Range(4f, 10f));
            groomGoal = 0f;
        }

        IEnumerator Nap()
        {
            var spot = PickNapSpot();
            if (spot != null && spot != at)
            {
                act = Act.Travel;
                yield return Travel(spot, walkSpeed * 0.8f);
            }
            act = Act.Nap;
            Mood = "sleepy";
            // Knead, turn round once, lie down facing her way.
            yield return StandUp();
            yield return TurnTo(-(spot != null ? spot.Facing : transform.forward));
            knead = true;
            yield return new WaitForSeconds(Random.Range(1.4f, 2.4f));
            knead = false;
            yield return TurnTo(spot != null ? spot.Facing : -transform.forward);
            yield return LieDown();
            yield return new WaitForSeconds(1.5f);
            yield return Sleep(Random.Range(0.7f, 1.3f) * napFor);
        }

        IEnumerator Yawn()
        {
            yawnGoal = 1f;
            yield return new WaitForSeconds(1.6f);
            yawnGoal = 0f;
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator Stretch()
        {
            stretchGoal = 1f;
            yield return new WaitForSeconds(1.0f);
            yawnGoal = 1f;
            yield return new WaitForSeconds(1.4f);
            yawnGoal = 0f;
            stretchGoal = 0f;
            yield return new WaitForSeconds(0.8f);
        }

        // ------------------------------------------------------------------ hunting

        bool stalking, pouncing, pounceStrike;

        IEnumerator Hunt()
        {
            int pounces = 0;
            while (true)
            {
                if (target == null)
                {
                    if (Time.time - lastPlaneSeen > 2.5f) break;
                    yield return null;
                    continue;
                }
                var p = target.position;
                float h = HeightAboveMe(p);
                float d = Flat(p, transform.position);
                stalking = false;

                if (at != null && at.IsHigh) break;           // perched: the seated swat does the work
                if (d > huntRange * 1.15f || energy < 0.08f) break;

                if (h > huntCeiling)
                {
                    // Out of reach overhead: sit, watch, chatter.
                    yield return SitDown();
                    chatter = h < huntCeiling * 2.2f;
                    float until = Time.time + 1.5f;
                    while (Time.time < until && target != null && HeightAboveMe(target.position) > huntCeiling) yield return null;
                    chatter = false;
                    continue;
                }

                // Low enough to catch. Close it down, low and slow (or trot if it's getting away).
                yield return StandUp();
                at = null;
                Vector3 aim = Predict(0.6f);
                float pounceRange = 34f;
                if (d > pounceRange)
                {
                    stalking = d < 90f;
                    crouchGoal = stalking ? 1f : 0f;
                    gait = stalking ? Gait.Stalk : Gait.Trot;
                    float speed = stalking ? stalkSpeed : trotSpeed;
                    float until = Time.time + 0.4f;
                    while (Time.time < until) { MoveToward(aim, speed, pounceRange * 0.8f); yield return null; }
                    continue;
                }

                // In range: face it, gather (wiggle), pounce.
                crouchGoal = 1f;
                gait = Gait.Stalk;
                moveSpeed = 0f;
                yield return TurnTo(aim - transform.position, 240f);
                wiggle = true;
                float gather = Random.Range(0.5f, 1.1f);
                float t0 = Time.time;
                while (Time.time - t0 < gather && target != null) { FaceToward(Predict(0.6f), 200f); yield return null; }
                wiggle = false;
                if (target == null) continue;
                h = HeightAboveMe(target.position);
                if (h > huntCeiling * 0.75f) { crouchGoal = 0f; continue; }   // it climbed away
                yield return Pounce(Predict(0.55f));
                pounces++;
                energy -= 0.06f;
                crouchGoal = 0f;
                // After a pounce: often sit and pretend she didn't care, sometimes have another go.
                if (Random.value < 0.45f || pounces >= 3)
                {
                    yield return SitDown();
                    groomGoal = Random.value < 0.6f ? 1f : 0f;
                    ignoreUntil = Time.time + Random.Range(1.5f, 3f);
                    yield return Look(Random.Range(1.5f, 3f));
                    groomGoal = 0f;
                    if (pounces >= 3) break;
                }
            }
            stalking = false;
            crouchGoal = 0f;
            at = Nearest(transform.position, sameHeight: true);
        }

        Vector3 Predict(float seconds)
        {
            if (target == null) return transform.position + transform.forward * 10f;
            var p = target.position + targetVel * seconds;
            return p;
        }

        IEnumerator Pounce(Vector3 at3)
        {
            pouncing = true;
            Vector3 from = transform.position;
            Vector3 flat = at3 - from; flat.y = 0f;
            float dist = Mathf.Clamp(flat.magnitude, 8f, 40f);
            Vector3 dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : transform.forward;
            Vector3 land = KeepOut(from + dir * dist);
            float peak = Mathf.Clamp(HeightAboveMe(at3) * 0.8f, 5f, huntCeiling * 0.6f);
            pounceStrike = true;
            yield return Leap(land, surfaceY, peak, strike: true);
            pounceStrike = false;
            pouncing = false;
        }

        void MoveToward(Vector3 goal, float speed, float stopAt)
        {
            Vector3 d = goal - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < stopAt) { moveSpeed = 0f; return; }
            FaceToward(goal, speed > 20f ? 200f : 120f);
            float ang = Vector3.Angle(transform.forward, d);
            float v = speed * Mathf.Clamp01(1f - (ang - 30f) / 90f);
            moveSpeed = v;
            var next = transform.position + transform.forward * v * Time.deltaTime;
            next = KeepOut(next);
            next.y = surfaceY;
            transform.position = next;
        }

        void FaceToward(Vector3 goal, float degPerSec)
        {
            Vector3 d = goal - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            var want = Quaternion.LookRotation(d, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSec * Time.deltaTime);
        }

        /// <summary>Pushes a floor position out of the boxes she must not walk into, and keeps it inside the room.</summary>
        Vector3 KeepOut(Vector3 p)
        {
            const float margin = 7f;
            if (keepOut == null) return p;
            foreach (var b in keepOut)
            {
                var min = b.min - new Vector3(margin, 0f, margin);
                var max = b.max + new Vector3(margin, 0f, margin);
                if (p.x <= min.x || p.x >= max.x || p.z <= min.z || p.z >= max.z) continue;
                if (p.y > b.max.y || p.y + 10f < b.min.y) continue;   // she's above it (or it's overhead)
                float dl = p.x - min.x, dr = max.x - p.x, db = p.z - min.z, df = max.z - p.z;
                float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, df));
                if (m == dl) p.x = min.x; else if (m == dr) p.x = max.x; else if (m == db) p.z = min.z; else p.z = max.z;
            }
            return p;
        }

        // ------------------------------------------------------------------ fear

        Transform NearestScare(out float dist)
        {
            dist = float.MaxValue;
            Transform best = null;
            foreach (var s in scares)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                float d = Flat(s.position, transform.position);
                if (d < dist) { dist = d; best = s; }
            }
            return best;
        }

        IEnumerator Flee(Transform scare)
        {
            // The highest perch away from it.
            CatSpot best = null;
            float score = float.MinValue;
            foreach (var s in spots)
            {
                if (s == null || !s.IsHigh) continue;
                float sc = s.Position.y * 2f + Flat(s.Position, scare.position);
                if (sc > score) { score = sc; best = s; }
            }
            arch = 1f;
            yield return new WaitForSeconds(0.5f);
            if (best != null && best != at) yield return Travel(best, trotSpeed);
            arch = 0.6f;
            yield return SitDown();
            // Watch it, ears back, until it's been gone a while.
            float calm = 0f;
            while (calm < 10f)
            {
                var s = NearestScare(out float d);
                if (s != null) glanceAt = s.position;
                calm = s == null || d > 160f ? calm + Time.deltaTime : 0f;   // quiet (docked) or far away
                yield return null;
            }
            glanceAt = null;
            arch = 0f;
        }

        // ------------------------------------------------------------------ getting about

        CatSpot PickNapSpot()
        {
            CatSpot best = null;
            float bestScore = float.MinValue;
            foreach (var s in spots)
            {
                if (s == null || !s.napSpot) continue;
                float sc = s.liking + Random.value;
                if (sc > bestScore) { bestScore = sc; best = s; }
            }
            return best;
        }

        CatSpot PickWanderSpot()
        {
            float total = 0f;
            foreach (var s in spots) if (s != null && s != at) total += s.liking;
            float r = Random.value * total;
            foreach (var s in spots)
            {
                if (s == null || s == at) continue;
                r -= s.liking;
                if (r <= 0f) return s;
            }
            return null;
        }

        CatSpot Nearest(Vector3 p, bool sameHeight)
        {
            CatSpot best = null;
            float bd = float.MaxValue;
            foreach (var s in spots)
            {
                if (s == null) continue;
                if (sameHeight && Mathf.Abs(s.Position.y - p.y) > 3f) continue;
                float d = Vector3.Distance(s.Position, p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Breadth-first route over the spots' links.</summary>
        List<CatSpot> Route(CatSpot from, CatSpot to)
        {
            var prev = new Dictionary<CatSpot, CatSpot>();
            var queue = new Queue<CatSpot>();
            queue.Enqueue(from);
            prev[from] = null;
            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                if (s == to) break;
                foreach (var n in Neighbours(s))
                    if (n != null && !prev.ContainsKey(n)) { prev[n] = s; queue.Enqueue(n); }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<CatSpot>();
            for (var s = to; s != null; s = prev[s]) path.Add(s);
            path.Reverse();
            return path;
        }

        IEnumerable<CatSpot> Neighbours(CatSpot s)
        {
            if (s.links != null) foreach (var l in s.links) yield return l;
            foreach (var o in spots)   // links work both ways
                if (o != null && o != s && o.links != null && System.Array.IndexOf(o.links, s) >= 0) yield return o;
        }

        IEnumerator Travel(CatSpot dest, float speed)
        {
            if (dest == null) yield break;
            var from = at != null ? at : Nearest(transform.position, sameHeight: true);
            if (from == null) yield break;
            yield return StandUp();
            gait = speed > 25f ? Gait.Trot : Gait.Walk;
            crouchGoal = 0f;
            // Back onto her map first (after a hunt she can be anywhere on the floor).
            if (Flat(from.Position, transform.position) > 3f && Mathf.Abs(from.Position.y - surfaceY) < 4f) yield return WalkTo(from.Position, speed);
            at = from;
            var path = Route(from, dest);
            if (path == null) yield break;
            for (int i = 1; i < path.Count; i++)
            {
                var next = path[i];
                if (Mathf.Abs(next.Position.y - surfaceY) > 4f) yield return JumpTo(next);
                else yield return WalkTo(next.Position, speed);
                at = next;
            }
            moveSpeed = 0f;
        }

        IEnumerator WalkTo(Vector3 goal, float speed)
        {
            float stuck = 0f;
            while (Flat(goal, transform.position) > 1.5f && stuck < 6f)
            {
                MoveToward(goal, speed, 1.2f);
                stuck += Time.deltaTime;
                yield return null;
            }
            moveSpeed = 0f;
        }

        IEnumerator TurnTo(Vector3 dir, float degPerSec = 150f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) yield break;
            if (Vector3.Angle(transform.forward, dir) < 8f) yield break;
            if (sitW > 0.3f || lieW > 0.3f) yield return StandUp();
            var want = Quaternion.LookRotation(dir, Vector3.up);
            float limit = Time.time + 3f;
            while (Quaternion.Angle(transform.rotation, want) > 2f && Time.time < limit)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSec * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>A jump to a spot at another height: walk up to it, gather, leap, land.</summary>
        IEnumerator JumpTo(CatSpot s)
        {
            Vector3 land = s.Position;
            bool up = land.y > surfaceY;
            Vector3 flat = land - transform.position; flat.y = 0f;
            Vector3 dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : transform.forward;
            // Up: take off a little way back from it. Down: hop off from where she is.
            float takeoff = up ? Mathf.Clamp(flat.magnitude, 14f, 26f) : flat.magnitude;
            if (up && flat.magnitude > takeoff + 1f) yield return WalkTo(land - dir * takeoff, walkSpeed);
            yield return TurnTo(dir, 220f);
            // Gather: she looks up at it, crouches, her hindquarters set.
            glanceAt = land + Vector3.up * 6f;
            crouchGoal = 0.8f;
            moveSpeed = 0f;
            yield return new WaitForSeconds(up ? Random.Range(0.45f, 0.8f) : 0.3f);
            float peak = up ? 7f : 4f;   // above the higher end
            yield return Leap(land, land.y, peak, strike: false);
            glanceAt = null;
            crouchGoal = 0f;
        }

        /// <summary>
        /// The flight of a jump or a pounce: a ballistic arc from here to 'land' (a surface at 'landY'),
        /// 'peak' above the higher end. Her body pitches with the arc and her legs reach and tuck.
        /// </summary>
        IEnumerator Leap(Vector3 land, float landY, float peak, bool strike)
        {
            Vector3 from = transform.position;
            land.y = landY;
            float horiz = Flat(land, from);
            float top = Mathf.Max(from.y, landY) + peak;
            // Flight time from the arc height (gravity scaled for her size so it reads as a cat, not a feather).
            const float g = 260f;
            float tUp = Mathf.Sqrt(2f * Mathf.Max(0.5f, top - from.y) / g);
            float tDown = Mathf.Sqrt(2f * Mathf.Max(0.5f, top - landY) / g);
            float T = Mathf.Max(0.25f, tUp + tDown);
            Vector3 dir = land - from; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // Push-off: the hind legs extend (about a tenth of a second).
            pushOff = true;
            yield return new WaitForSeconds(0.1f);
            pushOff = false;
            airborne = true;
            airStrike = strike;
            float t = 0f;
            while (t < T)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / T);
                Vector3 p = Vector3.Lerp(from, land, u);
                p.y = t < tUp ? from.y + (top - from.y) * (1f - (1f - t / tUp) * (1f - t / tUp))
                              : top - (top - landY) * ((t - tUp) / tDown) * ((t - tUp) / tDown);
                transform.position = p;
                airU = u;
                airVy = t < tUp ? 1f - t / tUp : -(t - tUp) / tDown;   // +1 rising fast .. -1 falling fast
                if (strike && slam != null && u > 0.3f && u < 0.75f)
                {
                    slam.transform.SetPositionAndRotation(p + transform.forward * 8f + Vector3.up * (slam.range * 0.55f), Quaternion.LookRotation(Vector3.down, transform.forward));
                    slam.targetStrength = 1f;
                }
                else if (strike && slam != null) slam.targetStrength = 0f;
                yield return null;
            }
            transform.position = land;
            surfaceY = landY;
            airborne = false;
            airStrike = false;
            if (slam != null && strike) slam.targetStrength = 0f;
            feetValid = false;          // plant her feet where she landed
            landed = Time.time;         // the landing squash
            yield return new WaitForSeconds(0.28f);
        }

        // ------------------------------------------------------------------ posture changes

        IEnumerator SitDown()
        {
            if (sitW > 0.97f) yield break;
            moveSpeed = 0f;
            SetPose(sit: 1f, lie: 0f);
            while (sitW < 0.95f) yield return null;
        }

        IEnumerator StandUp()
        {
            if (sitW < 0.03f && lieW < 0.03f && sleepy < 0.05f) yield break;
            sleepyGoal = 0f;
            groomGoal = 0f;
            SetPose(sit: 0f, lie: 0f);
            while (sitW > 0.05f || lieW > 0.05f) yield return null;
        }

        IEnumerator LieDown()
        {
            moveSpeed = 0f;
            SetPose(sit: 0f, lie: 1f);
            while (lieW < 0.95f) yield return null;
        }
    }
}
