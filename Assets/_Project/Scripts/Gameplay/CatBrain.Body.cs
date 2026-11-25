using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Biscuit's body (Stage 45). Her model has one animation, sitting and grooming, so everything
    /// else is posed here on her rig, every frame:
    ///
    ///  - SITTING is the clip (grooming, or frozen at the watching pose), with the seated swat on top
    ///    (crouch, one-paw bats, the double bat, the rear-up grab) exactly as Stage 44 tuned it.
    ///  - STANDING / WALKING / TROTTING / STALKING: her pelvis and shoulders are placed at a height,
    ///    the spine is bent through them (arched, sagging in a stretch, flexing sideways in a turn), the
    ///    neck carries the head forward, and the legs reach for their feet with IK. Feet are PLANTED on
    ///    the floor and only move in their swing, in a cat's walking order (hind, fore on the same side,
    ///    then the other side) or in diagonal pairs at a trot. Standing still she takes small steps to
    ///    tidy her feet, which is also how she turns on the spot.
    ///  - LYING (a loaf, or asleep with her chin down and her tail wrapped round), STRETCHING (front
    ///    paws out, chest down, rump up, a yawn) and JUMPING (push-off, legs reaching and tucking with
    ///    the arc, a squash on landing) are the same machinery with other targets.
    ///  - Changing between the clip and the posed body blends every bone's rotation, so she really
    ///    stands up and sits down.
    ///
    /// Staying natural: bone lengths never change (everything is rotation, aimed at points), elbows
    /// always bend back and knees forward, the head is kept level, and every change is eased.
    ///
    /// UNITY NOTE: the Animator writes the bones every frame, then LateUpdate runs, so all of this goes
    /// in LateUpdate. Each bone first goes back to its clean pose (Bone.Reset) so nothing builds up.
    /// </summary>
    public partial class CatBrain
    {
        // ------------------------------------------------------------------ bones

        sealed class Bone
        {
            public readonly Transform t;
            Quaternion written, baseRot;
            Vector3 writtenPos, basePos;
            bool has, hasPos;
            public Bone(Transform t) { this.t = t; }
            public bool Ok => t != null;
            public Vector3 Pos => t != null ? t.position : Vector3.zero;
            /// <summary>This frame's pose before our tweaks: the animation's, or our last base if the animation didn't touch it.</summary>
            public Quaternion Base()
            {
                if (t == null) return Quaternion.identity;
                if (!has || t.localRotation != written) baseRot = t.localRotation;
                return baseRot;
            }
            public Vector3 BasePos()
            {
                if (t == null) return Vector3.zero;
                if (!hasPos || t.localPosition != writtenPos) basePos = t.localPosition;
                return basePos;
            }
            public void Write(Quaternion q)
            {
                if (t == null || float.IsNaN(q.x) || float.IsNaN(q.w)) return;   // never let a bad frame into the rig
                t.localRotation = q; written = q; has = true;
            }
            public void WritePos(Vector3 p) { if (t == null || float.IsNaN(p.x)) return; BasePos(); t.localPosition = p; writtenPos = p; hasPos = true; }
            public void WriteWorldPos(Vector3 p) { if (t != null) WritePos(t.parent != null ? t.parent.InverseTransformPoint(p) : p); }
            /// <summary>Back to the clean pose, so nothing written last frame builds up.</summary>
            public void Reset()
            {
                if (t == null) return;
                Write(Base());
                if (hasPos) WritePos(BasePos());
            }
            /// <summary>Applies a world-space rotation on top of the bone's current pose (it turns about its own pivot).</summary>
            public void Turn(Quaternion worldDelta) { if (t != null) SetWorld(worldDelta * t.rotation); }
            public void SetWorld(Quaternion world)
            {
                if (t == null) return;
                var parent = t.parent != null ? t.parent.rotation : Quaternion.identity;
                Write(Quaternion.Inverse(parent) * world);
            }
            /// <summary>Turns so that 'child' (a point on this bone, usually the next joint) lands on the line toward 'target'.</summary>
            public void Aim(Transform child, Vector3 target, float weight = 1f)
            {
                if (t == null || child == null || weight <= 0.001f) return;
                var from = child.position - t.position;
                var to = target - t.position;
                if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;
                Turn(Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), weight));
            }
        }

        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;

        Bone bBody, bS1, bS2, bS3, bS4, bNeckBase, bNeck, bHead, bJaw, bLidL, bLidR, bEarL, bEarR, bChest;
        Bone bUpperL, bLowerL, bPawL, bPalmL, bFingerL, bUpperR, bLowerR, bPawR, bPalmR, bFingerR;
        Bone bHips, bFemurL, bKneeL, bAnkleL, bBallL, bToeL, bFemurR, bKneeR, bAnkleR, bBallR, bToeR;
        Bone[] bSpine, bTail, all;
        Quaternion[] snapRot;
        Vector3[] snapPos;
        bool posed;               // the procedural body is available (Stage 45 bones are filled in)

        // Her proportions, measured from the rig (so the pose numbers scale with the model).
        float k = 1f, legLength, bodyLen, pelvisZ, frontMeta, hindMeta;
        float[] tailLen = new float[0];

        void BuildBones()
        {
            bBody = new Bone(body);
            bS1 = new Bone(spine != null && spine.Length > 0 ? spine[0] : null);
            bS2 = new Bone(spine != null && spine.Length > 1 ? spine[1] : null);
            bS3 = new Bone(spine != null && spine.Length > 2 ? spine[2] : null);
            bS4 = new Bone(spineTop);
            bNeckBase = new Bone(neckBase); bNeck = new Bone(neck); bHead = new Bone(head); bJaw = new Bone(jaw);
            bLidL = new Bone(lidL); bLidR = new Bone(lidR); bEarL = new Bone(earL); bEarR = new Bone(earR); bChest = new Bone(chest);
            bUpperL = new Bone(upperL); bLowerL = new Bone(lowerL); bPawL = new Bone(pawL); bPalmL = new Bone(palmL); bFingerL = new Bone(fingerL);
            bUpperR = new Bone(upperR); bLowerR = new Bone(lowerR); bPawR = new Bone(pawR); bPalmR = new Bone(palmR); bFingerR = new Bone(fingerR);
            bHips = new Bone(hips);
            bFemurL = new Bone(femurL); bKneeL = new Bone(kneeL); bAnkleL = new Bone(ankleL); bBallL = new Bone(ballL); bToeL = new Bone(toeL);
            bFemurR = new Bone(femurR); bKneeR = new Bone(kneeR); bAnkleR = new Bone(ankleR); bBallR = new Bone(ballR); bToeR = new Bone(toeR);
            bSpine = new[] { bS1, bS2, bS3 };
            bTail = new Bone[tail != null ? tail.Length : 0];
            for (int i = 0; i < bTail.Length; i++) bTail[i] = new Bone(tail[i]);

            // Parents before children, so resetting a parent never disturbs a child's clean pose.
            var list = new System.Collections.Generic.List<Bone>
            {
                bBody, bS1, bS2, bS3, bS4, bNeckBase, bNeck, bHead, bJaw, bLidL, bLidR, bEarL, bEarR, bChest,
                bUpperL, bLowerL, bPawL, bPalmL, bFingerL, bUpperR, bLowerR, bPawR, bPalmR, bFingerR,
                bHips, bFemurL, bKneeL, bAnkleL, bBallL, bToeL, bFemurR, bKneeR, bAnkleR, bBallR, bToeR
            };
            list.AddRange(bTail);
            list.RemoveAll(b => !b.Ok);
            all = list.ToArray();
            snapRot = new Quaternion[all.Length];
            snapPos = new Vector3[all.Length];

            // Measurements, in the pose the prefab was saved in (the watching pose).
            float D(Transform a, Transform b) => a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f;
            legLength = D(upperL, lowerL) + D(lowerL, pawL);
            posed = body != null && spineTop != null && bS1.Ok && bS2.Ok && bS3.Ok && neckBase != null
                    && palmL != null && fingerL != null && palmR != null && fingerR != null
                    && femurL != null && kneeL != null && ankleL != null && ballL != null && toeL != null
                    && femurR != null && kneeR != null && ankleR != null && ballR != null && toeR != null;
            if (posed)
            {
                bodyLen = D(spine[0], spineTop);
                k = bodyLen / 11.84f;                     // the numbers below were laid out for this model at its first size
                pelvisZ = transform.InverseTransformPoint(spine[0].position).z;
                frontMeta = D(pawL, palmL);
                hindMeta = D(ankleL, ballL);
            }
            else k = Mathf.Max(0.01f, legLength / 10.6f);
            tailLen = new float[bTail.Length];
            for (int i = 0; i + 1 < bTail.Length; i++) tailLen[i] = D(tail[i], tail[i + 1]);
        }

        void StartGraph()
        {
            if (idleClip == null || graph.IsValid()) return;
            // Two copies of the idle clip: one playing (grooming), one frozen at the rest pose (watching).
            graph = PlayableGraph.Create("Biscuit");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var groomClip = AnimationClipPlayable.Create(graph, idleClip);
            var rest = AnimationClipPlayable.Create(graph, idleClip);
            rest.SetTime(restTime);
            rest.SetSpeed(0);
            graph.Connect(groomClip, 0, mixer, 0);
            graph.Connect(rest, 0, mixer, 1);
            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 1f);
            var output = AnimationPlayableOutput.Create(graph, "Biscuit", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        // ------------------------------------------------------------------ pose numbers

        /// <summary>
        /// The posed body's numbers, in units of her first size (scaled by her measured body length).
        /// Heights are her pelvis and shoulders above the surface; neck numbers place her head
        /// forward / up from the top of her spine.
        /// </summary>
        [System.Serializable]
        public class PoseTuning
        {
            public float standHip = 14.8f, standShoulder = 14.2f;
            public float crouchHip = 8.4f, crouchShoulder = 7.2f;
            public float stretchHip = 13.8f, stretchShoulder = 5.0f;
            public float lieHip = 4.4f, lieShoulder = 5.4f;
            [Tooltip("Front feet forward (+) or back (-) of the shoulder joint; hind feet of the hip joint.")]
            public float frontFootFwd = -0.8f, hindFootFwd = 0.8f;
            [Tooltip("How much the feet come in toward her midline (0 = straight under the joints, 1 = on the line).")]
            public float frontNarrow = 0.4f, hindNarrow = 0.3f;
            public float neckFwd = 4.6f, neckUp = 1.6f, headPitch = -6f;
            public float lieNeckFwd = 3.7f, lieNeckUp = 3.0f;
            public float sleepNeckFwd = 3.6f, sleepNeckUp = 1.3f, sleepPitch = -16f;
            public float arch = 0.7f, lieArch = 0.15f;
            [Tooltip("Hind foot: angle of the long foot bone up from the floor (she stands on her toes).")]
            public float hindMetaAngle = 62f;
        }

        [Header("Pose (Stage 45)")]
        public PoseTuning pose = new PoseTuning();

        // ------------------------------------------------------------------ what the mind asks for

        enum Gait { Walk, Trot, Stalk }
        Gait gait = Gait.Walk;
        float moveSpeed;                       // what the mind is doing with her root (for the gait)

        float sitW = 1f, sitGoal = 1f;         // 1 = sitting (the clip)
        float lieW, lieGoal;                   // 1 = lying down
        float sleepy, sleepyGoal;              // chin down, eyes shut
        float groom, groomGoal;                // sitting: groom (the clip playing) instead of watching
        float yawn, yawnGoal;
        float stretch, stretchGoal;
        float crouch, crouchGoal;              // standing: low (stalk, gather)
        float arch, archS;                     // fear: back up, ears flat
        bool wiggle, knead, chatter, slowBlink;
        float earAlert, eyeOpen;               // asleep: stages of being disturbed
        Vector2 glance;                        // idle glances (yaw, pitch degrees)
        Vector3? glanceAt;                     // look at a point
        bool pushOff, airborne, airStrike;
        float airU, airVy, landed = -9f;

        void SetPose(float sit, float lie, bool snap = false)
        {
            sitGoal = posed ? sit : 1f;   // without the Stage 45 bones she can only sit
            lieGoal = posed ? lie : 0f;
            if (snap) { sitW = sitGoal; lieW = lieGoal; feetValid = false; }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Ease(float x) => Smooth(x);
        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); }
        static float Expo(float current, float goal, float rate, float dt) => Mathf.Lerp(current, goal, 1f - Mathf.Exp(-rate * dt));

        void UpdateParams(float dt)
        {
            sitW = Mathf.MoveTowards(sitW, sitGoal, dt / 0.6f);
            lieW = Mathf.MoveTowards(lieW, lieGoal, dt / (lieGoal > lieW ? 1.1f : 0.7f));
            sleepy = Mathf.MoveTowards(sleepy, sleepyGoal, dt / 1.3f);
            groom = Mathf.MoveTowards(groom, sitW > 0.95f && target == null ? groomGoal : 0f, dt / 0.5f);
            yawn = Mathf.MoveTowards(yawn, yawnGoal, dt / (yawnGoal > yawn ? 0.6f : 0.35f));
            stretch = Mathf.MoveTowards(stretch, stretchGoal, dt / 0.8f);
            crouch = Expo(crouch, crouchGoal, 5f, dt);
            archS = Expo(archS, arch, 4f, dt);
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, groom);
                mixer.SetInputWeight(1, 1f - groom);
            }
        }

        // ------------------------------------------------------------------ every frame

        Vector3 lastRootPos, velocity;
        float lastYaw, yawRate;

        void LateUpdate()
        {
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            foreach (var b in all) b.Reset();
            UpdateParams(dt);

            // How fast her root is going and turning (the gait and the spine's sideways bend follow it).
            var o = transform.position;
            velocity = Vector3.Lerp(velocity, (o - lastRootPos) / dt, 1f - Mathf.Exp(-12f * dt));
            lastRootPos = o;
            float yawNow = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yawNow) / dt, 1f - Mathf.Exp(-8f * dt));
            lastYaw = yawNow;

            float ws = Smooth(sitW);
            if (posed && ws < 0.999f)
            {
                PoseBody(dt);
                if (ws > 0.001f)
                {
                    for (int i = 0; i < all.Length; i++) { snapRot[i] = all[i].t.localRotation; snapPos[i] = all[i].t.localPosition; }
                    for (int i = 0; i < all.Length; i++)
                    {
                        all[i].Reset();
                        all[i].Write(Quaternion.Slerp(snapRot[i], all[i].t.localRotation, ws));
                        if (all[i] == bBody) all[i].WritePos(Vector3.Lerp(snapPos[i], all[i].t.localPosition, ws));
                    }
                }
            }
            else feetValid = false;

            if (ws > 0.98f) SeatedLayer();
            HeadLayer(ws);
            FaceLayer();
            if (ws > 0.5f || !posed) SeatedTail();
        }

        // ------------------------------------------------------------------ the posed body

        struct Foot { public Vector3 planted, from, to; public bool swing; public float t, dur; }
        readonly Foot[] feet = new Foot[4];      // 0 front left, 1 front right, 2 hind left, 3 hind right
        readonly Vector3[] footAt = new Vector3[4];
        readonly float[] footLift = new float[4];
        bool feetValid;
        float gaitPhase;
        int lastCorrected = -1;

        static readonly float[] WalkOrder = { 0.25f, 0.75f, 0f, 0.5f };    // hind left, fore left, hind right, fore right
        static readonly float[] TrotOrder = { 0f, 0.5f, 0.5f, 0f };        // diagonal pairs

        void PoseBody(float dt)
        {
            Vector3 up = Vector3.up, fwd = transform.forward, right = transform.right, o = transform.position;
            float t = Time.time;
            float lie = Smooth(lieW), st = Smooth(stretch), cr = crouch * (1f - lie), ar = archS * (1f - lie);
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = !airborne;

            // ---- heights of her pelvis and shoulders ----
            var q = pose;
            float hip = q.standHip, sh = q.standShoulder;
            hip = Mathf.Lerp(hip, q.crouchHip, cr); sh = Mathf.Lerp(sh, q.crouchShoulder, cr);
            hip = Mathf.Lerp(hip, q.stretchHip, st); sh = Mathf.Lerp(sh, q.stretchShoulder, st);
            hip += 1.4f * ar; sh += 0.6f * ar;
            hip = Mathf.Lerp(hip, q.lieHip, lie); sh = Mathf.Lerp(sh, q.lieShoulder, lie);
            if (pushOff) { hip += 2.5f; sh += 1.5f; }
            float sinceLand = t - landed;
            if (sinceLand < 0.3f) { float sq = Mathf.Sin(Mathf.PI * sinceLand / 0.3f) * 2.6f; hip -= sq * 0.7f; sh -= sq; }
            // Walking bob: twice per stride, the shoulders a little after the hips.
            if (grounded && speed > 0.5f && lie < 0.5f)
            {
                float bob = Mathf.Clamp01(speed / walkSpeed) * (gait == Gait.Trot ? 0.6f : 0.35f) * (gait == Gait.Stalk ? 0.3f : 1f);
                hip -= bob * (0.5f + 0.5f * Mathf.Cos(gaitPhase * Mathf.PI * 4f));
                sh -= bob * (0.5f + 0.5f * Mathf.Cos(gaitPhase * Mathf.PI * 4f - 1.2f));
            }
            // Breathing (slower and deeper asleep).
            float breath = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Lerp(2.6f, 4.2f, sleepy)) * Mathf.Lerp(0.12f, 0.25f, lie);
            hip *= k; sh *= k;

            float wig = wiggle ? Mathf.Sin(t * 15f) * 0.9f * k : 0f;
            Vector3 P = o + up * hip + fwd * pelvisZ + right * wig;
            float dy = sh - hip;
            float L = bodyLen * 0.97f;
            float dz = Mathf.Sqrt(Mathf.Max(L * L - dy * dy, 1f * k));
            Vector3 S = P + fwd * dz + up * dy - right * wig * 0.3f;
            if (airborne)
            {
                // Nose up while rising, down while falling.
                var tilt = Quaternion.AngleAxis(-airVy * 22f, right);
                S = P + tilt * (S - P);
            }

            // ---- the spine: place the body, then bend the chain through the points ----
            bBody.WriteWorldPos(P);
            bBody.Turn(Quaternion.FromToRotation(spineTop.position - body.position, S - P));
            float roll = grounded && speed > 0.5f ? Mathf.Sin(gaitPhase * Mathf.PI * 2f) * 1.5f : 0f;
            roll += Mathf.Clamp(-yawRate * 0.04f, -6f, 6f);
            bBody.Turn(Quaternion.AngleAxis(roll, (S - P).normalized));

            Vector3 axis = (S - P).normalized;
            Vector3 upish = Vector3.Cross(axis, right).normalized;
            float archH = (Mathf.Lerp(q.arch, q.lieArch, lie) + 3.2f * ar - 2.2f * st - 0.4f * cr) * k + breath * k;
            float bend = Mathf.Clamp(yawRate * 0.025f, -2.2f, 2.2f) * k * (1f - lie);
            float l12 = Vector3.Distance(bS1.Pos, bS2.Pos), l23 = Vector3.Distance(bS2.Pos, bS3.Pos), l34 = Vector3.Distance(bS3.Pos, bS4.Pos);
            float lt = Mathf.Max(0.01f, l12 + l23 + l34);
            Vector3 Curve(float f) => P + (S - P) * f + upish * archH * Mathf.Sin(Mathf.PI * f) + right * bend * Mathf.Sin(Mathf.PI * f);
            bS1.Aim(spine[1], Curve(l12 / lt));
            bS2.Aim(spine[2], Curve((l12 + l23) / lt));
            bS3.Aim(spineTop, S);

            // ---- neck and head: carried forward, level ----
            Vector3 flatFwd = fwd;
            float nf = q.neckFwd, nu = q.neckUp, pitchHead = q.headPitch;
            nf = Mathf.Lerp(nf, 4.9f, cr); nu = Mathf.Lerp(nu, 0.8f, cr); pitchHead = Mathf.Lerp(pitchHead, -2f, cr);
            nf = Mathf.Lerp(nf, 5.4f, st); nu = Mathf.Lerp(nu, 1.4f, st); pitchHead = Mathf.Lerp(pitchHead, 18f, st);
            nf = Mathf.Lerp(nf, 3.2f, ar); nu = Mathf.Lerp(nu, 0.2f, ar);
            nf = Mathf.Lerp(nf, q.lieNeckFwd, lie); nu = Mathf.Lerp(nu, q.lieNeckUp, lie);
            float sl = Smooth(sleepy) * lie;
            nf = Mathf.Lerp(nf, q.sleepNeckFwd, sl); nu = Mathf.Lerp(nu, q.sleepNeckUp, sl); pitchHead = Mathf.Lerp(pitchHead, q.sleepPitch, sl);
            if (airborne) pitchHead -= airVy * 10f;
            Vector3 H = spineTop.position + flatFwd * nf * k + up * nu * k + right * sl * 1.2f * k;
            Vector3 mid = Vector3.Lerp(neckBase.position, H, 0.45f) + up * 0.6f * k;
            bNeckBase.Aim(neck, mid);
            bNeck.Aim(head, H);
            var face = Quaternion.AngleAxis(-pitchHead, right) * flatFwd;
            var tiltSide = Quaternion.AngleAxis(sl * 25f, face);
            bHead.SetWorld(tiltSide * Quaternion.LookRotation(face, up) * Quaternion.Inverse(Quaternion.LookRotation(headFace, headUp)));

            // ---- legs ----
            Vector3 Ground(Vector3 p) => p - up * Vector3.Dot(p - o, up);
            float Lateral(Vector3 p) => Vector3.Dot(p - o, right);
            var neutral = new Vector3[4];
            Vector3 fl = Ground(upperL.position), fr = Ground(upperR.position), hl = Ground(femurL.position), hr = Ground(femurR.position);
            neutral[0] = fl - right * Lateral(fl) * q.frontNarrow + fwd * q.frontFootFwd * k;
            neutral[1] = fr - right * Lateral(fr) * q.frontNarrow + fwd * q.frontFootFwd * k;
            neutral[2] = hl - right * Lateral(hl) * q.hindNarrow + fwd * q.hindFootFwd * k;
            neutral[3] = hr - right * Lateral(hr) * q.hindNarrow + fwd * q.hindFootFwd * k;
            if (st > 0.01f) { neutral[0] += fwd * 6.5f * k * st; neutral[1] += fwd * 6.5f * k * st; }

            Gaiting(dt, neutral, grounded && lie < 0.5f);

            // Where each foot is this frame: gait (or tidy standing), blended toward lying, or the air pose.
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = footAt[i];
                if (knead && i < 2 && grounded)
                    p += up * Mathf.Max(0f, Mathf.Sin(t * 6.5f + i * Mathf.PI)) * 1.5f * k;
                footAt[i] = p;
            }

            float sideL = -1f, sideR = 1f;
            // Lying: paws tucked under her chest, hind legs folded along her sides.
            Vector3 LoafWrist(Vector3 n, float side) => n - right * side * 0.4f * k + fwd * 0.6f * k + up * 1.4f * k;
            Vector3 LoafHock(Vector3 hipGround, float side) => hipGround - fwd * 2.2f * k + right * side * 1.3f * k + up * 0.7f * k;

            // Front legs.
            FrontLeg(bUpperL, bLowerL, bPawL, bPalmL, palmL, fingerL, 0, sideL, LoafWrist(fl, sideL), lie, up, fwd, right, cr);
            FrontLeg(bUpperR, bLowerR, bPawR, bPalmR, palmR, fingerR, 1, sideR, LoafWrist(fr, sideR), lie, up, fwd, right, cr);
            // Hind legs.
            HindLeg(bFemurL, bKneeL, bAnkleL, bBallL, ankleL, ballL, toeL, 2, sideL, LoafHock(hl, sideL), lie, up, fwd, right);
            HindLeg(bFemurR, bKneeR, bAnkleR, bBallR, ankleR, ballR, toeR, 3, sideR, LoafHock(hr, sideR), lie, up, fwd, right);

            // ---- tail ----
            PoseTail(speed, cr, lie, ar, up, fwd, right, t);
        }

        /// <summary>Planted feet and a gait: each foot stays put on the floor until its turn to swing.</summary>
        void Gaiting(float dt, Vector3[] neutral, bool active)
        {
            if (!active || !feetValid)
            {
                for (int i = 0; i < 4; i++)
                {
                    feet[i].planted = neutral[i];
                    feet[i].swing = false;
                    footAt[i] = neutral[i];
                    footLift[i] = 0f;
                }
                feetValid = active;
                return;
            }
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool moving = speed > 0.8f || Mathf.Abs(moveSpeed) > 0.5f;
            float period, duty, lift;
            float[] order;
            switch (gait)
            {
                case Gait.Trot: period = Mathf.Lerp(0.6f, 0.4f, Mathf.Clamp01(speed / trotSpeed)); duty = 0.5f; lift = 2.8f; order = TrotOrder; break;
                case Gait.Stalk: period = 1.7f; duty = 0.78f; lift = 1.4f; order = WalkOrder; break;
                default: period = Mathf.Lerp(1.15f, 0.7f, Mathf.Clamp01(speed / walkSpeed)); duty = 0.64f; lift = 2.1f; order = WalkOrder; break;
            }
            Vector3 vFlat = new Vector3(velocity.x, 0f, velocity.z);

            if (moving)
            {
                gaitPhase += dt / period;
                for (int i = 0; i < 4; i++)
                {
                    float local = Mathf.Repeat(gaitPhase - order[i], 1f);
                    bool inSwing = local >= duty;
                    if (inSwing && !feet[i].swing)
                    {
                        feet[i].swing = true;
                        feet[i].from = feet[i].planted;
                        feet[i].dur = (1f - duty) * period;
                        feet[i].t = 0f;
                        feet[i].to = neutral[i] + vFlat * (duty * period * 0.5f + feet[i].dur);
                    }
                }
            }
            else
            {
                // Standing still: tidy up one foot at a time (this is also how she turns on the spot).
                bool any = false;
                for (int i = 0; i < 4; i++) any |= feet[i].swing;
                if (!any)
                {
                    int worst = -1;
                    float wd = 1.1f * k;
                    for (int j = 0; j < 4; j++)
                    {
                        int i = (lastCorrected + 1 + j) % 4;
                        float d = Vector3.Distance(feet[i].planted, neutral[i]);
                        if (d > wd) { wd = d; worst = i; }
                    }
                    if (worst >= 0)
                    {
                        feet[worst].swing = true;
                        feet[worst].from = feet[worst].planted;
                        feet[worst].to = neutral[worst];
                        feet[worst].dur = 0.24f;
                        feet[worst].t = 0f;
                        lastCorrected = worst;
                    }
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (feet[i].swing)
                {
                    feet[i].t += dt;
                    float s = Mathf.Clamp01(feet[i].t / Mathf.Max(0.05f, feet[i].dur));
                    float l = lift * k * (i < 2 ? 1.15f : 0.9f) * (moving ? 1f : 0.6f);
                    footLift[i] = Mathf.Max(0f, Mathf.Sin(Mathf.PI * s));
                    footAt[i] = Vector3.Lerp(feet[i].from, feet[i].to, Smooth(s)) + Vector3.up * l * Mathf.Pow(footLift[i], 0.8f);
                    if (s >= 1f) { feet[i].swing = false; feet[i].planted = feet[i].to; footLift[i] = 0f; }
                }
                else
                {
                    footAt[i] = feet[i].planted;
                    footLift[i] = 0f;
                }
            }
        }

        void FrontLeg(Bone upper, Bone lower, Bone wrist, Bone palm, Transform palmT, Transform fingerT, int i, float side,
                      Vector3 loafWrist, float lie, Vector3 up, Vector3 fwd, Vector3 right, float cr)
        {
            Vector3 shoulder = upper.Pos;
            Vector3 foot = footAt[i];
            float swing = footLift[i];
            // Standing: the wrist sits above the paw, which lies flat; in its swing the paw curls under.
            Vector3 w = foot + up * frontMeta * 0.92f - fwd * frontMeta * 0.18f;
            Vector3 palmAt = foot + fwd * 0.2f * k;
            if (swing > 0f) palmAt = Vector3.Lerp(palmAt, w + (-fwd * 0.6f - up * 0.9f) * frontMeta, swing);
            Vector3 toeAt = palmAt + fwd * 1.1f * k - up * 0.15f * k;

            if (airborne)
            {
                // Tucked under the chest going up, reaching forward and down for the landing coming down.
                float reachOut = Mathf.Clamp01(1f - airVy);
                Vector3 tuck = shoulder + fwd * 2.5f * k - up * 5.5f * k + right * side * 0.6f * k;
                Vector3 reach = shoulder + fwd * 6.5f * k - up * 9.5f * k + right * side * 0.7f * k;
                if (airStrike && target != null)
                {
                    // Pounce: both paws go for the plane.
                    var d = target.position - shoulder;
                    reach = shoulder + Vector3.ClampMagnitude(d, legLength * 0.95f) + right * side * 0.8f * k;
                    reachOut = 1f;
                }
                w = Vector3.Lerp(tuck, reach, reachOut);
                palmAt = w + (fwd * 0.4f - up * 0.9f).normalized * frontMeta;
                toeAt = palmAt + fwd * 1.0f * k;
            }
            if (lie > 0.001f)
            {
                w = Vector3.Lerp(w, loafWrist, lie);
                palmAt = Vector3.Lerp(palmAt, loafWrist - fwd * frontMeta * 0.95f - up * 0.35f * k, lie);
                toeAt = Vector3.Lerp(toeAt, palmAt - fwd * 1.0f * k, lie);
            }

            // Elbows bend back (and out, and up when lying).
            Vector3 pole = -fwd + right * side * 0.3f + up * (0.2f + 0.8f * lie) - up * 0.3f * cr;
            TwoBone(upper, lower, wrist.t, w, upper.Pos + pole * 10f);
            wrist.Aim(palmT, palmAt);
            palm.Aim(fingerT, toeAt);
        }

        void HindLeg(Bone femur, Bone knee, Bone ankle, Bone ball, Transform ankleT, Transform ballT, Transform toeT, int i, float side,
                     Vector3 loafHock, float lie, Vector3 up, Vector3 fwd, Vector3 right)
        {
            Vector3 hipJoint = femur.Pos;
            Vector3 foot = footAt[i];
            // Standing: up on her toes, the hock high and a little behind the ball of the foot.
            float metaAngle = pose.hindMetaAngle * Mathf.Deg2Rad;
            Vector3 hock = foot + up * hindMeta * Mathf.Sin(metaAngle) - fwd * hindMeta * Mathf.Cos(metaAngle);
            Vector3 ballAt = foot;
            Vector3 toeAt = foot + fwd * 1.2f * k;
            if (footLift[i] > 0f) toeAt -= up * 0.6f * k * footLift[i];

            if (airborne)
            {
                // Legs trail out behind on the way up, tuck forward for the landing.
                float tuck = Mathf.Clamp01(1f - airVy);
                Vector3 trail = hipJoint - fwd * 8.5f * k - up * 6.5f * k + right * side * 0.6f * k;
                Vector3 under = hipJoint + fwd * 2f * k - up * 9f * k + right * side * 0.8f * k;
                Vector3 b = Vector3.Lerp(trail, under, tuck);
                ballAt = b;
                hock = b + (up * 0.6f - fwd).normalized * hindMeta;
                toeAt = b + (Vector3.Lerp(-fwd, fwd, tuck) - up * 0.4f).normalized * 1.2f * k;
            }
            if (lie > 0.001f)
            {
                hock = Vector3.Lerp(hock, loafHock, lie);
                Vector3 loafBall = loafHock + fwd * hindMeta * 0.97f + right * side * 0.5f * k - up * 0.45f * k;
                ballAt = Vector3.Lerp(ballAt, loafBall, lie);
                toeAt = Vector3.Lerp(toeAt, loafBall + fwd * 1.2f * k, lie);
            }

            // Knees bend forward (and out, a lot more when she's folded up lying down).
            Vector3 pole = fwd + right * side * (0.25f + 1.1f * lie) + up * 0.4f * lie;
            TwoBone(femur, knee, ankleT, hock, femur.Pos + pole * 10f);
            ankle.Aim(ballT, ballAt);
            ball.Aim(toeT, toeAt);
        }

        // Tail shapes: each segment's angle up from straight back (pitch) and round to her side (yaw).
        static readonly float[] TailIdle = { -8f, 6f, 20f, 30f, 38f, 44f };
        static readonly float[] TailWalk = { 32f, 48f, 64f, 78f, 98f, 122f };      // the happy question mark
        static readonly float[] TailLow = { -18f, -12f, -6f, -4f, 2f, 12f };       // hunting: low, the tip alive
        static readonly float[] TailArch = { 62f, 80f, 88f, 90f, 90f, 90f };       // spooked: straight up
        static readonly float[] TailAir = { 8f, 2f, -4f, -6f, -4f, 0f };           // a rudder in the air
        static readonly float[] TailWrapP = { -26f, -18f, -10f, -4f, 0f, 0f };
        static readonly float[] TailWrapY = { 25f, 48f, 72f, 98f, 122f, 142f };    // wrapped round her side

        void PoseTail(float speed, float cr, float lie, float ar, Vector3 up, Vector3 fwd, Vector3 right, float t)
        {
            int n = bTail.Length;
            if (n < 2) return;
            float walkAmt = Mathf.Clamp01(speed / walkSpeed) * (1f - cr);
            bool hunting = act == Act.Hunt || crouch > 0.3f;
            for (int i = 0; i + 1 < n && i < TailIdle.Length; i++)
            {
                float p = Mathf.Lerp(TailIdle[i], TailWalk[i], walkAmt);
                p = Mathf.Lerp(p, TailLow[i], cr);
                p = Mathf.Lerp(p, TailArch[i], ar);
                if (airborne) p = Mathf.Lerp(p, TailAir[i], 0.8f);
                float y = 0f;
                p = Mathf.Lerp(p, TailWrapP[i], lie);
                y = Mathf.Lerp(y, TailWrapY[i], lie);
                float along = i / (float)(n - 1);
                // Life: a lazy sway, swinging with her steps, a twitching tip when hunting, a slow flick asleep.
                float sway = Mathf.Sin(t * 0.9f - i * 0.5f) * (5f + 6f * along) * (1f - lie * 0.7f);
                if (hunting) sway += Mathf.Sin(t * 9f + Mathf.Sin(t * 2.3f) * 3f) * 22f * along * along;
                if (lie > 0.5f && sleeping) sway = Mathf.Sin(t * 0.5f) * 4f * along + (disturbance > 0.2f ? Mathf.Sin(t * 5f) * 12f * along * along : 0f);
                sway += Mathf.Sin(gaitPhase * Mathf.PI * 2f - i * 0.4f) * 5f * walkAmt;
                Vector3 dir = Quaternion.AngleAxis(y + sway, up) * (Quaternion.AngleAxis(p, right) * -fwd);
                bTail[i].Aim(tail[i + 1], tail[i].position + dir * tailLen[i]);
            }
        }

        /// <summary>
        /// Two-bone IK: turns the upper bone, then the lower one, so the end reaches the target. The middle
        /// joint bends toward 'poleAt', and a leg can straighten but never stretch.
        /// </summary>
        void TwoBone(Bone upper, Bone lower, Transform end, Vector3 target, Vector3 poleAt, float maxReach = 0.995f)
        {
            if (!upper.Ok || !lower.Ok || end == null) return;
            var a = upper.t.position;
            var b = lower.t.position;
            var c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            var toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.05f * (l1 + l2), maxReach * (l1 + l2));
            var dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (c - a).normalized;
            var bendDir = Vector3.ProjectOnPlane(poleAt - a, dir);
            if (bendDir.sqrMagnitude < 1e-8f) bendDir = Vector3.ProjectOnPlane(b - a, dir);
            bendDir.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            var elbow = a + (dir * cosA + bendDir * Mathf.Sqrt(1f - cosA * cosA)) * l1;
            var endAt = a + dir * d;
            upper.Turn(Quaternion.FromToRotation(b - a, elbow - a));
            b = lower.t.position;
            c = end.position;
            lower.Turn(Quaternion.FromToRotation(c - b, endAt - b));
        }

        // ------------------------------------------------------------------ head, face, ears

        float yaw, pitch, yawVel, pitchVel;
        float blinkStart, blinkLength, nextBlink, earTwitch, nextTwitch;
        float jawSign;

        void HeadLayer(float ws)
        {
            if (head == null) return;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
            float t = Time.time;
            float wantYaw = glance.x, wantPitch = glance.y;
            Vector3? lookAt = seat == SeatState.Strike ? strikePoint : target != null ? target.position : glanceAt;
            if (glanceAt.HasValue && target == null) lookAt = glanceAt;
            if (lookAt.HasValue)
            {
                var d = lookAt.Value - head.position;
                var flat = Vector3.ProjectOnPlane(d, up);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    wantYaw = Mathf.Clamp(Vector3.SignedAngle(fwd, flat, up), -maxYaw, maxYaw);
                    wantPitch = Mathf.Clamp(Vector3.SignedAngle(flat, d, Vector3.Cross(flat, up)), -maxPitch, maxPitch);   // positive = above her
                }
            }
            // Asleep she doesn't look about (until she's bothered enough to lift her head).
            float lookW = Mathf.Lerp(1f, sleeping ? Mathf.Clamp01((disturbance - 0.5f) / 0.3f) : 1f, Smooth(lieW));
            if (airborne) lookW *= 0.4f;
            if (yawn > 0.3f) lookW *= 0.3f;
            float lookSpeed = seat == SeatState.Strike || pouncing ? 0.08f : 0.18f;
            yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVel, lookSpeed);
            pitch = Mathf.SmoothDampAngle(pitch, wantPitch, ref pitchVel, lookSpeed * 1.2f);
            // Sitting and watching (no target), the clip's bowed head comes up; posed, the head is already level.
            float watchW = ws > 0.5f ? (lookAt.HasValue || seat != SeatState.Idle || glance != Vector2.zero ? 1f : 1f - groom) : 1f;
            float w = lookW * watchW;
            if (w < 0.001f) return;
            var wantDir = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(-pitch, right) * fwd;
            // A little head tilt when she's curious about something above her.
            float tilt = Mathf.Clamp(pitch, 0f, 30f) * 0.4f * Mathf.Sin(t * 0.3f);
            var desired = Quaternion.AngleAxis(tilt, wantDir) * Quaternion.LookRotation(wantDir, up) * Quaternion.Inverse(Quaternion.LookRotation(headFace, headUp));
            if (ws < 0.5f && Mathf.Abs(yaw) < 1f && Mathf.Abs(pitch) < 1f && tilt == 0f) return;   // posed head already faces forward
            bNeck.Turn(Quaternion.Slerp(Quaternion.identity, desired * Quaternion.Inverse(head.rotation), 0.4f * w));
            bHead.Turn(Quaternion.Slerp(Quaternion.identity, desired * Quaternion.Inverse(head.rotation), w));
        }

        void FaceLayer()
        {
            float t = Time.time, dt = Time.deltaTime;
            bool hunting = seat == SeatState.Stalk || seat == SeatState.Strike || crouch > 0.5f || pouncing;

            // ---- eyes ----
            if (t >= nextBlink)
            {
                blinkStart = t;
                blinkLength = target != null ? 0.16f : Random.Range(0.45f, 0.8f);
                nextBlink = t + (hunting ? Random.Range(4f, 7f) : target != null ? Random.Range(2f, 4.5f) : Random.Range(3f, 7f));
            }
            float bt = (t - blinkStart) / Mathf.Max(0.01f, blinkLength);
            float blink = bt >= 0f && bt <= 1f ? Mathf.Sin(bt * Mathf.PI) : 0f;
            float lid = Mathf.Lerp(blink, -0.15f, (hunting ? 1f : 0f) * (1f - blink));   // negative = wider than open
            if (slowBlink) lid = Mathf.Max(lid, 0.75f);
            lid = Mathf.Max(lid, yawn * 0.9f);
            float asleep = Smooth(sleepy) * Smooth(lieW);
            float lidL2 = Mathf.Lerp(lid, 1f - eyeOpen * 0.85f, asleep);   // the left eye is the one that opens a crack
            float lidR2 = Mathf.Lerp(lid, 1f - Mathf.Clamp01(eyeOpen - 0.6f) * 2f, asleep);
            if (lidL != null) bLidL.Write(Quaternion.SlerpUnclamped(lidOpenL, lidClosedL, lidL2));
            if (lidR != null) bLidR.Write(Quaternion.SlerpUnclamped(lidOpenR, lidClosedR, lidR2));

            // ---- ears: a twitch now and then; flat when spooked or yawning; flicking when disturbed asleep ----
            if (t >= nextTwitch) { earTwitch = 1f; nextTwitch = t + (asleep > 0.5f && earAlert > 0f ? Random.Range(0.6f, 1.6f) : Random.Range(2.5f, 7f)); }
            earTwitch = Mathf.MoveTowards(earTwitch, 0f, dt * 5f);
            float twitch = earTwitch * (asleep > 0.5f ? 0.35f + 0.5f * earAlert : 0.5f) * (hunting ? 0f : 1f);
            float flat = Mathf.Max(archS, yawn * 0.5f);
            if (earL != null) bEarL.Write(Quaternion.Slerp(bEarL.Base(), earBackL, Mathf.Max(twitch, flat)));
            if (earR != null) bEarR.Write(Quaternion.Slerp(bEarR.Base(), earBackR, flat));

            // ---- jaw: yawns and chattering at birds (planes) she can't reach ----
            if (bJaw.Ok && head != null)
            {
                float open = yawn * 30f + (chatter ? Mathf.Abs(Mathf.Sin(t * 38f)) * 7f : 0f);
                if (open > 0.1f)
                {
                    var faceW = head.rotation * headFace;
                    var upW = head.rotation * headUp;
                    var axis = Vector3.Cross(upW, faceW).normalized;
                    if (jawSign == 0f)
                    {
                        // Which way is "open": the side that drops the front of her jaw.
                        var p = jaw.position + faceW;
                        var q = jaw.position + Quaternion.AngleAxis(10f, axis) * faceW;
                        jawSign = Vector3.Dot(q - p, -upW) > 0f ? 1f : -1f;
                    }
                    bJaw.Turn(Quaternion.AngleAxis(open * jawSign, axis));
                }
            }
        }

        // ------------------------------------------------------------------ sitting: the seated swat (Stage 44)

        enum SeatState { Idle, Stalk, Strike, Recover }
        enum Kind { BatLeft, BatRight, Rear }
        SeatState seat;
        Kind kind;
        bool secondBat;
        float strikeStart, cooldownEnd, ignoreUntil, readiness;
        Vector3 strikePoint;
        float sCrouch, rear, watch;

        // Strike timings (seconds).
        const float WindUp = 0.16f, Lash = 0.10f, Hold = 0.08f, Back = 0.32f;          // one-paw bat
        const float Rise = 0.35f, Grab = 0.12f, RearHold = 0.15f, Down = 0.45f;          // rear-up grab
        const float MaxSpineBend = 13f;                                                  // degrees per spine bone

        Vector3 Shoulders => upperL != null && upperR != null ? (upperL.position + upperR.position) * 0.5f : transform.position;

        /// <summary>Sitting and awake: crouch when something's low and close, then bat it (Update).</summary>
        void UpdateSeatedSwat(float dt)
        {
            float now = Time.time;
            float reach = Mathf.Max(0.01f, legLength);
            bool canSwat = sitW > 0.95f && !sleeping && act != Act.Flee && lieW < 0.05f;
            bool inZone = false, inStrike = false;
            if (canSwat && target != null && head != null)
            {
                var flat = Vector3.ProjectOnPlane(target.position - transform.position, transform.up);
                inZone = flat.magnitude < stalkRange * reach && target.position.y < head.position.y + 5f * reach
                         && Vector3.Dot(flat, transform.forward) > -0.2f * flat.magnitude;
                inStrike = Vector3.Distance(target.position, Shoulders) < strikeRange * reach;
            }

            switch (seat)
            {
                case SeatState.Idle:
                case SeatState.Stalk:
                    readiness = Mathf.Clamp01(readiness + (inZone ? dt / buildUp : -dt / 0.8f));
                    seat = readiness > 0.02f ? SeatState.Stalk : SeatState.Idle;
                    if (seat == SeatState.Stalk && readiness >= 1f && inStrike && now >= cooldownEnd) BeginStrike(false);
                    break;
                case SeatState.Strike:
                    if (now - strikeStart >= StrikeLength())
                    {
                        // Still there after a bat? Often a second one with the other paw.
                        if (kind != Kind.Rear && !secondBat && inStrike && Random.value < 0.6f) BeginStrike(true);
                        else
                        {
                            seat = SeatState.Recover;
                            cooldownEnd = now + cooldown;
                            readiness = 0.4f;
                            if (Random.value < 0.3f) ignoreUntil = now + Random.Range(1.5f, 3f);   // "I didn't want it anyway"
                        }
                    }
                    break;
                case SeatState.Recover:
                    if (now >= cooldownEnd) seat = readiness > 0.02f ? SeatState.Stalk : SeatState.Idle;
                    break;
            }
            if (!canSwat && seat != SeatState.Strike) { seat = SeatState.Idle; readiness = 0f; }

            float k1 = 1f - Mathf.Exp(-4f * dt);
            watch = Mathf.Lerp(watch, target != null || seat != SeatState.Idle ? 1f : 0f, k1);
            float wantCrouch = seat == SeatState.Stalk ? readiness : seat == SeatState.Strike && kind != Kind.Rear ? 0.35f : 0f;
            sCrouch = Mathf.Lerp(sCrouch, wantCrouch, 1f - Mathf.Exp(-6f * dt));
        }

        void BeginStrike(bool second)
        {
            seat = SeatState.Strike;
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

        void SeatedLayer()
        {
            float t = Time.time;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;

            // Where the animation put her front paws (and how the elbows bend), before the body moves.
            var plantL = pawL != null ? pawL.position : Vector3.zero;
            var plantR = pawR != null ? pawR.position : Vector3.zero;
            var plantRotL = pawL != null ? pawL.rotation : Quaternion.identity;
            var plantRotR = pawR != null ? pawR.rotation : Quaternion.identity;
            var poleL = SeatPole(upperL, lowerL, pawL);
            var poleR = SeatPole(upperR, lowerR, pawR);
            float reach = Mathf.Max(0.01f, legLength);

            // ---- Strike timeline ----
            float st = t - strikeStart;
            float wind = secondBat ? WindUp * 0.5f : WindUp;
            float rearAmount = 0f, slamOn = 0f;
            Vector3? pawTargetL = null, pawTargetR = null;
            if (seat == SeatState.Strike)
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
            if (slam != null && !airborne) slam.targetStrength = slamOn;

            // ---- Body: breathing, the crouch (front lowers) or the rear-up, shared along the spine ----
            float breathe = Mathf.Sin(t * Mathf.PI * 2f / 3.2f) * 1.0f;
            float bendAmt = 14f * sCrouch - 36f * rear;   // turning about her right tips her front DOWN: positive = crouch
            for (int i = 0; i < bSpine.Length; i++)
            {
                float share = bSpine.Length > 1 ? Mathf.Lerp(0.4f, 0.25f, i / (float)(bSpine.Length - 1)) : 1f;   // more at the base
                bSpine[i].Turn(Quaternion.AngleAxis(Mathf.Clamp(bendAmt * share * 1.3f, -MaxSpineBend, MaxSpineBend), right));
            }
            bChest.Turn(Quaternion.AngleAxis(breathe, right));
            float wig = seat == SeatState.Stalk ? Mathf.InverseLerp(0.6f, 1f, readiness) : 0f;
            bHips.Turn(Quaternion.AngleAxis(Mathf.Sin(t * 15f) * 5f * wig, up));

            // ---- Front legs: planted unless striking ----
            poleL += -fwd * 0.25f * reach;
            poleR += -fwd * 0.25f * reach;
            float plantWeight = 1f - groom;   // while grooming, the clip's own paw moves stand
            SeatLeg(bUpperL, bLowerL, bPawL, pawTargetL ?? plantL, poleL, pawTargetL.HasValue ? 1f : plantWeight, pawTargetL.HasValue ? (Quaternion?)null : plantRotL, pawTargetL.HasValue ? 0.97f : 0.999f);
            SeatLeg(bUpperR, bLowerR, bPawR, pawTargetR ?? plantR, poleR, pawTargetR.HasValue ? 1f : plantWeight, pawTargetR.HasValue ? (Quaternion?)null : plantRotR, pawTargetR.HasValue ? 0.97f : 0.999f);
        }

        void SeatedTail()
        {
            float t = Time.time;
            Vector3 up = transform.up;
            bool hunting = seat == SeatState.Stalk || seat == SeatState.Strike || chatter;
            float h = hunting ? 1f : 0f;
            float amp = Mathf.Lerp(5f + 3f * watch, 9f, h);
            float freq = Mathf.Lerp(0.35f, 2.4f, h);
            for (int i = 0; i < bTail.Length; i++)
            {
                float along = bTail.Length > 1 ? i / (float)(bTail.Length - 1) : 1f;
                float weight = Mathf.Lerp(0.4f + 0.6f * along, along * along, h);
                bTail[i].Turn(Quaternion.AngleAxis(amp * weight * Mathf.Sin(t * freq * Mathf.PI * 2f - i * 0.6f), up));
            }
        }

        /// <summary>The way an elbow bends, from its pose now: from the shoulder-to-paw line out to the elbow.</summary>
        static Vector3 SeatPole(Transform upper, Transform lower, Transform paw)
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

        /// <summary>Stage 44's sitting-leg IK (pole as a direction, optional planted paw rotation).</summary>
        void SeatLeg(Bone upper, Bone lower, Bone paw, Vector3 target, Vector3 pole, float weight, Quaternion? pawWorld, float maxReach)
        {
            if (!upper.Ok || !lower.Ok || !paw.Ok || weight <= 0.001f) return;
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
    }
}
