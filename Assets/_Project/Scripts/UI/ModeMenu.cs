using UnityEngine;
using UnityEngine.InputSystem;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// Mode select menu, results screen and in-flight objective strip (IMGUI, like the dev HUD).
    ///   - Opens at start. Esc / M / gamepad Start re-opens it (the game pauses while it's up).
    ///   - Navigate: Up/Down, W/S or D-pad. Choose: Enter, Space or (A). Shortcuts: 1-4. Or click.
    ///   - Results: Enter / Space / (A) = retry, Esc / M / (B) = back to the menu.
    ///
    /// UNITY NOTE: Time.timeScale = 0 pauses everything driven by game time (physics, Time.deltaTime),
    /// while Update and OnGUI keep running, so menus still work.
    /// </summary>
    public class ModeMenu : MonoBehaviour
    {
        [SerializeField] ModeDirector director;

        public static bool IsOpen { get; private set; }

        enum Screen { Menu, Results, None }
        Screen screen = Screen.Menu;
        int selected;
        GUIStyle title, body, small, button;
        float scale;

        static readonly GameMode[] Modes = { GameMode.FreeFlight, GameMode.RingRun, GameMode.Dogfight, GameMode.BossFight };

        void Start()
        {
            selected = Mathf.Clamp(PlayerPrefs.GetInt("PaperWings.LastMode", 1), 0, Modes.Length - 1);
            if (director != null) director.Completed += _ => Show(Screen.Results);
            Show(Screen.Menu);
        }

        void OnDestroy()
        {
            // Never leave the game paused or input blocked if this object goes away.
            Time.timeScale = 1f;
            SetBlocked(false);
        }

        void Show(Screen s)
        {
            screen = s;
            bool paused = s != Screen.None;
            Time.timeScale = paused ? 0f : 1f;
            SetBlocked(paused);
        }

        static void SetBlocked(bool blocked)
        {
            IsOpen = blocked;
            LaunchAndReset.InputBlocked = blocked;   // no accidental throws from menu keys
            PlaneInput.CursorBlocked = blocked;      // free the mouse while a menu is up
        }

        void Choose(int index)
        {
            selected = index;
            Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
            director.StartMode(Modes[index]);
            Show(Screen.None);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (director == null) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool menuKey = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame))
                           || (pad != null && pad.startButton.wasPressedThisFrame);
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                           || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = pad != null && pad.buttonEast.wasPressedThisFrame;

            switch (screen)
            {
                case Screen.None:
                    if (menuKey) Show(Screen.Menu);
                    break;

                case Screen.Results:
                    if (confirm) Choose(selected = System.Array.IndexOf(Modes, director.Mode));
                    else if (menuKey || back) Show(Screen.Menu);
                    break;

                case Screen.Menu:
                    bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
                    bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
                    if (up) selected = (selected + Modes.Length - 1) % Modes.Length;
                    if (down) selected = (selected + 1) % Modes.Length;
                    if (up || down) Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
                    if (kb != null)
                    {
                        if (kb.digit1Key.wasPressedThisFrame) { Choose(0); break; }
                        if (kb.digit2Key.wasPressedThisFrame) { Choose(1); break; }
                        if (kb.digit3Key.wasPressedThisFrame) { Choose(2); break; }
                        if (kb.digit4Key.wasPressedThisFrame) { Choose(3); break; }
                    }
                    if (confirm) Choose(selected);
                    else if ((menuKey || back) && director.HasMode && !director.RunComplete) Show(Screen.None);   // resume
                    break;
            }
        }

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (director == null) return;
            scale = UnityEngine.Screen.height / 1080f;
            EnsureStyles();
            GUI.depth = -10;   // draw on top of the dev HUD

            switch (screen)
            {
                case Screen.Menu: DrawMenu(); break;
                case Screen.Results: DrawResults(); break;
                case Screen.None: DrawObjective(); break;
            }
        }

        void DrawMenu()
        {
            Dim(0.65f);
            float w = 760 * scale, h = 640 * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.85f);

            float y = panel.y + 24 * scale;
            GUI.Label(new Rect(panel.x, y, w, 50 * scale), "PAPER WINGS", title);
            y += 52 * scale;
            GUI.Label(new Rect(panel.x, y, w, 30 * scale), "<color=#BBBBBB>The Night Shift War</color>", body);
            y += 50 * scale;

            for (int i = 0; i < Modes.Length; i++)
            {
                GameMode m = Modes[i];
                Rect card = new Rect(panel.x + 40 * scale, y, w - 80 * scale, 92 * scale);
                bool sel = i == selected;
                GUI.color = sel ? new Color(1f, 0.85f, 0.3f, 0.35f) : new Color(1f, 1f, 1f, 0.06f);
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string best = director.BestTime(m) > 0f ? $"Best {director.BestTime(m):0.0}s" : "";
                if (m == GameMode.RingRun && director.Route != null && director.Route.BestTime > 0f) best = $"Best {director.Route.BestTime:0.00}s";
                GUI.Label(new Rect(card.x + 18 * scale, card.y + 8 * scale, card.width, 36 * scale),
                          $"<b>{i + 1}.  {ModeDirector.ModeName(m).ToUpper()}</b>", button);
                GUI.Label(new Rect(card.x + 18 * scale, card.y + 44 * scale, card.width - 200 * scale, 44 * scale),
                          $"<color=#CCCCCC>{Describe(m)}</color>", small);
                GUI.Label(new Rect(card.xMax - 190 * scale, card.y + 12 * scale, 175 * scale, 30 * scale),
                          $"<color=#FFD040>{best}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperRight });

                // Mouse support: click a card to start it.
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) Choose(i);
                y += 102 * scale;
            }

            string resume = director.HasMode && !director.RunComplete ? "    Esc / M: resume" : "";
            GUI.Label(new Rect(panel.x, panel.yMax - 44 * scale, w, 30 * scale),
                      $"<color=#999999>Up/Down or W/S, Enter / Space to start, or 1-4, or click.{resume}</color>", small);
        }

        void DrawResults()
        {
            var r = director.LastResult;
            if (r == null) { screen = Screen.Menu; return; }
            Dim(0.5f);
            float w = 720 * scale, h = 300 * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.85f);

            float y = panel.y + 34 * scale;
            GUI.Label(new Rect(panel.x, y, w, 60 * scale), $"<color=#60FF80>{r.title}</color>", title);
            y += 70 * scale;
            GUI.Label(new Rect(panel.x, y, w, 34 * scale), r.detail, body);
            y += 44 * scale;
            if (r.newBest) GUI.Label(new Rect(panel.x, y, w, 40 * scale), "<color=#FFD040><b>NEW BEST!</b></color>", body);

            GUI.Label(new Rect(panel.x, panel.yMax - 50 * scale, w, 30 * scale),
                      "<color=#BBBBBB>Enter / Space / (A): retry      Esc / M / (B): change mode</color>", small);
        }

        /// <summary>Small strip above the controls hint: current mode, objective, timer, best.</summary>
        void DrawObjective()
        {
            if (!director.HasMode) return;
            GameMode m = director.Mode;
            string obj = m switch
            {
                GameMode.FreeFlight => "Free flight: no objective. Ride the air, practise landings.",
                GameMode.RingRun => "Fly through every ring, then land on the mug.",
                GameMode.Dogfight => $"Rivals shot down  <b>{director.KillsThisRun} / {director.DogfightTarget}</b>",
                _ => "Defeat THE STAPLER. Hit the glowing mouth when the jaw opens."
            };
            string timer = m == GameMode.Dogfight || m == GameMode.BossFight ? $"    Time <b>{director.RunTime:0.0}s</b>" : "";
            float best = director.BestTime(m);
            string bestText = (m == GameMode.Dogfight || m == GameMode.BossFight) && best > 0f ? $"    <color=#AAAAAA>Best {best:0.0}s</color>" : "";

            float w = 900 * scale;
            Rect r = new Rect((UnityEngine.Screen.width - w) * 0.5f, UnityEngine.Screen.height - 72 * scale, w, 32 * scale);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, $"<color=#FFD040>{ModeDirector.ModeName(m).ToUpper()}</color>    {obj}{timer}{bestText}    <color=#888888>Esc: menu</color>", small);
        }

        static string Describe(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Just you, the desk and the air. No enemies, no clock.",
            GameMode.RingRun => "Thread all five sticky-note rings, then land on the coffee mug. Race the clock.",
            GameMode.Dogfight => "Accounting's rival planes are up. Shoot down five of them as fast as you can.",
            _ => "THE STAPLER guards the mug. Dodge its staple fans and slams, hit the open jaw.",
        };

        // ------------------------------------------------------------------ helpers

        void EnsureStyles()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
                body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, richText = true };
            }
            title.fontSize = Mathf.RoundToInt(44 * scale);
            body.fontSize = Mathf.RoundToInt(22 * scale);
            small.fontSize = Mathf.RoundToInt(16 * scale);
            button.fontSize = Mathf.RoundToInt(24 * scale);
        }

        Rect Centered(float w, float h) => new Rect((UnityEngine.Screen.width - w) * 0.5f, (UnityEngine.Screen.height - h) * 0.5f, w, h);

        static void Dim(float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Box(Rect r, float alpha)
        {
            GUI.color = new Color(0.08f, 0.09f, 0.13f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
