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

        enum Screen { Menu, Results, None, Unlocks, Campaign, Options }
        int roomSelected;   // free-play room picked on the main menu
        int optSelected;
        int shopSelected;
        string shopMessage = "";
        int nightSelected;
        Screen screen = Screen.Menu;
        int selected;
        GUIStyle title, body, small, button;
        float scale;

        // Boss fights are only played as campaign nights (no separate Boss Fight card).
        static readonly GameMode[] Modes = { GameMode.FreeFlight, GameMode.RingRun, GameMode.Dogfight, GameMode.Squadron };

        CampaignSettings Campaign => director != null ? director.Campaign : null;
        /// <summary>1 if the menu starts with the Campaign card, else 0. Mode cards come after it.</summary>
        /// <summary>Index of the Flight School card (0), or -1 if the scene has no Flight School.</summary>
        int TrainingEntry => director != null && director.HasTraining ? 0 : -1;
        /// <summary>Index of the campaign card, or -1.</summary>
        int CampaignEntry => Campaign != null && Campaign.Count > 0 ? (TrainingEntry + 1) : -1;
        /// <summary>Cards before the free-play modes (Flight School, Campaign).</summary>
        int Offset => (TrainingEntry >= 0 ? 1 : 0) + (CampaignEntry >= 0 ? 1 : 0);
        int EntryCount => Modes.Length + Offset;

        void Start()
        {
            // The saved value is the mode itself; find where it sits in the menu list.
            int mode = System.Array.IndexOf(Modes, (GameMode)PlayerPrefs.GetInt("PaperWings.LastMode", (int)GameMode.RingRun));
            // New players land on the campaign; returning players on what they played last.
            // New players land on Flight School (then the campaign); returning players on what they played last.
            if (TrainingEntry >= 0 && !ModeDirector.TrainingDone) selected = TrainingEntry;
            else if (CampaignEntry >= 0 && !PlayerPrefs.HasKey("PaperWings.LastMode")) selected = CampaignEntry;
            else selected = Mathf.Max(0, mode) + Offset;
            nightSelected = FirstUnclearedNight();
            if (director != null)
            {
                roomSelected = director.RoomIndex;
                director.Completed += _ => { roomSelected = director.RoomIndex; Show(Screen.Results); };
            }
            GameOptions.Init();   // saved volume, controls and brightness
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
            if (index == TrainingEntry)
            {
                Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
                director.StartTraining();
                Show(Screen.None);
                return;
            }
            if (index == CampaignEntry)
            {
                Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
                nightSelected = FirstUnclearedNight();
                Show(Screen.Campaign);
                return;
            }
            GameMode mode = Modes[index - Offset];
            if (!director.RoomSupports(roomSelected, mode)) { Audio.AudioFx.PlayUi(Audio.Sfx.NoLock); return; }   // e.g. no boss in the break room
            Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
            director.StartMode(mode, roomSelected);
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
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            bool optionsKey = (kb != null && kb.oKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);

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
                    if (left || right) ChangeRoom(right ? 1 : -1);
                    if (kb != null)
                    {
                        int digit = DigitPressed(kb);
                        if (digit >= 1 && digit <= EntryCount) { Choose(digit - 1); break; }
                    }
                    if (confirm) Choose(selected);
                    else if ((menuKey || back) && director.HasMode && !director.RunComplete) Show(Screen.None);   // resume
                    else if (unlocksKey && director.ProgressionConfig != null) OpenUnlocks();
                    else if (optionsKey) OpenOptions();
                    break;

                case Screen.Options:
                    UpdateOptions(up, down, left, right, confirm, menuKey || back || optionsKey);
                    break;

                case Screen.Campaign:
                    UpdateCampaign(up, down, confirm, menuKey || back, unlocksKey);
                    break;

                case Screen.Unlocks:
                    UpdateUnlocks(up, down, left, right, confirm, menuKey || back || unlocksKey);
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
            if (kb.digit7Key.wasPressedThisFrame) return 7;
            return 0;
        }

        void OpenUnlocks()
        {
            // Open on the tab of the fold you're holding.
            if (foldSelector == null) foldSelector = FindAnyObjectByType<FoldSelector>();
            int tab = foldSelector != null ? System.Array.IndexOf(ShopFolds(), foldSelector.Current) : -1;
            SetTab(Mathf.Max(0, tab));
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

            if (r.training)
            {
                if (retry) Choose(TrainingEntry);
                else if (confirm && Campaign != null && Campaign.Count > 0) PlayNight(FirstUnclearedNight());
                else if (confirm || leave) Show(Screen.Menu);
                return;
            }
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
                unlock = $"{(item != null ? item.LongName : night.rewardUnlockId)} + ";
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
                string where = !string.IsNullOrEmpty(night.place) ? night.place : (night.room > 0 ? director.RoomName(night.room) : "");
                if (open && where.Length > 0) goal = $"<color=#80C8FF>{where}:</color> {goal}";
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

        // The Unlocks screen has a tab per fold: each fold buys its own upgrades.
        int foldTab;
        FoldSelector foldSelector;

        PlaneFold[] ShopFolds()
        {
            var cfg = director.ProgressionConfig;
            if (cfg == null || cfg.folds == null) return new PlaneFold[0];
            return System.Array.FindAll(cfg.folds, f => f != null);
        }

        PlaneFold TabFold()
        {
            var folds = ShopFolds();
            if (folds.Length == 0) return null;
            foldTab = Mathf.Clamp(foldTab, 0, folds.Length - 1);
            return folds[foldTab];
        }

        void SetTab(int tab)
        {
            var folds = ShopFolds();
            if (folds.Length == 0) return;
            foldTab = (tab + folds.Length) % folds.Length;
            shopSelected = 0;
            shopMessage = "";
        }

        void UpdateUnlocks(bool up, bool down, bool left, bool right, bool confirm, bool leave)
        {
            var cfg = director.ProgressionConfig;
            if (cfg == null || leave) { Show(Screen.Menu); return; }
            if (left || right) { SetTab(foldTab + (right ? 1 : -1)); Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }

            // F / (X): fly the fold on this tab.
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) Equip(TabFold());

            var shop = cfg.ShopFor(TabFold());
            if (shop.Count == 0) return;
            if (up) shopSelected = (shopSelected + shop.Count - 1) % shop.Count;
            if (down) shopSelected = (shopSelected + 1) % shop.Count;
            if (up || down) { Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); shopMessage = ""; }
            shopSelected = Mathf.Clamp(shopSelected, 0, shop.Count - 1);
            if (confirm) Buy(shop[shopSelected]);
        }

        /// <summary>Makes the tab's fold the one in your hand (if it's unlocked and you're not mid-flight).</summary>
        void Equip(PlaneFold fold)
        {
            if (fold == null) return;
            if (foldSelector == null) foldSelector = FindAnyObjectByType<FoldSelector>();
            if (foldSelector == null) return;
            if (!Progression.IsUnlocked(fold.unlockId))
            {
                shopMessage = $"<color=#FF9050>Unlock the {fold.displayName} first.</color>";
                Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
            }
            else if (foldSelector.Select(fold))
            {
                shopMessage = $"<color=#60FF80>Flying the {fold.displayName}.</color>";
                Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
            }
            else
            {
                shopMessage = "<color=#FF9050>Land (or reset) first: you can only refold in hand.</color>";
                Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
            }
        }

        string NightName(int night)
        {
            var n = director.Campaign != null ? director.Campaign.Get(night) : null;
            return n != null ? $"Night {night + 1} ({n.title})" : $"Night {night + 1}";
        }

        string NameOf(string id)
        {
            var item = director.ProgressionConfig != null ? director.ProgressionConfig.Find(id) : null;
            return item != null ? item.LongName : id;
        }

        void Buy(UnlockItem item)
        {
            var result = Progression.TryBuy(item);
            switch (result)
            {
                case Progression.BuyResult.Bought:
                    shopMessage = $"<color=#60FF80>Unlocked {item.LongName}!</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.Victory, 0.7f);
                    break;
                case Progression.BuyResult.AlreadyOwned:
                    shopMessage = "<color=#AAAAAA>Already yours.</color>";
                    break;
                case Progression.BuyResult.NeedsBlueprint:
                    shopMessage = $"<color=#FF9050>No blueprint yet: clear {NightName(item.requiresNight)} in the campaign.</color>";
                    break;
                case Progression.BuyResult.NeedsRequirement:
                    shopMessage = $"<color=#FF9050>Unlock {NameOf(item.requires)} first.</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
                    break;
                case Progression.BuyResult.TooExpensive:
                    shopMessage = $"<color=#FF9050>Not enough paper clips ({Progression.PaperClips} / {item.cost}).</color>";
                    Audio.AudioFx.PlayUi(Audio.Sfx.NoLock);
                    break;
            }
        }

        PlanePreview preview;

        /// <summary>
        /// The hangar (Stage 28): a tab per fold. Left, the fold on a turntable with every upgrade it owns
        /// fitted, plus its stats; the upgrade selected in the list shows on the plane as a glowing blueprint.
        /// Right, the fold's shop. F / (X) flies the fold.
        /// </summary>
        void DrawUnlocks()
        {
            var cfg = director.ProgressionConfig;
            if (cfg == null) { screen = Screen.Menu; return; }
            var folds = ShopFolds();
            var fold = TabFold();
            var shop = cfg.ShopFor(fold);
            if (foldSelector == null) foldSelector = FindAnyObjectByType<FoldSelector>();
            if (preview == null) preview = FindAnyObjectByType<PlanePreview>();

            Dim(0.7f);
            float leftW = 440 * scale, rightW = 760 * scale, gap = 20 * scale;
            float w = leftW + rightW + gap + 60 * scale, rowH = 64 * scale;
            float h = Mathf.Max(290 + shop.Count * 70, 930) * scale;   // tall enough for the turntable column
            Rect panel = Centered(w, h);
            Box(panel, 0.9f);

            float y = panel.y + 20 * scale;
            GUI.Label(new Rect(panel.x, y, w, 44 * scale), "HANGAR", title);
            y += 48 * scale;
            GUI.Label(new Rect(panel.x, y, w, 30 * scale), $"<color=#FFD040><b>{Progression.PaperClips}</b> paper clips</color>", body);
            y += 42 * scale;

            // Fold tabs across the top.
            if (folds.Length > 0)
            {
                float tabW = (w - 60 * scale) / folds.Length;
                for (int t = 0; t < folds.Length; t++)
                {
                    Rect tab = new Rect(panel.x + 30 * scale + t * tabW, y, tabW - 6 * scale, 40 * scale);
                    bool open = Progression.IsUnlocked(folds[t].unlockId);
                    bool flying = foldSelector != null && foldSelector.Current == folds[t];
                    GUI.color = t == foldTab ? new Color(1f, 0.85f, 0.3f, 0.45f) : new Color(1f, 1f, 1f, 0.07f);
                    GUI.DrawTexture(tab, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    string label = open ? folds[t].displayName.ToUpper() : $"<color=#888888>{folds[t].displayName.ToUpper()} (locked)</color>";
                    if (flying) label += "  <color=#60FF80>✈</color>";
                    GUI.Label(tab, $"<b>{label}</b>", body);
                    if (GUI.Button(tab, GUIContent.none, GUIStyle.none)) SetTab(t);
                }
                y += 54 * scale;
            }

            float top = y;
            DrawHangarBay(new Rect(panel.x + 30 * scale, top, leftW, panel.yMax - 80 * scale - top), fold, shop);

            // The shop list on the right.
            float x = panel.x + 30 * scale + leftW + gap;
            for (int i = 0; i < shop.Count; i++)
            {
                var item = shop[i];
                Rect row = new Rect(x, y, rightW, rowH);
                bool sel = i == shopSelected;
                var state = Progression.CanBuy(item);
                GUI.color = sel ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string nameColor = state == Progression.BuyResult.AlreadyOwned ? "#80FF90" : (state == Progression.BuyResult.NeedsRequirement || state == Progression.BuyResult.NeedsBlueprint ? "#777777" : "#FFFFFF");
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 4 * scale, row.width, 30 * scale), $"<b><color={nameColor}>{item.displayName}</color></b>", button);
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 34 * scale, row.width - 220 * scale, 28 * scale),
                          $"<color=#BBBBBB>{item.description}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperLeft });

                string status = state switch
                {
                    Progression.BuyResult.AlreadyOwned => "<color=#80FF90><b>OWNED</b></color>",
                    Progression.BuyResult.NeedsRequirement => $"<color=#888888>needs {NameOf(item.requires)}</color>",
                    Progression.BuyResult.NeedsBlueprint => $"<color=#888888>blueprint: {NightName(item.requiresNight)}</color>\n<color=#666666>{item.cost} clips</color>",
                    Progression.BuyResult.TooExpensive => $"<color=#FF9050>{item.cost} clips</color>",
                    _ => $"<color=#FFD040><b>{item.cost} clips</b></color>"
                };
                GUI.Label(new Rect(row.xMax - 250 * scale, row.y + 6 * scale, 236 * scale, 56 * scale), status,
                          new GUIStyle(small) { alignment = TextAnchor.UpperRight });

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    if (i == shopSelected) Buy(item);   // click to look at it on the plane, click again to buy
                    else { shopSelected = i; shopMessage = ""; Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
                }
                y += 70 * scale;
            }

            GUI.Label(new Rect(panel.x, panel.yMax - 72 * scale, w, 28 * scale), shopMessage, body);
            GUI.Label(new Rect(panel.x, panel.yMax - 40 * scale, w, 28 * scale),
                      "<color=#999999>Left/Right: fold.   Up/Down: look at an upgrade.   Enter / (A) or click twice: buy.   F / (X): fly this fold.   Drag the plane to spin it.   U / Esc / (B): back</color>", small);
        }

        /// <summary>Left column of the hangar: the turntable picture, the fold's stats and the fly button.</summary>
        void DrawHangarBay(Rect area, PlaneFold fold, System.Collections.Generic.List<UnlockItem> shop)
        {
            if (fold == null) return;
            bool unlocked = Progression.IsUnlocked(fold.unlockId);

            // The 3D model: owned upgrades fitted, the selected (unowned) one as a blueprint.
            Rect pic = new Rect(area.x, area.y, area.width, area.width);
            if (preview != null)
            {
                var owned = PlanePreview.OwnedLoadout(fold);
                var ghost = default(PreviewLoadout);
                if (shopSelected >= 0 && shopSelected < shop.Count && Progression.CanBuy(shop[shopSelected]) != Progression.BuyResult.AlreadyOwned)
                    ghost = PlanePreview.LoadoutFor(shop[shopSelected].id);
                preview.Request(fold, owned, ghost);
                GUI.DrawTexture(pic, preview.Texture, ScaleMode.ScaleToFit, false);

                // Drag to spin.
                var e = Event.current;
                if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition)) { preview.Drag(-e.delta.x * 0.6f); e.Use(); }
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(pic, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (!unlocked)
                GUI.Label(new Rect(pic.x, pic.y + 10 * scale, pic.width, 30 * scale), "<color=#FF9050><b>LOCKED</b></color>", body);

            float y = pic.yMax + 8 * scale;
            GUI.Label(new Rect(area.x, y, area.width, 30 * scale), $"<b>{fold.displayName.ToUpper()}</b>", body);
            y += 30 * scale;
            GUI.Label(new Rect(area.x, y, area.width, 40 * scale), $"<color=#BBBBBB>{fold.description}</color>", small);
            y += 44 * scale;

            if (fold.flight != null)
            {
                StatLine(ref y, area, "Speed", fold.flight.maxSpeed / 50f);
                StatLine(ref y, area, "Glide", fold.flight.glideRatio / 15f);
                StatLine(ref y, area, "Agility", fold.flight.turnRate / 120f);
            }
            if (fold.paper != null)
                StatLine(ref y, area, "Paper", fold.paper.maxIntegrity * (Progression.IsUnlocked(UnlockIds.ForFold(fold, UnlockIds.HeavyPaper)) ? 1.25f : 1f) / 150f);

            // Fly button.
            y += 6 * scale;
            Rect fly = new Rect(area.x, y, area.width, 36 * scale);
            bool current = foldSelector != null && foldSelector.Current == fold;
            GUI.color = current ? new Color(0.3f, 1f, 0.45f, 0.2f) : (unlocked ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.06f));
            GUI.DrawTexture(fly, Texture2D.whiteTexture);
            GUI.color = Color.white;
            string flyText = current ? "<color=#80FF90><b>FLYING THIS FOLD</b></color>"
                           : unlocked ? "<b>F / (X): FLY THIS FOLD</b>" : "<color=#888888>Unlock it to fly it</color>";
            GUI.Label(fly, flyText, small);
            if (!current && unlocked && GUI.Button(fly, GUIContent.none, GUIStyle.none)) Equip(fold);
        }

        void StatLine(ref float y, Rect area, string label, float value01)
        {
            float labelW = 90 * scale;
            GUI.Label(new Rect(area.x, y - 4 * scale, labelW, 24 * scale), $"<color=#CCCCCC>{label}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperLeft });
            Rect bg = new Rect(area.x + labelW, y + 4 * scale, area.width - labelW, 10 * scale);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * Mathf.Clamp01(value01), bg.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 24 * scale;
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
                case Screen.Options: DrawOptions(); break;
            }
        }

        void DrawMenu()
        {
            Dim(0.65f);
            bool rooms = director.RoomCount > 1;
            const float Step = 90f;
            float w = 760 * scale, h = (230 + (rooms ? 56 : 0) + EntryCount * Step) * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.85f);

            float y = panel.y + 24 * scale;
            GUI.Label(new Rect(panel.x, y, w, 50 * scale), "PAPER WINGS", title);
            y += 52 * scale;
            GUI.Label(new Rect(panel.x, y, w, 30 * scale), "<color=#BBBBBB>The Night Shift War</color>", body);
            y += 50 * scale;

            // Room picker (free play): Left/Right, or click the arrows.
            if (rooms)
            {
                Rect bar = new Rect(panel.x + 40 * scale, y, w - 80 * scale, 46 * scale);
                GUI.color = new Color(0.4f, 0.7f, 1f, 0.16f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var room = director.RoomAt(roomSelected);
                string desc = room != null && room.description.Length > 0 ? $"   <color=#AAAAAA>{room.description}</color>" : "";
                GUI.Label(bar, $"<b>ROOM:  {director.RoomName(roomSelected).ToUpper()}</b>{desc}", new GUIStyle(small) { wordWrap = false, clipping = TextClipping.Clip });
                Rect leftArrow = new Rect(bar.x, bar.y, 46 * scale, bar.height), rightArrow = new Rect(bar.xMax - 46 * scale, bar.y, 46 * scale, bar.height);
                GUI.Label(leftArrow, "<b>◀</b>", body);
                GUI.Label(rightArrow, "<b>▶</b>", body);
                if (GUI.Button(leftArrow, GUIContent.none, GUIStyle.none)) ChangeRoom(-1);
                if (GUI.Button(rightArrow, GUIContent.none, GUIStyle.none)) ChangeRoom(+1);
                y += 56 * scale;
            }

            for (int i = 0; i < EntryCount; i++)
            {
                Rect card = new Rect(panel.x + 40 * scale, y, w - 80 * scale, (Step - 8f) * scale);
                bool sel = i == selected;
                bool isTraining = i == TrainingEntry;
                bool isCampaign = i == CampaignEntry;
                bool available = isCampaign || isTraining || director.RoomSupports(roomSelected, Modes[i - Offset]);
                Color tint = isTraining ? new Color(0.45f, 0.85f, 1f, 0.12f) : isCampaign ? new Color(1f, 0.85f, 0.3f, 0.12f) : new Color(1f, 1f, 1f, 0.06f);
                GUI.color = sel ? new Color(1f, 0.85f, 0.3f, available ? 0.35f : 0.15f) : tint;
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string heading, description, right;
                if (isTraining)
                {
                    bool done = ModeDirector.TrainingDone;
                    heading = "FLIGHT SCHOOL";
                    description = done ? "Fly the lessons again on a fully upgraded plane: every fold, every weapon."
                                       : "Start here! Learn to throw, steer, climb, boost and fight, on a fully upgraded plane.";
                    right = done ? "<color=#80FF90>passed</color>" : "<color=#80D8FF>new</color>";
                }
                else if (isCampaign)
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
                    if (!available)
                    {
                        heading = $"<color=#666666>{heading}</color>";
                        description = $"<color=#777777>Not in the {director.RoomName(roomSelected)}.</color>";
                        right = "";
                    }
                    else
                    {
                        description = Describe(m);
                        float best = director.BestTime(m, roomSelected);
                        right = best > 0f ? $"Best {best:0.0}s" : "";
                        var route = director.RouteOf(roomSelected);
                        if (m == GameMode.RingRun && route != null && route.BestTime > 0f) right = $"Best {route.BestTime:0.00}s";
                        int medal = Progression.GetMedal(ModeDirector.MedalKey(m, roomSelected));
                        if (medal > 0) right = $"<color={ProgressionSettings.MedalColor(medal)}>{ProgressionSettings.MedalName(medal)}</color>   " + right;
                    }
                }

                GUI.Label(new Rect(card.x + 18 * scale, card.y + 6 * scale, card.width, 36 * scale), $"<b>{i + 1}.  {heading}</b>", button);
                GUI.Label(new Rect(card.x + 18 * scale, card.y + 40 * scale, card.width - 200 * scale, 42 * scale),
                          $"<color=#CCCCCC>{description}</color>", small);
                GUI.Label(new Rect(card.xMax - 190 * scale, card.y + 12 * scale, 175 * scale, 30 * scale),
                          $"<color=#FFD040>{right}</color>", new GUIStyle(small) { alignment = TextAnchor.UpperRight });

                // Mouse support: click a card to start it.
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) Choose(i);
                y += Step * scale;
            }

            string resume = director.HasMode && !director.RunComplete ? "    Esc / M: resume" : "";
            string roomHint = rooms ? "  Left/Right: room." : "";
            GUI.Label(new Rect(panel.x, panel.yMax - 44 * scale, w, 30 * scale),
                      $"<color=#999999>Up/Down, Enter / Space to start, 1-{EntryCount}, or click.{roomHint}{resume}</color>", small);

            // Top-left: options. Top-right: paper clips + the way into the unlock shop.
            Rect opt = new Rect(panel.x + 20 * scale, panel.y + 18 * scale, 200 * scale, 30 * scale);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(opt, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(opt, "<b>O / (Select): OPTIONS</b>", small);
            if (GUI.Button(opt, GUIContent.none, GUIStyle.none)) OpenOptions();

            if (director.ProgressionConfig != null)
            {
                GUI.Label(new Rect(panel.xMax - 260 * scale, panel.y + 18 * scale, 240 * scale, 28 * scale),
                          $"<color=#FFD040><b>{Progression.PaperClips}</b> paper clips</color>", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
                Rect btn = new Rect(panel.xMax - 260 * scale, panel.y + 46 * scale, 240 * scale, 30 * scale);
                GUI.color = new Color(1f, 0.85f, 0.3f, 0.25f);
                GUI.DrawTexture(btn, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(btn, "<b>U / (Y): HANGAR</b>", small);
                if (GUI.Button(btn, GUIContent.none, GUIStyle.none)) OpenUnlocks();
            }
        }

        void ChangeRoom(int dir)
        {
            int n = director.RoomCount;
            if (n < 2) return;
            roomSelected = (roomSelected + dir + n) % n;
            Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
        }

        // ------------------------------------------------------------------ options

        enum Opt { Master, Effects, Ambience, Scheme, Invert, MouseSens, Brightness, Quality, Window, Resolution, Back, Quit }
        static readonly Opt[] OptRows = (Opt[])System.Enum.GetValues(typeof(Opt));

        void OpenOptions()
        {
            optSelected = 0;
            Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
            Show(Screen.Options);
        }

        void UpdateOptions(bool up, bool down, bool left, bool right, bool confirm, bool leave)
        {
            if (leave) { Show(Screen.Menu); return; }
            int n = OptRows.Length;
            if (up) optSelected = (optSelected + n - 1) % n;
            if (down) optSelected = (optSelected + 1) % n;
            if (up || down) Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
            if (left || right) { Adjust(OptRows[optSelected], right ? 1 : -1); Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
            else if (confirm) Activate(OptRows[optSelected]);
        }

        /// <summary>Enter on a row: toggles flip, Back / Quit act, sliders step up.</summary>
        void Activate(Opt o)
        {
            if (o == Opt.Back) { Show(Screen.Menu); return; }
            if (o == Opt.Quit) { GameOptions.Quit(); return; }
            Adjust(o, 1);
            Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);
        }

        void Adjust(Opt o, int dir)
        {
            var c = GameOptions.Controls;
            switch (o)
            {
                case Opt.Master: GameOptions.SetVolume("Master", Step(GameOptions.GetVolume("Master"), 0.1f * dir)); break;
                case Opt.Effects: GameOptions.SetVolume("Effects", Step(GameOptions.GetVolume("Effects"), 0.1f * dir)); break;
                case Opt.Ambience: GameOptions.SetVolume("Ambience", Step(GameOptions.GetVolume("Ambience"), 0.1f * dir)); break;
                case Opt.Scheme:
                    if (c != null) GameOptions.SetScheme(c.scheme == Flight.ControlScheme.Classic ? Flight.ControlScheme.MouseAim : Flight.ControlScheme.Classic);
                    break;
                case Opt.Invert: if (c != null) GameOptions.SetInvert(!c.invertPitch); break;
                case Opt.MouseSens: if (c != null) GameOptions.SetMouseSensitivity(Mathf.Round((c.mouseSensitivity + 0.02f * dir) * 100f) / 100f); break;
                case Opt.Brightness: GameOptions.Brightness = Mathf.Clamp(GameOptions.Brightness + 0.25f * dir, -1f, 2f); break;
                case Opt.Quality:
                    int q = QualitySettings.names.Length;
                    if (q > 0) GameOptions.SetQuality((QualitySettings.GetQualityLevel() + dir + q) % q);
                    break;
                case Opt.Window: GameOptions.SetFullscreen(UnityEngine.Screen.fullScreenMode == FullScreenMode.Windowed); break;
                case Opt.Resolution:
                    var res = GameOptions.Resolutions();
                    int cur = System.Array.IndexOf(res, new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height));
                    GameOptions.SetResolution(res[((cur < 0 ? res.Length - 1 : cur) + dir + res.Length) % res.Length]);
                    break;
            }
            PlayerPrefs.Save();
        }

        static float Step(float v, float d) => Mathf.Clamp01(Mathf.Round((v + d) * 10f) / 10f);

        string OptLabel(Opt o) => o switch
        {
            Opt.Master => "Master volume",
            Opt.Effects => "Effects volume",
            Opt.Ambience => "Wind & ambience volume",
            Opt.Scheme => "Controls",
            Opt.Invert => "Invert pitch",
            Opt.MouseSens => "Mouse sensitivity (mouse aim)",
            Opt.Brightness => "Brightness",
            Opt.Quality => "Graphics quality",
            Opt.Window => "Display",
            Opt.Resolution => "Resolution",
            Opt.Back => "Back",
            _ => "Quit to desktop"
        };

        string OptValue(Opt o)
        {
            var c = GameOptions.Controls;
            string Bar(float v) => $"{new string('|', Mathf.RoundToInt(v * 10f))}<color=#444444>{new string('|', 10 - Mathf.RoundToInt(v * 10f))}</color>  {Mathf.RoundToInt(v * 100f)}%";
            return o switch
            {
                Opt.Master => Bar(GameOptions.GetVolume("Master")),
                Opt.Effects => Bar(GameOptions.GetVolume("Effects")),
                Opt.Ambience => Bar(GameOptions.GetVolume("Ambience")),
                Opt.Scheme => c == null ? "-" : (c.scheme == Flight.ControlScheme.Classic ? "Classic (keys / stick)" : "Mouse aim"),
                Opt.Invert => c == null ? "-" : (c.invertPitch ? "On" : "Off"),
                Opt.MouseSens => c == null ? "-" : $"{c.mouseSensitivity:0.00}",
                Opt.Brightness => GameOptions.Brightness == 0f ? "Normal" : $"{GameOptions.Brightness:+0.00;-0.00}",
                Opt.Quality => QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "-",
                Opt.Window => UnityEngine.Screen.fullScreenMode == FullScreenMode.Windowed ? "Windowed" : "Fullscreen",
                Opt.Resolution => $"{UnityEngine.Screen.width} x {UnityEngine.Screen.height}",
                _ => ""
            };
        }

        void DrawOptions()
        {
            Dim(0.7f);
            float w = 760 * scale, rowStep = 46 * scale;
            float h = (170 + OptRows.Length * 46) * scale;
            Rect panel = Centered(w, h);
            Box(panel, 0.9f);

            float y = panel.y + 20 * scale;
            GUI.Label(new Rect(panel.x, y, w, 44 * scale), "OPTIONS", title);
            y += 60 * scale;

            for (int i = 0; i < OptRows.Length; i++)
            {
                var o = OptRows[i];
                Rect row = new Rect(panel.x + 30 * scale, y, w - 60 * scale, rowStep - 6 * scale);
                GUI.color = i == optSelected ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;
                string color = o == Opt.Quit ? "#FF9080" : "#FFFFFF";
                GUI.Label(new Rect(row.x + 14 * scale, row.y + 4 * scale, 330 * scale, 32 * scale), $"<color={color}>{OptLabel(o)}</color>", button);
                string value = OptValue(o);
                if (value.Length > 0)
                    GUI.Label(new Rect(row.x + 340 * scale, row.y + 6 * scale, row.width - 354 * scale, 30 * scale),
                              $"<color=#FFD040>◀</color>  {value}  <color=#FFD040>▶</color>", new GUIStyle(body) { alignment = TextAnchor.UpperRight });

                // Mouse: left half steps down, right half steps up (or activates buttons).
                Rect leftHalf = new Rect(row.x, row.y, row.width * 0.5f, row.height), rightHalf = new Rect(row.center.x, row.y, row.width * 0.5f, row.height);
                if (GUI.Button(leftHalf, GUIContent.none, GUIStyle.none)) { optSelected = i; if (value.Length > 0) Adjust(o, -1); else Activate(o); }
                if (GUI.Button(rightHalf, GUIContent.none, GUIStyle.none)) { optSelected = i; if (value.Length > 0) Adjust(o, +1); else Activate(o); }
                y += rowStep;
            }

            GUI.Label(new Rect(panel.x, panel.yMax - 40 * scale, w, 28 * scale),
                      "<color=#999999>Up/Down to pick, Left/Right to change (or click).    Esc / (B): back</color>", small);
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
            if (r.training) hint = "Enter / Space / (A): start the Night Shift      R / (X): fly it again      Esc / M / (B): menu";
            else if (!night) hint = "Enter / Space / (A): retry      Esc / M / (B): change mode";
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
                GameMode.FreeFlight => night != null && night.airTimeTarget > 0f
                    ? $"Stay up {night.airTimeTarget:0}s in one flight   Air time <b>{(director.PlayerFlying ? director.PlayerFlightTime : 0f):0.0}s</b>"
                    : "Free flight: no objective. Ride the air.",
                GameMode.RingRun => "Fly through every ring.",
                GameMode.Dogfight => $"Rivals shot down  <b>{director.KillsThisRun} / {director.DogfightTarget}</b>",
                GameMode.Squadron => $"Squadron kills  <b>{director.TeamKillsThisRun} / {director.SquadronTarget}</b>   <color=#AAAAAA>(yours {director.KillsThisRun})</color>",
                _ => director.Boss != null ? $"Defeat {director.Boss.DisplayName}." : "Defeat the boss."
            };
            string mission = director.ObjectiveText();
            if (mission != null) obj = mission;
            bool timed = m == GameMode.Dogfight || m == GameMode.Squadron || m == GameMode.BossFight;
            string timer = timed ? $"    Time <b>{director.RunTime:0.0}s</b>" : "";
            float best = director.BestTime(m);
            string bestText = timed && best > 0f && night == null ? $"    <color=#AAAAAA>Best {best:0.0}s</color>" : "";

            string label = ModeDirector.ModeName(m).ToUpper();
            if (director.RoomCount > 1 && director.RoomIndex > 0) label += $" · {director.RoomName(director.RoomIndex).ToUpper()}";
            if (director.InTraining) { label = "FLIGHT SCHOOL"; timer = ""; bestText = ""; }
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
            DrawEventBanner();
            DrawLesson();
        }

        /// <summary>Flight School: the current lesson as a card at the top of the screen.</summary>
        void DrawLesson()
        {
            var t = director.InTraining ? director.Training : null;
            if (t == null || director.RunComplete || t.Current == Tutorial.Step.Done) return;
            float fade = Mathf.Clamp01((Time.time - t.StepStarted) * 3f);
            float w = 900 * scale, h = 132 * scale;
            Rect r = new Rect((UnityEngine.Screen.width - w) * 0.5f, 24 * scale, w, h);
            GUI.color = new Color(0.05f, 0.12f, 0.2f, 0.78f * fade);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.85f, 1f, 0.9f * fade);
            GUI.DrawTexture(new Rect(r.x, r.y, 6 * scale, r.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.Label(new Rect(r.x + 24 * scale, r.y + 8 * scale, w - 48 * scale, 34 * scale),
                      $"<color=#80D8FF>LESSON {t.StepNumber} / {Tutorial.StepCount}</color>   <b>{t.Title}</b>" +
                      (t.Progress.Length > 0 ? $"   <color=#FFD040>{t.Progress}</color>" : ""), new GUIStyle(body) { alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(r.x + 24 * scale, r.y + 46 * scale, w - 48 * scale, h - 52 * scale), t.Body,
                      new GUIStyle(small) { alignment = TextAnchor.UpperLeft });
            GUI.color = Color.white;
        }

        /// <summary>Room events (Stage 29): a warning with a countdown, then what to do while it lasts.</summary>
        void DrawEventBanner()
        {
            var ev = RoomEvent.Announcing;
            if (ev == null || director.RunComplete) return;
            bool warn = ev.Current == RoomEvent.Stage.Warning;
            string hex = ColorUtility.ToHtmlStringRGB(ev.color);
            float pulse = warn ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            string text = warn ? $"<b><color=#{hex}>{ev.title}</color></b>   <color=#DDDDDD>in {ev.SecondsToGo:0.0}s</color>"
                               : $"<b><color=#{hex}>{ev.title}</color></b>   <color=#FFFFFF>{ev.hint}</color>";
            float w = 900 * scale;
            Rect r = new Rect((UnityEngine.Screen.width - w) * 0.5f, 128 * scale, w, 38 * scale);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * pulse);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, pulse);
            GUI.Label(r, text, body);
            GUI.color = Color.white;
        }

        static string Describe(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Just you, the desk and the air. No enemies, no clock.",
            GameMode.RingRun => "Thread all five sticky-note rings as fast as you can. The last ring stops the clock.",
            GameMode.Dogfight => "Accounting's rival planes are up. Shoot down five of them as fast as you can.",
            GameMode.Squadron => "Fly with two Marketing wingmen against four rivals. Your squadron needs eight kills.",
            _ => "Each room's boss: THE PRINTER (office), THE AUDITOR (break room), THE MAINFRAME (server room), THE CONDENSER (rooftop).",
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
