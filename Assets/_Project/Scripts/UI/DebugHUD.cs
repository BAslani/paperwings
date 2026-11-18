using UnityEngine;
using UnityEngine.InputSystem;
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
        float damageFlash;      // >0 briefly after taking damage
        float lastDamage;

        void Start()
        {
            if (route == null) route = FindAnyObjectByType<RaceRoute>();
            if (route != null) route.RingPassed += _ => ringFlash = 1f;

            if (plane != null) paper = plane.GetComponent<PaperDamage>();
            if (paper != null) paper.Damaged += amount => { damageFlash = 1f; lastDamage = amount; };
        }

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
            Label(ref y, x, w, line, route.BestTime > 0f ? $"Best   {route.BestTime:0.00}s" : "Best   —");

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
            Vector3 sp = cam.WorldToScreenPoint(target);
            bool behind = sp.z < 0f;
            // IMGUI's y axis points down; screen space points up.
            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
            if (behind) gui = new Vector2(Screen.width - gui.x, Screen.height - gui.y);   // mirror when behind

            float margin = 50 * scale;
            bool onScreen = !behind && gui.x > margin && gui.x < Screen.width - margin && gui.y > margin && gui.y < Screen.height - margin;
            string color = route.AllRingsPassed ? "#60FF80" : "#FFE040";

            if (onScreen)
            {
                GUI.Label(new Rect(gui.x - 80 * scale, gui.y - 60 * scale, 160 * scale, 30 * scale),
                          $"<size={Px(20)}><color={color}><b>{label}</b>  {dist:0}</color></size>", big);
                return;
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
                      $"<size={Px(16)}><color={color}>{label}  {dist:0}</color></size>", big);
        }

        // ------------------------------------------------------------------ panel

        void DrawPanel()
        {
            FlightSettings s = plane.Settings;
            bool moving = plane.State == PlaneState.Flying;
            Vector3 v = moving ? plane.Velocity : Vector3.zero;
            float h = HeightAboveSurface();
            float boostCd = plane.BoostCooldownRemaining;

            Rect panel = R(10, 10, 440, 390);
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
            string pitchHint = plane.Settings.invertPitch ? "W climbs" : "W dives";
            string controls = $"W/S or stick: pitch ({pitchHint})    A/D: bank & turn    SPACE / Shift / (A) / RT: boost    R / (Y) / Select: reset    F1: HUD";
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
