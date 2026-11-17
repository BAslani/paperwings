using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// Developer HUD drawn with IMGUI (OnGUI). Ugly but zero setup. Not the final UI.
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

        GUIStyle box, big;

        float HeightAboveFloor()
        {
            Vector3 origin = plane.transform.position;
            // Ignore trigger zones (wind volumes etc.); raycasts don't hit the collider they start inside.
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f, floorMask, QueryTriggerInteraction.Ignore))
                return hit.distance;
            return float.NaN;
        }

        string AirText()
        {
            var air = plane.Air;
            Vector3 w = plane.AirVelocity;
            if (!air.InThermal && !air.InDraft) return $"calm ({w.magnitude:0.0})";
            string s = "";
            if (air.InThermal) s += "<color=#FFA040>THERMAL</color> ";
            if (air.InDraft)   s += "<color=#60D8FF>DRAFT</color> ";
            return s + $"{w.magnitude:0.0}  (up {w.y:+0.0;-0.0})";
        }

        void OnGUI()
        {
            if (plane == null) return;
            float scale = Screen.height / 1080f;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
            }
            box.fontSize = Mathf.RoundToInt(18 * scale);
            big.fontSize = Mathf.RoundToInt(42 * scale);

            FlightSettings s = plane.Settings;
            Vector3 v = plane.State == PlaneState.Held ? Vector3.zero : plane.Velocity;
            float h = HeightAboveFloor();
            float boostCd = plane.BoostCooldownRemaining;

            string vsColor = v.y >= 0.2f ? "#7CFC00" : (v.y <= -3f ? "#FF6347" : "white");
            string text =
                $"<b>PAPER WINGS — dev HUD</b>\n" +
                $"State:      {plane.State}\n" +
                $"Airspeed:   {plane.Speed:0.0}   (cruise {s.cruiseSpeed:0}, stall {s.stallSpeed:0})\n" +
                $"Ground spd: {v.magnitude:0.0}\n" +
                $"Height:     {(float.IsNaN(h) ? "—" : h.ToString("0.0"))} above surface   (altitude {plane.transform.position.y:0.0})\n" +
                $"Vert speed: <color={vsColor}>{v.y:+0.0;-0.0}</color>\n" +
                $"Pitch/Bank: {plane.PitchAngle:0}° / {plane.BankAngle:0}°\n" +
                $"Boost:      {(boostCd <= 0f ? "<color=#7CFC00>READY</color>" : $"{boostCd:0.0}s")}\n" +
                $"Air:        {AirText()}\n" +
                $"Assist:     {(plane.LandingAssistStrength > 0.01f ? $"<color=#60FF80>landing {Mathf.RoundToInt(plane.LandingAssistStrength * 100)}%</color>" : "—")}\n" +
                $"Flight:     {plane.FlightTime:0.0}s";

            GUI.Box(new Rect(10 * scale, 10 * scale, 520 * scale, 280 * scale), text, box);

            string pitchHint = s.invertPitch ? "W climbs" : "W dives";
            string controls = $"W/S or stick: pitch ({pitchHint})   A/D: bank & turn   SPACE/Shift/(A)/RT: boost   R/(Y)/Select: reset";
            GUI.Label(new Rect(10 * scale, Screen.height - 34 * scale, Screen.width, 30 * scale),
                      $"<size={Mathf.RoundToInt(16 * scale)}>{controls}</size>", new GUIStyle(GUI.skin.label) { richText = true });

            Rect center = new Rect(0, Screen.height * 0.18f, Screen.width, 60 * scale);
            switch (plane.State)
            {
                case PlaneState.Held:
                    string msg = launcher != null && launcher.IsCharging
                        ? $"Winding up… {Mathf.RoundToInt(launcher.Charge01 * 100)}%  — release to throw!"
                        : "Hold SPACE / (A) to wind up, release to throw.  Steer to aim.";
                    GUI.Label(center, $"<size={Mathf.RoundToInt(28 * scale)}>{msg}</size>", big);
                    break;

                case PlaneState.Flying:
                    // Zone banner just under the top of the screen, so you always know WHY you're climbing.
                    var air = plane.Air;
                    string zone = "";
                    if (air.InThermal) zone += $"<color=#FFA040>^ THERMAL {Mathf.RoundToInt(air.thermal * 100)}%</color>   ";
                    if (air.InDraft)   zone += $"<color=#60D8FF>>> DRAFT {Mathf.RoundToInt(air.draft * 100)}%</color>";
                    if (zone.Length > 0)
                        GUI.Label(new Rect(0, 12 * scale, Screen.width, 50 * scale),
                                  $"<size={Mathf.RoundToInt(30 * scale)}>{zone}</size>", big);

                    if (plane.StallAmount > 0.01f)
                    {
                        // Flashing warning.
                        float a = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                        GUI.color = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.4f, 1f, a) * plane.StallAmount);
                        GUI.Label(center, "STALL!  nose dropping to regain speed", big);
                        GUI.color = Color.white;
                    }
                    break;

                case PlaneState.Down:
                    string color = plane.LastDownWasCrash ? "#FF6347" : "#FFD24A";
                    GUI.Label(center, $"<color={color}>{plane.LastResultMessage}</color>", big);
                    break;

                case PlaneState.Landed:
                    // The payoff: big, happy, with a couple of stats to brag about.
                    float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f);
                    GUI.Label(center, $"<size={Mathf.RoundToInt(56 * scale * pulse)}><color=#60FF80>{plane.LastResultMessage}</color></size>", big);
                    Rect sub = new Rect(0, center.yMax + 10 * scale, Screen.width, 40 * scale);
                    GUI.Label(sub, $"<size={Mathf.RoundToInt(24 * scale)}>Flight time {plane.FlightTime:0.0}s     —     SPACE / (A) to fly again</size>", big);
                    break;
            }
        }
    }
}
