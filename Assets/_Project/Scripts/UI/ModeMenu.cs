using UnityEngine;
using UnityEngine.InputSystem;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// Main menu, campaign night select, unlock shop, results screen and in-flight objective strip
    /// (IMGUI, like the dev HUD).
    ///   - Opens at start. Esc / M / gamepad Start re-opens it (the game pauses while it's up).
    ///   - Navigate: Up/Down, W/S or D-pad. Choose: Enter, Space or (A). Shortcuts: 1-6. Or click.
    ///   - Results: Enter / Space / (A) = next night or retry, R / (X) = retry, Esc / M / (B) = back.
    ///
    /// UNITY NOTE: Time.timeScale = 0 pauses everything driven by game time (physics, Time.deltaTime),
    /// while Update and OnGUI keep running, so menus still work.
    /// </summary>
    public class ModeMenu : MonoBehaviour
    {
        [SerializeField] ModeDirector director;

        public static bool IsOpen { get; private set; }

        enum Screen { Menu, Results, None, Unlocks, Campaign }
        int shopSelected;
        string shopMessage = "";
        int nightSelected;
        Screen screen = Screen.Menu;
        int selected;
        GUIStyle title, body, small, button;
        float scale;

        static readonly GameMode[] Modes = { GameMode.FreeFlight, GameMode.RingRun, GameMode.Dogfight, GameMode.Squadron, GameMode.BossFight };

        CampaignSettings Campaign => director != null ? director.Campaign : null;
        /// <summary>1 if the menu starts with the Campaign card, else 0. Mode cards come after it.</summary>
        int Offset => Campaign != null && Campaign.Count > 0 ? 1 : 0;
        int EntryCount => Modes.Length + Offset;

        void Start()
        {
            // The saved value is the mode itself; find where it sits in the menu list.
            int mode = System.Array.IndexOf(Modes, (GameMode)PlayerPrefs.GetInt("PaperWings.LastMode", (int)GameMode.RingRun));
            // New players land on the campaign; returning players on what they played last.
            selected = Offset == 1 && !PlayerPrefs.HasKey("PaperWings.LastMode") ? 0 : Mathf.Max(0, mode) + Offset;
            nightSelected = FirstUnclearedNight();
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
            if (index < Offset)
            {
                nightSelected = FirstUnclearedNight();
                Show(Screen.Campaign);
                return;
            }
            director.StartMode(Modes[index - Offset]);
            Show(Screen.None);
        }

        void PlayNight(int index)
        {
            if (!Progression.NightOpen(index)) { Audio.AudioFx.PlayUi(Audio.Sfx.NoLock); return; }
            nightSelected = index;
            Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
            director.StartNight(index);
            Show(Screen.None);
        }

        /// <summary>The first night not yet cleared (or the last one if they're all done).</summary>
        int FirstUnclearedNight()
        {
            if (Campaign == null) return 0;
            for (int i = 0; i < Campaign.Count; i++) if (!Progression.NightCleared(i)) return i;
            return Mathf.Max(0, Campaign.Count - 1);
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
            bool unlocksKey = (kb != null && kb.uKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            bool retryKey = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);

            switch (screen)
            {
                case Screen.None:
                    if (menuKey) Show(Screen.Menu);   // Esc again resumes
                    break;

                case Screen.Results:
                    UpdateResults(confirm, retryKey, menuKey || back);
                    break;

                case Screen.Menu:
                    if (up) selected = (selected + EntryCount - 1) % EntryCount;
                    if (down) selected = (selected + 1) % EntryCount;
                    if (up || down) Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
                    if (kb != null)
                    {
                        int digit = DigitPressed(kb);
                        if (digit >= 1 && digit <= EntryCount) { Choose(digit - 1); break; }
                    }
                    if (confirm) Choose(selected);
                    else if ((menuKey || back) && director.HasMode && !director.RunComplete) Show(Screen.None);   // resume
                    else if (unlocksKey && director.ProgressionConfig != null) OpenUnlocks();
                    break;

                case Screen.Campaign:
                    UpdateCampaign(up, down, confirm, menuKey || back, unlocksKey);
                    break;

                case Screen.Unlocks:
                    UpdateUnlocks(up, down, confirm, menuKey || back || unlocksKey);
                    break;
            }
        }

        static int DigitPressed(Keyboard kb)
        {
            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame) return 5;
            if (kb.digit6Key.wasPressedThisFrame) return 6;
            return 0;
        }

        void OpenUnlocks()
        {
            shopMessage = "";
            Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
            Show(Screen.Unlocks);
        }

        // ------------------------------------------------------------------ results

        /// <summary>Night that Enter plays from the results screen: the next one after a clear, else this one again.</summary>
        int NextNightAfter(ModeResult r)
        {
            if (r.failed || Campaign == null) return r.night;
            int next = r.night + 1;
            return next < Campaign.Count && Progression.NightOpen(next) ? next : r.night;
        }

        void UpdateResults(bool confirm, bool retry, bool leave)
        {
            var r = director.LastResult;
            if (r == null) { Show(Screen.Menu); return; }

            if (r.night >= 0)
            {
                if (retry) PlayNight(r.night);
                else if (confirm) PlayNight(NextNightAfter(r));
                else if (leave) { nightSelected = NextNightAfter(r); Show(Screen.Campaign); }
            }
            else
            {
                int entry = System.Array.IndexOf(Modes, director.Mode) + Offset;
                if (confirm || retry) Choose(entry);
                else if (leave) Show(Screen.Menu);
            }
        }

        // ------------------------------------------------------------------ campaign

        void UpdateCampaign(bool up, bool down, bool confirm, bool leave, bool unlocksKey)
        {
            if (Campaign == null || Campaign.Count == 0 || leave) { Show(Screen.Menu); return; }
            int n = Campaign.Count;
            if (up) nightSelected = (nightSelected + n - 1) % n;
            if (down) nightSelected = (nightSelected + 1) % n;
            if (up || down) Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
            nightSelected = Mathf.Clamp(nightSelected, 0, n - 1);
            if (confirm) PlayNight(nightSelected);
            else if (unlocksKey && director.ProgressionConfig != null) OpenUnlocks();
        }

        static string StarText(int stars)
        {
            int s = Mathf.Clamp(stars, 0, 3);
            return $"<color=#FFD040>{new string('★', s)}</color><color=#555555>{new string('☆', 3 - s)}</color>";
        }

        string RewardText(CampaignNight night)
        {
            string unlock = "";
            if (!string.IsNullOrEmpty(night.rewardUnlockId))
            {
                var item = director.ProgressionConfig != null ? director.ProgressionConfig.Find(night.rewardUnlockId) : null;
                unlock = $"{(item != null ? item.displayName : night.rewardUnlockId)} + ";
            }
            return $"{unlock}{night.rewardClips} clips";
        }

        static string StarTimes(CampaignNight night)
            => night.threeStarTime > 0f ? $"   <color=#555555>(★★ under {night.twoStarTime:0}s, ★★★ under {night.threeStarTime:0}s)</color>" : "";

        /// <summary>The Night Shift: every night in order, its stars, and the selected night's memo.</summary>
        void DrawCampaign()
        {
            var c = Campaign;
            if (c == null) { screen = Screen.Menu; return; }

            Dim(0.7f);
            float w = 880 * scale, rowStep = 50 * scale;
            float h = (330 + c.Count * 50) * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.9f);

            float y = panel.y + 18 * scale;
            GUI.Label(new Rect(panel.x, y, w, 44 * scale), "THE NIGHT SHIFT", title);
            y += 46 * scale;
            GUI.Label(new Rect(panel.x, y, w, 26 * scale),
                      $"<color=#FFD040>★ {Progression.TotalStars()} / {c.Count * 3}</color>     <color=#FFD040>{Progression.PaperClips} paper clips</color>", small);
            y += 36 * scale;

            for (int i = 0; i < c.Count; i++)
            {
                var night = c.Get(i);
                bool open = Progression.NightOpen(i);
                int stars = Progression.GetNightStars(i);
                Rect row = new Rect(panel.x + 30 * scale, y, w - 60 * scale, rowStep - 6 * scale);

                GUI.color = i == nightSelected ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string nameColor = !open ? "#666666" : (stars > 0 ? "#FFFFFF" : "#FFE080");
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 6 * scale, 330 * scale, 32 * scale),
                          $"<color={nameColor}><b>NIGHT {i + 1}</b>   {night.title}</color>", button);
                string goal = open ? night.Goal(director.DefaultDogfightTarget, director.DefaultSquadronTarget) : "";
                GUI.Label(new Rect(row.x + 340 * scale, row.y + 10 * scale, row.width - 470 * scale, 28 * scale),
                          $"<color=#AAAAAA>{goal}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperLeft, wordWrap = false, clipping = TextClipping.Clip });
                string right = !open ? "<color=#666666>LOCKED</color>" : (stars > 0 ? StarText(stars) : "<color=#FFE080><b>NEW</b></color>");
                GUI.Label(new Rect(row.xMax - 124 * scale, row.y + 8 * scale, 110 * scale, 30 * scale), right,
                          new GUIStyle(body) { alignment = TextAnchor.UpperRight });

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    if (i == nightSelected) PlayNight(i);   // click to select, click again to fly
                    else { nightSelected = i; Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
                }
                y += rowStep;
            }

            // Briefing for the selected night, on a yellow memo slip.
            var sel = c.Get(nightSelected);
            if (sel != null)
            {
                y += 6 * scale;
                Rect memo = new Rect(panel.x + 30 * scale, y, w - 60 * scale, 130 * scale);
                GUI.color = new Color(1f, 0.97f, 0.82f, 0.92f);
                GUI.DrawTexture(memo, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var memoStyle = new GUIStyle(small) { alignment = TextAnchor.UpperLeft };
                string text = Progression.NightOpen(nightSelected) ? sel.memo : "Clear the previous night to read this memo.";
                GUI.Label(new Rect(memo.x + 16 * scale, memo.y + 10 * scale, memo.width - 32 * scale, 76 * scale),
                          $"<color=#2A2A2A>{text}</color>", memoStyle);
                string reward = Progression.NightCleared(nightSelected)
                    ? $"Cleared. Best {StarText(Progression.GetNightStars(nightSelected))}{StarTimes(sel)}"
                    : $"<b>Reward:</b> {RewardText(sel)}{StarTimes(sel)}";
                GUI.Label(new Rect(memo.x + 16 * scale, memo.yMax - 38 * scale, memo.width - 32 * scale, 30 * scale),
                          $"<color=#5A4A10>{reward}</color>", memoStyle);
            }

            GUI.Label(new Rect(panel.x, panel.yMax - 40 * scale, w, 28 * scale),
                      "<color=#999999>Up/Down, Enter / Space / (A) to fly (or click twice).    U / (Y): unlocks    Esc / (B): back</color>", small);
        }

        // ------------------------------------------------------------------ unlocks

        void UpdateUnlocks(bool up, bool down, bool confirm, bool leave)
        {
            var shop = director.ProgressionConfig != null ? director.ProgressionConfig.Shop() : null;
            if (shop == null || shop.Length == 0 || leave) { Show(Screen.Menu); return; }

            if (up) shopSelected = (shopSelected + shop.Length - 1) % shop.Length;
            if (down) shopSelected = (shopSelected + 1) % shop.Length;
            if (up || down) { Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); shopMessage = ""; }
            shopSelected = Mathf.Clamp(shopSelected, 0, shop.Length - 1);
            if (confirm) Buy(shop[shopSelected]);
        }

        void Buy(UnlockItem item)
        {
            var result = Progression.TryBuy(item);
            switch (result)
            {
                case Progression.BuyResult.Bought:
                    shopMessage = $"<color=#60FF80>Unlocked {item.displayName}!</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.Victory, 0.7f);
                    break;
                case Progression.BuyResult.AlreadyOwned:
                    shopMessage = "<color=#AAAAAA>Already yours.</color>";
                    break;
                case Progression.BuyResult.NeedsRequirement:
                    var req = director.ProgressionConfig.Find(item.requires);
                    shopMessage = $"<color=#FF9050>Unlock {(req != null ? req.displayName : item.requires)} first.</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
                    break;
                case Progression.BuyResult.TooExpensive:
                    shopMessage = $"<color=#FF9050>Not enough paper clips ({Progression.PaperClips} / {item.cost}).</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
                    break;
            }
        }

        /// <summary>The unlock shop: every upgrade with its price and status. Earn clips by playing modes.</summary>
        void DrawUnlocks()
        {
            var cfg = director.ProgressionConfig;
            if (cfg == null) { screen = Screen.Menu; return; }
            var shop = cfg.Shop();

            Dim(0.7f);
            float w = 780 * scale, rowH = 64 * scale;
            float h = (190 + shop.Length * 70) * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.9f);

            float y = panel.y + 20 * scale;
            GUI.Label(new Rect(panel.x, y, w, 44 * scale), "UNLOCKS", title);
            y += 48 * scale;
            GUI.Label(new Rect(panel.x, y, w, 30 * scale), $"<color=#FFD040><b>{Progression.PaperClips}</b> paper clips</color>", body);
            y += 40 * scale;

            for (int i = 0; i < shop.Length; i++)
            {
                var item = shop[i];
                Rect row = new Rect(panel.x + 30 * scale, y, w - 60 * scale, rowH);
                bool sel = i == shopSelected;
                var state = Progression.CanBuy(item);
                GUI.color = sel ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string nameColor = state == Progression.BuyResult.AlreadyOwned ? "#80FF90" : (state == Progression.BuyResult.NeedsRequirement ? "#777777" : "#FFFFFF");
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 4 * scale, row.width, 30 * scale), $"<b><color={nameColor}>{item.displayName}</color></b>", button);
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 34 * scale, row.width - 200 * scale, 28 * scale),
                          $"<color=#BBBBBB>{item.description}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperLeft });

                string status = state switch
                {
                    Progression.BuyResult.AlreadyOwned => "<color=#80FF90><b>OWNED</b></color>",
                    Progression.BuyResult.NeedsRequirement => $"<color=#888888>needs {(cfg.Find(item.requires)?.displayName ?? item.requires)}</color>",
                    Progression.BuyResult.TooExpensive => $"<color=#FF9050>{item.cost} clips</color>",
                    _ => $"<color=#FFD040><b>{item.cost} clips</b></color>"
                };
                GUI.Label(new Rect(row.xMax - 210 * scale, row.y + 6 * scale, 196 * scale, 30 * scale), status,
                          new GUIStyle(small) { alignment = TextAnchor.UpperRight });

                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { shopSelected = i; Buy(item); }
                y += 70 * scale;
            }

            GUI.Label(new Rect(panel.x, panel.yMax - 72 * scale, w, 28 * scale), shopMessage, body);
            GUI.Label(new Rect(panel.x, panel.yMax - 40 * scale, w, 28 * scale),
                      "<color=#999999>Up/Down, Enter / Space / (A) or click to unlock.    U / Esc / (B): back</color>", small);
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
                case Screen.Unlocks: DrawUnlocks(); break;
                case Screen.Campaign: DrawCampaign(); break;
            }
        }

        void DrawMenu()
        {
            Dim(0.65f);
            float w = 760 * scale, h = (230 + EntryCount * 102) * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.85f);

            float y = panel.y + 24 * scale;
            GUI.Label(new Rect(panel.x, y, w, 50 * scale), "PAPER WINGS", title);
            y += 52 * scale;
            GUI.Label(new Rect(panel.x, y, w, 30 * scale), "<color=#BBBBBB>The Night Shift War</color>", body);
            y += 50 * scale;

            for (int i = 0; i < EntryCount; i++)
            {
                Rect card = new Rect(panel.x + 40 * scale, y, w - 80 * scale, 92 * scale);
                bool sel = i == selected;
                bool isCampaign = i < Offset;
                GUI.color = sel ? new Color(1f, 0.85f, 0.3f, 0.35f) : (isCampaign ? new Color(1f, 0.85f, 0.3f, 0.12f) : new Color(1f, 1f, 1f, 0.06f));
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string heading, description, right;
                if (isCampaign)
                {
                    int next = FirstUnclearedNight();
                    bool allDone = Progression.NightCleared(Campaign.Count - 1);
                    heading = "CAMPAIGN: THE NIGHT SHIFT";
                    description = allDone ? "Every night cleared. Go back for three stars on each."
                                          : $"Next up: Night {next + 1}, {Campaign.Get(next).title}. Missions with twists and rewards.";
                    right = $"★ {Progression.TotalStars()} / {Campaign.Count * 3}";
                }
                else
                {
                    GameMode m = Modes[i - Offset];
                    heading = ModeDirector.ModeName(m).ToUpper();
                    description = Describe(m);
                    right = director.BestTime(m) > 0f ? $"Best {director.BestTime(m):0.0}s" : "";
                    if (m == GameMode.RingRun && director.Route != null && director.Route.BestTime > 0f) right = $"Best {director.Route.BestTime:0.00}s";
                    int medal = Progression.GetMedal(m);
                    if (medal > 0) right = $"<color={ProgressionSettings.MedalColor(medal)}>{ProgressionSettings.MedalName(medal)}</color>   " + right;
                }

                GUI.Label(new Rect(card.x + 18 * scale, card.y + 8 * scale, card.width, 36 * scale), $"<b>{i + 1}.  {heading}</b>", button);
                GUI.Label(new Rect(card.x + 18 * scale, card.y + 44 * scale, card.width - 200 * scale, 44 * scale),
                          $"<color=#CCCCCC>{description}</color>", small);
                GUI.Label(new Rect(card.xMax - 190 * scale, card.y + 12 * scale, 175 * scale, 30 * scale),
                          $"<color=#FFD040>{right}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperRight });

                // Mouse support: click a card to start it.
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) Choose(i);
                y += 102 * scale;
            }

            string resume = director.HasMode && !director.RunComplete ? "    Esc / M: resume" : "";
            GUI.Label(new Rect(panel.x, panel.yMax - 44 * scale, w, 30 * scale),
                      $"<color=#999999>Up/Down or W/S, Enter / Space to start, or 1-{EntryCount}, or click.{resume}</color>", small);

            // Paper clips + the way into the unlock shop.
            if (director.ProgressionConfig != null)
            {
                GUI.Label(new Rect(panel.xMax - 260 * scale, panel.y + 18 * scale, 240 * scale, 28 * scale),
                          $"<color=#FFD040><b>{Progression.PaperClips}</b> paper clips</color>", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
                Rect btn = new Rect(panel.xMax - 260 * scale, panel.y + 46 * scale, 240 * scale, 30 * scale);
                GUI.color = new Color(1f, 0.85f, 0.3f, 0.25f);
                GUI.DrawTexture(btn, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(btn, "<b>U / (Y): UNLOCKS</b>", small);
                if (GUI.Button(btn, GUIContent.none, GUIStyle.none)) OpenUnlocks();
            }
        }

        void DrawResults()
        {
            var r = director.LastResult;
            if (r == null) { screen = Screen.Menu; return; }
            bool night = r.night >= 0;
            Dim(0.5f);
            float w = 760 * scale, h = 440 * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.85f);

            float y = panel.y + 26 * scale;
            if (night && Campaign != null && Campaign.Get(r.night) != null)
            {
                GUI.Label(new Rect(panel.x, y, w, 28 * scale), $"<color=#BBBBBB>NIGHT {r.night + 1}: {Campaign.Get(r.night).title.ToUpper()}</color>", small);
                y += 30 * scale;
            }
            GUI.Label(new Rect(panel.x, y, w, 60 * scale), $"<color={(r.failed ? "#FF6050" : "#60FF80")}>{r.title}</color>", title);
            y += 64 * scale;
            GUI.Label(new Rect(panel.x, y, w, 34 * scale), r.detail, body);
            y += 44 * scale;
            if (r.newBest) { GUI.Label(new Rect(panel.x, y, w, 40 * scale), "<color=#FFD040><b>NEW BEST!</b></color>", body); y += 40 * scale; }

            if (night && !r.failed)
            {
                var starStyle = new GUIStyle(title) { fontSize = Mathf.RoundToInt(40 * scale) };
                GUI.Label(new Rect(panel.x, y, w, 46 * scale), StarText(r.stars), starStyle);
                y += 46 * scale;
                if (r.newStars && r.stars > 1) { GUI.Label(new Rect(panel.x, y, w, 26 * scale), "<color=#FFD040>new best!</color>", small); y += 26 * scale; }
                if (!string.IsNullOrEmpty(r.reward)) { GUI.Label(new Rect(panel.x, y, w, 34 * scale), $"<color=#80FF90><b>{r.reward}</b></color>", body); y += 36 * scale; }
                if (r.campaignComplete) { GUI.Label(new Rect(panel.x, y, w, 34 * scale), "<color=#FFD040><b>THE NIGHT SHIFT WAR IS WON!</b></color>", body); y += 36 * scale; }
            }

            // Medal (free play only).
            if (r.medal > 0)
            {
                string medalText = $"<color={ProgressionSettings.MedalColor(r.medal)}><b>{ProgressionSettings.MedalName(r.medal).ToUpper()} MEDAL</b></color>"
                                   + (r.newMedal ? "  <color=#FFD040>(new!)</color>" : "");
                GUI.Label(new Rect(panel.x, y, w, 36 * scale), medalText, body);
                y += 38 * scale;
            }
            if (r.clipsEarned > 0)
                GUI.Label(new Rect(panel.x, y, w, 34 * scale),
                          $"<color=#FFD040>+{r.clipsEarned} paper clips</color>   <color=#999999>({Progression.PaperClips} total, U in the menu to spend)</color>", small);

            string hint;
            if (!night) hint = "Enter / Space / (A): retry      Esc / M / (B): change mode";
            else if (r.failed) hint = "Enter / Space / (A): try again      Esc / M / (B): campaign";
            else if (NextNightAfter(r) != r.night) hint = $"Enter / Space / (A): Night {r.night + 2}      R / (X): replay      Esc / M / (B): campaign";
            else hint = "Enter / Space / (A): replay      Esc / M / (B): campaign";
            GUI.Label(new Rect(panel.x, panel.yMax - 46 * scale, w, 30 * scale), $"<color=#BBBBBB>{hint}</color>", small);
        }

        /// <summary>Small strip above the controls hint: current mode or night, objective, timer, best.</summary>
        void DrawObjective()
        {
            if (!director.HasMode) return;
            GameMode m = director.Mode;
            var night = director.Night;
            string obj = m switch
            {
                GameMode.FreeFlight => night != null ? "Land on the coffee mug." : "Free flight: no objective. Ride the air, practise landings.",
                GameMode.RingRun => "Fly through every ring, then land on the mug.",
                GameMode.Dogfight => $"Rivals shot down  <b>{director.KillsThisRun} / {director.DogfightTarget}</b>",
                GameMode.Squadron => $"Squadron kills  <b>{director.TeamKillsThisRun} / {director.SquadronTarget}</b>   <color=#AAAAAA>(yours {director.KillsThisRun})</color>",
                _ => "Defeat THE STAPLER. Hit the glowing mouth when the jaw opens."
            };
            bool timed = m == GameMode.Dogfight || m == GameMode.Squadron || m == GameMode.BossFight;
            string timer = timed ? $"    Time <b>{director.RunTime:0.0}s</b>" : "";
            float best = director.BestTime(m);
            string bestText = timed && best > 0f && night == null ? $"    <color=#AAAAAA>Best {best:0.0}s</color>" : "";

            string label = ModeDirector.ModeName(m).ToUpper();
            if (night != null)
            {
                label = $"NIGHT {director.NightIndex + 1}: {night.title.ToUpper()}";
                float left = director.TimeLeft;
                if (left >= 0f) timer = $"    Time left <b><color={(left < 10f ? "#FF6050" : "#FFFFFF")}>{left:0.0}s</color></b>";
                if (night.noPencils) obj += "  <color=#FF9050>No pencils</color>";
                if (night.windScale > 1.05f) obj += "  <color=#80C8FF>Rough air</color>";
            }

            float w = 980 * scale;
            Rect r = new Rect((UnityEngine.Screen.width - w) * 0.5f, UnityEngine.Screen.height - 72 * scale, w, 32 * scale);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, $"<color=#FFD040>{label}</color>    {obj}{timer}{bestText}    <color=#888888>Esc: menu</color>", small);
        }

        static string Describe(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Just you, the desk and the air. No enemies, no clock.",
            GameMode.RingRun => "Thread all five sticky-note rings, then land on the coffee mug. Race the clock.",
            GameMode.Dogfight => "Accounting's rival planes are up. Shoot down five of them as fast as you can.",
            GameMode.Squadron => "Fly with two Marketing wingmen against four rivals. Your squadron needs eight kills.",
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
