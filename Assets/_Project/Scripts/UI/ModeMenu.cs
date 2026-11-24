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
    public partial class ModeMenu : MonoBehaviour
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
                roomSelected = director.HasRoom(director.RoomIndex) ? director.RoomIndex : 0;
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

        // The main menu itself (the desk) is drawn in ModeMenu.Desk.cs.

        void ChangeRoom(int dir)
        {
            int n = director.RoomCount;
            if (n < 2) return;
            // Step past empty slots (a removed room keeps its number, Stage 42).
            for (int i = 0; i < n; i++)
            {
                roomSelected = (roomSelected + dir + n) % n;
                if (director.HasRoom(roomSelected)) break;
            }
            Audio.AudioFx.PlayUi(Audio.Sfx.UiMove);
        }

        // ------------------------------------------------------------------ options

        enum Opt { Difficulty, Master, Effects, Ambience, Scheme, Invert, MouseSens, Brightness, Quality, Window, Resolution, Back, Quit }
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
                case Opt.Difficulty: Difficulty.Current = (Difficulty.Level)(((int)Difficulty.Current + dir + 3) % 3); break;
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
            Opt.Difficulty => "Difficulty",
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
                Opt.Difficulty => Difficulty.Name(Difficulty.Current),
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
            float w = 900 * scale, h = 150 * scale;   // room for the air lessons' longer text
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
