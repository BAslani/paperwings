using UnityEngine;
using UnityEngine.InputSystem;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// Developer HUD drawn with IMGUI (OnGUI). Not the final UI.
    ///   - Top-left panel: numbers + bars (airspeed, vertical speed, boost). F1 hides it.
    ///   - Top-center: thermal/draft banner, so you always know WHY you're climbing or drifting.
    ///   - Center: launch prompt + charge bar, stall warning, crash/landing results.
    ///
    /// UNITY NOTE: OnGUI is Unity's old immediate-mode GUI. It's redrawn every frame from code,
    /// which makes it perfect for quick debug readouts. Real game UI will use UI Toolkit or uGUI.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] LaunchAndReset launcher;
        [Tooltip("Layers counted as 'floor' for the height readout.")]
        [SerializeField] LayerMask floorMask = ~0;
        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] RaceRoute route;

        GUIStyle panelText, big, small;
        bool showPanel = true;
        float scale;
        float ringFlash;   // >0 briefly after passing a ring
        LandingZone landingZone;
        PaperDamage paper;
        PlaneInput planeInput;
        FoldSelector folds;
        float damageFlash;      // >0 briefly after taking damage
        float lastDamage;
        StapleGun gun;
        float hitFlash;         // >0 briefly after your staple hits a rival
        float killFlash;        // >0 briefly after shooting a rival down
        PencilLauncher pencils;
        float noLockFlash;      // >0 briefly after pressing fire without a lock

        void Start()
        {
            if (route == null) route = FindAnyObjectByType<RaceRoute>();
            if (route != null) route.RingPassed += _ => ringFlash = 1f;

            if (plane != null) paper = plane.GetComponent<PaperDamage>();
            if (plane != null) planeInput = plane.GetComponent<PlaneInput>();
            folds = FindAnyObjectByType<FoldSelector>();
            if (paper != null) paper.Damaged += amount => { damageFlash = 1f; lastDamage = amount; };

            if (plane != null) gun = plane.GetComponent<StapleGun>();
            if (plane != null) pencils = plane.GetComponent<PencilLauncher>();
            if (pencils != null) pencils.FireRefused += () => noLockFlash = 1f;
            CombatScore.AnyHit += OnAnyHit;
            CombatScore.KillScored += OnKill;
        }

        void OnDestroy()
        {
            // Static events outlive this object: always unsubscribe.
            CombatScore.AnyHit -= OnAnyHit;
            CombatScore.KillScored -= OnKill;
            if (boss != null) { boss.Damaged -= OnBossDamaged; boss.Defeated -= OnBossDefeated; }
        }

        void OnAnyHit(PlaneController shooter, GameObject victim) { if (shooter == plane) hitFlash = 1f; }
        void OnKill(int kills) => killFlash = 1f;

        static readonly Color Green = new Color(0.45f, 1f, 0.35f);
        static readonly Color Yellow = new Color(1f, 0.82f, 0.3f);
        static readonly Color Red = new Color(1f, 0.35f, 0.3f);
        static readonly Color Cyan = new Color(0.4f, 0.85f, 1f);

        void Update()
        {
            // Debug-only shortcut, read straight from the keyboard.
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) showPanel = !showPanel;
            ringFlash = Mathf.MoveTowards(ringFlash, 0f, Time.deltaTime * 1.5f);
            damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.deltaTime * 1.5f);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 4f);
            killFlash = Mathf.MoveTowards(killFlash, 0f, Time.deltaTime * 0.8f);
            noLockFlash = Mathf.MoveTowards(noLockFlash, 0f, Time.deltaTime * 1.5f);
            bossHitFlash = Mathf.MoveTowards(bossHitFlash, 0f, Time.deltaTime * 2.5f);
            bossDefeatFlash = Mathf.MoveTowards(bossDefeatFlash, 0f, Time.deltaTime * 0.3f);
        }

        float HeightAboveSurface()
        {
            // Ignore trigger zones; a raycast doesn't hit the collider it starts inside.
            if (Physics.Raycast(plane.transform.position, Vector3.down, out RaycastHit hit, 2000f, floorMask, QueryTriggerInteraction.Ignore))
                return hit.distance;
            return float.NaN;
        }

        void OnGUI()
        {
            if (plane == null) return;
            scale = Screen.height / 1080f;
            EnsureStyles();

            if (showPanel) DrawPanel();
            if (route != null && route.RingCount > 0)
            {
                DrawRoutePanel();
                DrawNextTargetPointer();
            }
            DrawCombat();
            DrawControlsHint();
            DrawCenter();
        }

        // ------------------------------------------------------------------ route

        void DrawRoutePanel()
        {
            Rect panel = new Rect(Screen.width - 290 * scale, 10 * scale, 280 * scale, 104 * scale);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = panel.x + 12 * scale, y = panel.y + 8 * scale, w = panel.width - 24 * scale, line = 26 * scale;
            string rings = route.AllRingsPassed
                ? $"<color=#60FF80><b>All {route.RingCount} rings!</b></color>  land on the mug"
                : $"Rings  <b>{route.NextIndex} / {route.RingCount}</b>";
            Label(ref y, x, w, line, rings);
            Label(ref y, x, w, line, $"Time   <b>{plane.FlightTime:0.00}s</b>");
            string foldTag = route.BestTimeFold.Length > 0 ? $"  <color=#999>({route.BestTimeFold})</color>" : "";
            Label(ref y, x, w, line, (route.BestTime > 0f ? $"Best   {route.BestTime:0.00}s" : "Best   —") + foldTag);

            // "+RING" flash under the panel.
            if (ringFlash > 0f)
            {
                GUI.color = new Color(1f, 0.9f, 0.3f, ringFlash);
                GUI.Label(new Rect(panel.x, panel.yMax + 4 * scale, panel.width, 40 * scale), $"<size={Px(30)}><b>+RING</b></size>", big);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Points at the next ring (or the mug once all rings are done): a marker over it when it's
        /// on screen, an arrow at the screen edge when it isn't. Readability over realism!
        /// </summary>
        void DrawNextTargetPointer()
        {
            if (plane.State != PlaneState.Flying && plane.State != PlaneState.Held) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 target;
            string label;
            if (!route.AllRingsPassed) { target = route.NextRing.transform.position; label = $"{route.NextIndex + 1}"; }
            else
            {
                if (landingZone == null) landingZone = FindAnyObjectByType<LandingZone>();
                if (landingZone == null) return;
                target = landingZone.SurfaceCenter;
                label = "LAND";
            }

            float dist = Vector3.Distance(plane.transform.position, target);
            string color = route.AllRingsPassed ? "#60FF80" : "#FFE040";
            DrawWorldMarker(cam, target, color, $"<b>{label}</b>  {dist:0}", $"{label}  {dist:0}");
        }

        /// <summary>
        /// Shared marker logic: text over a world point when it's on screen, or an arrow clamped
        /// to the screen edge pointing at it when it isn't. Returns the on-screen GUI position (or null).
        /// </summary>
        Vector2? DrawWorldMarker(Camera cam, Vector3 world, string color, string onScreenText, string arrowText, float yOffset = -60f)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            // IMGUI's y axis points down; screen space points up.
            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
            if (behind) gui = new Vector2(Screen.width - gui.x, Screen.height - gui.y);   // mirror when behind

            float margin = 50 * scale;
            bool onScreen = !behind && gui.x > margin && gui.x < Screen.width - margin && gui.y > margin && gui.y < Screen.height - margin;

            if (onScreen)
            {
                if (onScreenText != null)
                    GUI.Label(new Rect(gui.x - 80 * scale, gui.y + yOffset * scale, 160 * scale, 30 * scale),
                              $"<size={Px(20)}><color={color}>{onScreenText}</color></size>", big);
                return gui;
            }

            // Clamp to the screen edge and draw an arrow pointing toward the target.
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 dir = (gui - center).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            float sx = (center.x - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
            float sy = (center.y - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
            Vector2 edge = center + dir * Mathf.Min(sx, sy);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, edge);
            GUI.Label(new Rect(edge.x - 30 * scale, edge.y - 20 * scale, 60 * scale, 40 * scale),
                      $"<size={Px(34)}><color={color}><b>>></b></color></size>", big);
            GUI.matrix = old;
            GUI.Label(new Rect(edge.x - 60 * scale, edge.y + 14 * scale, 120 * scale, 26 * scale),
                      $"<size={Px(16)}><color={color}>{arrowText}</color></size>", big);
            return null;
        }

        // ------------------------------------------------------------------ reticle

        Texture2D ringTex, dotTex;
        Vector2 reticlePos;       // smoothed screen position (GUI coords)
        float reticleSize;        // smoothed ring diameter (pixels)
        bool reticleValid;

        /// <summary>
        /// Aim reticle:
        ///  - Free aim: a white ring where your nose points (30 units ahead), with your staple count under it.
        ///  - Locked: when aim assist has a rival, the ring turns red, shrinks and slides onto the LEAD point
        ///    (where staples will actually go), brackets frame the rival, and "LOCK" appears.
        ///  - Pulses on hits; turns grey with "EMPTY" when you're out of staples.
        /// </summary>
        void DrawReticle(Camera cam)
        {
            if (ringTex == null) ringTex = MakeRing(128, 7f);
            if (dotTex == null) dotTex = MakeRing(32, 16f);   // thick ring = filled dot

            Vector3 sp = cam.WorldToScreenPoint(gun.AimPoint);
            if (sp.z <= 0f) { reticleValid = false; return; }
            Vector2 target = new Vector2(sp.x, Screen.height - sp.y);

            bool locked = gun.LockTarget != null;
            bool empty = !gun.Unlimited && gun.Ammo == 0;
            float wantSize = (locked ? 34f : 48f) * scale * (1f + 0.35f * hitFlash);

            // Smooth movement so the ring glides onto a lock instead of teleporting.
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            reticlePos = reticleValid ? Vector2.Lerp(reticlePos, target, k) : target;
            reticleSize = reticleValid ? Mathf.Lerp(reticleSize, wantSize, k) : wantSize;
            reticleValid = true;

            Color c = empty ? new Color(0.6f, 0.6f, 0.6f, 0.6f)
                    : locked ? new Color(1f, 0.32f, 0.25f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.8f);
            if (hitFlash > 0f) c = Color.Lerp(c, Color.white, hitFlash);

            // Ring + centre dot.
            GUI.color = c;
            GUI.DrawTexture(new Rect(reticlePos.x - reticleSize * 0.5f, reticlePos.y - reticleSize * 0.5f, reticleSize, reticleSize), ringTex);
            float dot = 6f * scale;
            GUI.DrawTexture(new Rect(reticlePos.x - dot * 0.5f, reticlePos.y - dot * 0.5f, dot, dot), dotTex);

            // Brackets around the locked rival itself (the ring sits on the lead point, ahead of it).
            if (locked)
            {
                Vector3 tp = cam.WorldToScreenPoint(gun.LockTarget.Position);
                if (tp.z > 0f)
                {
                    float dist = Vector3.Distance(cam.transform.position, gun.LockTarget.Position);
                    float half = Mathf.Clamp(900f / dist, 14f, 60f) * scale;
                    DrawBrackets(new Vector2(tp.x, Screen.height - tp.y), half, c);
                }
            }
            GUI.color = Color.white;

            // Text under the ring: LOCK / HIT / EMPTY / ammo count.
            string text;
            if (hitFlash > 0.3f) text = "<color=#FFFFFF><b>HIT</b></color>";
            else if (empty) text = "<color=#FF6050><b>EMPTY</b></color>";
            else if (locked) text = $"<color=#FF6050><b>LOCK</b></color>  {AmmoText()}";
            else text = AmmoText();
            GUI.Label(new Rect(reticlePos.x - 80 * scale, reticlePos.y + reticleSize * 0.5f + 2 * scale, 160 * scale, 24 * scale),
                      $"<size={Px(15)}>{text}</size>", big);
        }

        string AmmoText() => gun.Unlimited ? "" : $"<color=#DDDDDD>{gun.Ammo}</color>";

        /// <summary>
        /// Mouse-aim marker: a small circle with four ticks where the MOUSE is aiming. The bigger
        /// reticle ring shows where the NOSE points; watch it slide onto this marker as the plane turns.
        /// </summary>
        void DrawMouseAimMarker(Camera cam)
        {
            if (ringTex == null) ringTex = MakeRing(128, 7f);
            Vector3 sp = cam.WorldToScreenPoint(plane.transform.position + planeInput.AimDirection * 100f);
            if (sp.z <= 0f) return;
            Vector2 c = new Vector2(sp.x, Screen.height - sp.y);

            float size = 22f * scale, tick = 8f * scale, t = Mathf.Max(2f, 2f * scale);
            GUI.color = new Color(0.6f, 1f, 0.9f, 0.9f);
            GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), ringTex);
            // Ticks pointing in from the four sides.
            float r = size * 0.5f;
            GUI.DrawTexture(new Rect(c.x - t * 0.5f, c.y - r - tick, t, tick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - t * 0.5f, c.y + r, t, tick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - r - tick, c.y - t * 0.5f, tick, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x + r, c.y - t * 0.5f, tick, t), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------ boss

        StaplerBoss boss;
        float bossHitFlash, lastBossHit, bossDefeatFlash;
        bool lastBossHitWeak;

        void HookBoss()
        {
            if (boss != null || StaplerBoss.Active == null) return;
            boss = StaplerBoss.Active;
            boss.Damaged += OnBossDamaged;
            boss.Defeated += OnBossDefeated;
        }

        void OnBossDamaged(float dealt, bool weak) { bossHitFlash = 1f; lastBossHit = dealt; lastBossHitWeak = weak; }
        void OnBossDefeated() => bossDefeatFlash = 1f;

        /// <summary>
        /// Boss health bar across the top: name, phase markers, and a status line that teaches the
        /// fight ("ARMORED", "WEAK SPOT OPEN!", "SLAM INCOMING!"). Hit pops show x2 vs CLINK.
        /// </summary>
        void DrawBossBar()
        {
            HookBoss();
            if (boss == null || boss.Settings == null) return;
            BossSettings bs = boss.Settings;

            bool near = Vector3.Distance(plane.transform.position, boss.transform.position) < bs.aggroRange * 1.3f;
            if (bossDefeatFlash > 0f)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01(bossDefeatFlash * 2f));
                GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 70 * scale), $"<size={Px(56)}><b>BOSS DEFEATED!</b></size>", big);
                GUI.color = Color.white;
            }
            if (boss.IsDefeated || !(boss.IsAwake || near)) return;

            float w = 620 * scale;
            float x = (Screen.width - w) * 0.5f, y = 64 * scale;
            GUI.Label(new Rect(x, y, w, 30 * scale),
                      $"<size={Px(22)}><b><color=#FF9030>{bs.displayName}</color></b>  <color=#BBB>{bs.title}</color>   <color=#888>phase {boss.Phase}</color></size>", big);
            y += 30 * scale;

            Color barColor = boss.WeakSpotOpen ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.4f, 0.2f);
            if (bossHitFlash > 0f) barColor = Color.Lerp(barColor, Color.white, bossHitFlash * 0.7f);
            // Phase markers show where the fight escalates.
            Bar(ref y, x, w, boss.Health01, barColor, bs.phase2At, Color.white, bs.phase3At, Color.white, 14);

            string status;
            if (boss.Waking)
                status = "<color=#FF9030><b>IT'S WAKING UP...</b></color>";
            else if (boss.SlamWarning)
                status = $"<color=#FF4030><b>SLAM INCOMING!  get clear!</b></color>";
            else if (boss.WeakSpotOpen)
                status = $"<color=#FFD040><b>WEAK SPOT OPEN!  x{bs.weakSpotMultiplier:0}</b></color>";
            else
                status = "<color=#AAAAAA>ARMORED: dodge, then hit it when the jaw opens</color>";
            GUI.Label(new Rect(x, y, w, 26 * scale), $"<size={Px(17)}>{status}</size>", big);

            // Hit pop to the right of the bar.
            if (bossHitFlash > 0f)
            {
                string pop = lastBossHitWeak ? $"<color=#FFD040><b>-{Mathf.RoundToInt(lastBossHit)}  x2!</b></color>" : "<color=#999999><b>CLINK</b></color>";
                GUI.color = new Color(1f, 1f, 1f, bossHitFlash);
                GUI.Label(new Rect(x + w + 10 * scale, 90 * scale - 20 * scale * (1f - bossHitFlash), 200 * scale, 30 * scale),
                          $"<size={Px(22)}>{pop}</size>", panelText);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Pencil lock-on: a yellow box that closes in on the rival while locking, then snaps
        /// tight and turns red with "LOCKED" when you can fire. "NO LOCK" flashes if you fire too early.
        /// </summary>
        void DrawPencilLock(Camera cam)
        {
            if (pencils.Candidate != null)
            {
                Vector3 tp = cam.WorldToScreenPoint(pencils.Candidate.Position);
                if (tp.z > 0f)
                {
                    Vector2 c = new Vector2(tp.x, Screen.height - tp.y);
                    float p = pencils.LockProgress;
                    bool locked = pencils.IsLocked;

                    // Box shrinks from wide to tight as the lock builds.
                    float half = Mathf.Lerp(110f, 34f, p * p) * scale;
                    Color col = locked
                        ? new Color(1f, 0.25f, 0.2f, 0.6f + 0.4f * Mathf.Sin(Time.time * 18f))   // pulsing red
                        : new Color(1f, 0.85f, 0.2f, 0.9f);
                    DrawBox(c, half, col, Mathf.Max(2f, 2f * scale));
                    GUI.color = Color.white;

                    string label = locked
                        ? $"<color=#FF5040><b>LOCKED</b></color>  <color=#DDD>G / RMB / LT</color>"
                        : $"<color=#FFD040>LOCKING {Mathf.RoundToInt(p * 100)}%</color>";
                    GUI.Label(new Rect(c.x - 140 * scale, c.y - half - 28 * scale, 280 * scale, 24 * scale),
                              $"<size={Px(16)}>{label}</size>", big);
                }
            }

            if (noLockFlash > 0f)
            {
                string why = pencils.PencilsLeft == 0 ? "NO PENCILS LEFT" : "NO LOCK";
                GUI.color = new Color(1f, 1f, 1f, noLockFlash);
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 30 * scale),
                          $"<size={Px(22)}><color=#FFB040><b>{why}</b></color></size>", big);
                GUI.color = Color.white;
            }
        }

        /// <summary>Square outline (four thin bars) centred on a screen point.</summary>
        void DrawBox(Vector2 center, float half, Color color, float thickness)
        {
            GUI.color = color;
            float size = half * 2f;
            GUI.DrawTexture(new Rect(center.x - half, center.y - half, size, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - half, center.y + half - thickness, size, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - half, center.y - half, thickness, size), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x + half - thickness, center.y - half, thickness, size), Texture2D.whiteTexture);
        }

        void DrawBrackets(Vector2 center, float half, Color color)
        {
            float len = half * 0.45f, t = Mathf.Max(2f, 2.5f * scale);
            GUI.color = color;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector2 corner = center + new Vector2(sx * half, sy * half);
                // Horizontal and vertical arms pointing inward from each corner.
                GUI.DrawTexture(new Rect(sx > 0 ? corner.x - len : corner.x, corner.y - t * 0.5f, len, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(corner.x - t * 0.5f, sy > 0 ? corner.y - len : corner.y, t, len), Texture2D.whiteTexture);
            }
        }

        /// <summary>A soft-edged white ring texture, generated once at runtime (no art asset needed).</summary>
        static Texture2D MakeRing(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f - 1f, mid = r - thickness * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01(1f - (Mathf.Abs(d - mid) - thickness * 0.5f + 1f) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ combat

        /// <summary>Red markers + health bars on rival planes, an aim reticle, kills, HIT / SHOT DOWN pops.</summary>
        void DrawCombat()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Enemy markers (only while you're flying or about to throw): rivals in red, the boss in orange.
            if (plane.State == PlaneState.Flying || plane.State == PlaneState.Held)
            {
                foreach (var t in CombatTarget.All)
                {
                    if (t.IsPlayerSide || !t.IsAlive) continue;
                    float dist = Vector3.Distance(plane.transform.position, t.Position);
                    string color = t.IsBoss ? "#FF9030" : "#FF5040";
                    string tag = t.IsBoss ? "BOSS" : "RIVAL";
                    Vector2? gui = DrawWorldMarker(cam, t.Position, color, $"[  ]  {dist:0}", $"{tag}  {dist:0}", -14f);
                    if (gui.HasValue && !t.IsBoss)   // boss health lives in the big bar at the top
                    {
                        float bw = 60 * scale, by = gui.Value.y + 18 * scale;
                        Bar(ref by, gui.Value.x - bw * 0.5f, bw, t.Health01, Red, 0f, Color.clear, 0f, Color.clear, 5);
                    }
                }
            }

            DrawBossBar();

            if (plane.State == PlaneState.Flying && gun != null) DrawReticle(cam);
            if (plane.State == PlaneState.Flying && planeInput != null && planeInput.IsMouseAim) DrawMouseAimMarker(cam);
            if (plane.State == PlaneState.Flying && pencils != null) DrawPencilLock(cam);
            else reticleValid = false;

            // Kills counter (top-right, under the route panel if there is one).
            float ky = (route != null && route.RingCount > 0 ? 120 : 10) * scale;
            GUI.Label(new Rect(Screen.width - 290 * scale, ky, 280 * scale, 30 * scale),
                      $"<size={Px(20)}>Rivals shot down: <b>{CombatScore.Kills}</b></size>", panelText);

            if (killFlash > 0f)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f, killFlash);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60 * scale), $"<size={Px(48)}><b>SHOT DOWN!</b></size>", big);
                GUI.color = Color.white;
            }
        }

        // ------------------------------------------------------------------ panel

        void DrawPanel()
        {
            FlightSettings s = plane.Settings;
            bool moving = plane.State == PlaneState.Flying;
            Vector3 v = moving ? plane.Velocity : Vector3.zero;
            float h = HeightAboveSurface();
            float boostCd = plane.BoostCooldownRemaining;

            Rect panel = R(10, 10, 440, 438);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = panel.x + 12 * scale, y = panel.y + 8 * scale, w = panel.width - 24 * scale;
            float line = 22 * scale;

            Label(ref y, x, w, line, $"<b>PAPER WINGS</b>  dev HUD   <color=#999>(F1 hide)</color>");
            Label(ref y, x, w, line, $"State: <b>{plane.State}</b>    Flight: {plane.FlightTime:0.0}s");

            // Airspeed bar with stall (red) and cruise (white) marks.
            Label(ref y, x, w, line, $"Airspeed  <b>{plane.Speed:0.0}</b>   ground {v.magnitude:0.0}");
            Color speedColor = plane.IsStalling ? Red : (plane.Speed < s.cruiseSpeed ? Yellow : Green);
            Bar(ref y, x, w, moving ? plane.Speed / s.maxSpeed : 0f, speedColor,
                s.stallSpeed / s.maxSpeed, Red, s.cruiseSpeed / s.maxSpeed, Color.white);

            // Vertical speed: centered bar, green = climbing, red = sinking fast.
            Label(ref y, x, w, line, $"Vertical  <b>{v.y:+0.0;-0.0}</b>");
            CenteredBar(ref y, x, w, Mathf.Clamp(v.y / 10f, -1f, 1f), v.y >= 0.2f ? Green : (v.y <= -3f ? Red : Yellow));

            Label(ref y, x, w, line, $"Height    <b>{(float.IsNaN(h) ? "—" : h.ToString("0.0"))}</b> above surface   alt {plane.transform.position.y:0.0}");
            Label(ref y, x, w, line, $"Pitch / Bank  {plane.PitchAngle:0}° / {plane.BankAngle:0}°");

            // Boost cooldown.
            Label(ref y, x, w, line, boostCd <= 0f ? "Boost  <color=#7CFC00><b>READY</b></color>" : $"Boost  {boostCd:0.0}s");
            Bar(ref y, x, w, 1f - boostCd / Mathf.Max(0.01f, s.boostCooldown), boostCd <= 0f ? Green : Cyan);

            // Staples (refill when back in hand).
            if (gun != null && gun.Settings != null && !gun.Unlimited)
            {
                int clip = gun.Settings.clipSize;
                string ammoColor = gun.Ammo == 0 ? "#FF6050" : "white";
                Label(ref y, x, w, line, $"Staples  <color={ammoColor}><b>{gun.Ammo} / {clip}</b></color>   <color=#999>(F / LMB / X)</color>");
            }

            // Pencil missiles left (shown as pencil marks: lit = mounted, dim = fired).
            if (pencils != null && pencils.MaxPencils > 0)
            {
                string marks = new string('I', pencils.PencilsLeft);
                string used = new string('I', pencils.MaxPencils - pencils.PencilsLeft);
                Label(ref y, x, w, line, $"Pencils  <b><color=#FFD040>{marks}</color><color=#555>{used}</color></b>   <color=#999>(lock on, then G / RMB / LT)</color>");
            }

            // Paper condition: integrity (green -> red) and wetness (blue).
            if (paper != null)
            {
                float p = paper.Integrity01;
                float wet = paper.Wetness01;
                string wetText = wet > 0.01f ? $"   <color=#7FB2FF>wet {Mathf.RoundToInt(wet * 100)}%</color>" : "";
                Label(ref y, x, w, line, $"Paper  <b>{Mathf.RoundToInt(p * 100)}%</b>{wetText}");
                Color pc = p > 0.6f ? Green : (p > 0.3f ? Yellow : Red);
                if (damageFlash > 0f) pc = Color.Lerp(pc, Color.white, damageFlash);
                Bar(ref y, x, w, p, pc);
                if (wet > 0.01f) Bar(ref y, x, w, wet, new Color(0.5f, 0.7f, 1f), 0f, Color.clear, 0f, Color.clear, 5);
            }

            var air = plane.Air;
            string airText = !air.InThermal && !air.InDraft && !air.InDowndraft ? "calm" :
                (air.InThermal ? "<color=#FFA040>THERMAL</color> " : "") +
                (air.InDraft ? "<color=#60D8FF>DRAFT</color> " : "") +
                (air.InDowndraft ? "<color=#A08CFF>DOWNDRAFT</color>" : "");
            Label(ref y, x, w, line, $"Air  {airText}   {plane.AirVelocity.magnitude:0.0} (up {plane.AirVelocity.y:+0.0;-0.0})");
            Label(ref y, x, w, line, plane.LandingAssistStrength > 0.01f
                ? $"Assist  <color=#60FF80>landing {Mathf.RoundToInt(plane.LandingAssistStrength * 100)}%</color>"
                : "Assist  —");
        }

        void DrawControlsHint()
        {
            string pitchHint = planeInput != null && planeInput.InvertPitch ? "W climbs" : "W dives";
            string controls = planeInput != null && planeInput.IsMouseAim
                ? $"<b>MOUSE AIM</b>    Mouse: steer    W/S A/D: manual override    LMB: staples    RMB: pencil (lock first)    SPACE: throw / boost    R: reset    C: classic controls    Esc: free cursor"
                : $"<b>CLASSIC</b>    W/S or stick: pitch ({pitchHint})    A/D: bank & turn    SPACE / Shift / (A) / RT: boost    F / LMB / (X): staples    G / RMB / LT: pencil    R / (Y): reset    C: mouse aim    F1: HUD";
            GUI.Label(new Rect(10 * scale, Screen.height - 32 * scale, Screen.width, 30 * scale), controls, small);
        }

        // ------------------------------------------------------------------ center messages

        void DrawCenter()
        {
            Rect center = new Rect(0, Screen.height * 0.18f, Screen.width, 60 * scale);
            switch (plane.State)
            {
                case PlaneState.Held:
                {
                    bool charging = launcher != null && launcher.IsCharging;
                    string msg = charging ? "Release to throw!" : "Hold SPACE / (A) to wind up, release to throw.  Steer to aim.";
                    GUI.Label(center, $"<size={Px(30)}>{msg}</size>", big);
                    if (charging)
                    {
                        float bw = 360 * scale;
                        float y = center.yMax + 8 * scale;
                        Bar(ref y, (Screen.width - bw) * 0.5f, bw, launcher.Charge01,
                            Color.Lerp(Yellow, Green, launcher.Charge01), 0f, Color.clear, 0f, Color.clear, 14);
                    }
                    if (folds != null && folds.Current != null && !charging) DrawFoldCard(folds.Current);
                    break;
                }

                case PlaneState.Flying:
                {
                    var air = plane.Air;
                    string zone = "";
                    if (air.InThermal) zone += $"<color=#FFA040>^ THERMAL {Mathf.RoundToInt(air.thermal * 100)}%</color>     ";
                    if (air.InDraft)   zone += $"<color=#60D8FF>>> DRAFT {Mathf.RoundToInt(air.draft * 100)}%</color>     ";
                    if (air.InDowndraft) zone += $"<color=#A08CFF>v DOWNDRAFT {Mathf.RoundToInt(air.downdraft * 100)}%</color>";
                    if (zone.Length > 0)
                        GUI.Label(new Rect(0, 12 * scale, Screen.width, 50 * scale), $"<size={Px(30)}>{zone}</size>", big);

                    if (plane.StallAmount > 0.01f)
                    {
                        float a = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                        GUI.color = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.4f, 1f, a) * plane.StallAmount);
                        GUI.Label(center, "STALL!  nose dropping to regain speed", big);
                        GUI.color = Color.white;
                    }

                    // Damage pop: "RIP! -14" floats up and fades.
                    if (damageFlash > 0f)
                    {
                        GUI.color = new Color(1f, 0.45f, 0.35f, damageFlash);
                        Rect pop = new Rect(0, center.yMax + (20f + 40f * (1f - damageFlash)) * scale, Screen.width, 40 * scale);
                        GUI.Label(pop, $"<size={Px(30)}><b>RIP!  -{Mathf.RoundToInt(lastDamage)}</b></size>", big);
                        GUI.color = Color.white;
                    }

                    // Gentle hint while soggy.
                    if (paper != null && paper.Wetness01 > 0.5f)
                        GUI.Label(new Rect(0, Screen.height * 0.72f, Screen.width, 30 * scale),
                                  $"<size={Px(20)}><color=#7FB2FF>Soggy paper! Dry off in the laptop's heat</color></size>", big);
                    break;
                }

                case PlaneState.Down:
                {
                    string color = plane.LastDownWasCrash ? "#FF6347" : "#FFD24A";
                    GUI.Label(center, $"<color={color}>{plane.LastResultMessage}</color>", big);
                    break;
                }

                case PlaneState.Landed:
                {
                    float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f);
                    GUI.Label(center, $"<size={Mathf.RoundToInt(56 * scale * pulse)}><color=#60FF80>{plane.LastResultMessage}</color></size>", big);
                    Rect sub = new Rect(0, center.yMax + 10 * scale, Screen.width, 40 * scale);
                    GUI.Label(sub, $"<size={Px(24)}>Flight time {plane.FlightTime:0.0}s     —     SPACE / (A) to fly again</size>", big);
                    if (route != null && route.LastResult != null)
                    {
                        string rc = route.LastWasNewBest ? "#FFE040" : (route.AllRingsPassed ? "#FFFFFF" : "#BBBBBB");
                        Rect res = new Rect(0, sub.yMax + 6 * scale, Screen.width, 44 * scale);
                        GUI.Label(res, $"<size={Px(30)}><color={rc}>{route.LastResult}</color></size>", big);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Fold picker card (bottom-centre while the plane is in hand): name, blurb, and
        /// stat bars so you can compare folds at a glance.
        /// </summary>
        void DrawFoldCard(PlaneFold fold)
        {
            float w = 460 * scale, h = 250 * scale;
            Rect card = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.58f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = card.x + 16 * scale, y = card.y + 10 * scale, iw = card.width - 32 * scale;
            string arrows = folds.Count > 1 ? "<color=#888><   </color>" : "";
            string arrowsR = folds.Count > 1 ? "<color=#888>   ></color>" : "";
            GUI.Label(new Rect(card.x, y, card.width, 40 * scale),
                      $"<size={Px(30)}>{arrows}<b>{fold.displayName.ToUpper()}</b>{arrowsR}</size>", big);
            y += 40 * scale;
            GUI.Label(new Rect(x, y, iw, 44 * scale), $"<size={Px(16)}><color=#CCC>{fold.description}</color></size>",
                      new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperCenter });
            y += 48 * scale;

            // Stat bars, normalised against rough "max" values across folds.
            FlightSettings f = fold.flight;
            if (f != null)
            {
                StatBar(ref y, x, iw, "Speed", f.maxSpeed / 50f);
                StatBar(ref y, x, iw, "Glide", f.glideRatio / 15f);
                StatBar(ref y, x, iw, "Agility", f.turnRate / 120f);
            }
            if (fold.paper != null) StatBar(ref y, x, iw, "Paper", fold.paper.maxIntegrity / 150f);

            if (folds.Count > 1)
                GUI.Label(new Rect(card.x, card.yMax - 26 * scale, card.width, 24 * scale),
                          $"<size={Px(14)}><color=#999>Q / E   or   LB / RB   to change fold</color></size>", big);
        }

        void StatBar(ref float y, float x, float w, string label, float value01)
        {
            float labelW = 80 * scale;
            GUI.Label(new Rect(x, y - 6 * scale, labelW, 22 * scale), $"<size={Px(15)}>{label}</size>", panelText);
            float barY = y;
            Bar(ref barY, x + labelW, w - labelW, Mathf.Clamp01(value01), Cyan);
            y += 24 * scale;
        }

        // ------------------------------------------------------------------ drawing helpers

        void EnsureStyles()
        {
            if (panelText == null)
            {
                panelText = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleLeft };
                big = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { richText = true };
            }
            panelText.fontSize = Px(17);
            big.fontSize = Px(42);
            small.fontSize = Px(15);
        }

        int Px(float size) => Mathf.Max(1, Mathf.RoundToInt(size * scale));
        Rect R(float x, float y, float w, float h) => new Rect(x * scale, y * scale, w * scale, h * scale);

        void Label(ref float y, float x, float w, float line, string text)
        {
            GUI.Label(new Rect(x, y, w, line), text, panelText);
            y += line;
        }

        void Bar(ref float y, float x, float w, float fill01, Color color,
                 float markA = 0f, Color markAColor = default, float markB = 0f, Color markBColor = default, float height = 8)
        {
            float h = height * scale;
            Rect back = new Rect(x, y, w, h);
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill01), h), Texture2D.whiteTexture);
            if (markA > 0f) { GUI.color = markAColor; GUI.DrawTexture(new Rect(x + w * markA - 1, y - 2, 2, h + 4), Texture2D.whiteTexture); }
            if (markB > 0f) { GUI.color = markBColor; GUI.DrawTexture(new Rect(x + w * markB - 1, y - 2, 2, h + 4), Texture2D.whiteTexture); }
            GUI.color = Color.white;
            y += h + 6 * scale;
        }

        void CenteredBar(ref float y, float x, float w, float value, Color color)
        {
            float h = 8 * scale;
            float mid = x + w * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = color;
            float len = w * 0.5f * Mathf.Abs(value);
            GUI.DrawTexture(new Rect(value >= 0f ? mid : mid - len, y, len, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(mid - 1, y - 2, 2, h + 4), Texture2D.whiteTexture);
            y += h + 6 * scale;
        }
    }
}
