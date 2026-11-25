using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// The other menu screens in the Desk Memo style (the main menu is in ModeMenu.Desk.cs):
    ///   Campaign  the night roster on a clipboard, the chosen night's memo beside it
    ///   Hangar    a manila folder with a tab per fold: the plane pinned like a photo, a typed parts list
    ///   Options   a settings form on a clipboard, filled in with blue pen
    ///   Results   a shift report on top of the (dimmed) game
    /// Input is unchanged (ModeMenu.Update and the Update* methods).
    /// </summary>
    public partial class ModeMenu
    {
        static readonly Color Manila = new Color32(0xe8, 0xd3, 0xa0, 0xff);
        static readonly Color ManilaDark = new Color32(0xc9, 0xb2, 0x7c, 0xff);
        static readonly Color MemoYellow = new Color32(0xfb, 0xf3, 0xd2, 0xff);
        static readonly Color BluePen = new Color32(0x1d, 0x3f, 0x8a, 0xff);
        static readonly Color GreenInk = new Color32(0x1f, 0x7a, 0x3a, 0xff);
        static readonly Color Pencil = new Color32(0x8b, 0x81, 0x75, 0xff);

        /// <summary>
        /// Messages and labels written for the old dark panels use light colours; on paper they become inks.
        /// </summary>
        static string Inked(string rich)
        {
            if (string.IsNullOrEmpty(rich)) return "";
            return rich.Replace("#60FF80", "#1f7a3a").Replace("#80FF90", "#1f7a3a")
                       .Replace("#FF9050", "#a3321f").Replace("#FF6050", "#a3321f").Replace("#FF9080", "#a3321f")
                       .Replace("#AAAAAA", "#6d6356").Replace("#BBBBBB", "#6d6356").Replace("#999999", "#6d6356").Replace("#888888", "#8b8175")
                       .Replace("#FFD040", "#8a5a10").Replace("#FFE080", "#8a5a10").Replace("#80C8FF", "#1d3f8a").Replace("#80D8FF", "#1d3f8a");
        }

        /// <summary>Stars in ink: filled gold, empty pencil.</summary>
        static string InkStars(int stars)
        {
            int s = Mathf.Clamp(stars, 0, 3);
            return $"<color=#b07d00>{new string('★', s)}</color><color=#b8ad9a>{new string('☆', 3 - s)}</color>";
        }

        void HintStrip(float x, float y, float w, string hint)
        {
            Fill(R(x, y, w, 34f), new Color(Paper.r, Paper.g, Paper.b, 0.9f));
            Text(x, y, w, 34f, hint, typeStyle, 13f, new Color32(0x3b, 0x34, 0x2b, 0xff), TextAnchor.MiddleCenter);
        }

        /// <summary>A clipboard: board, metal clip, the paper on it. Returns nothing; the paper is at (x+20, y+30).</summary>
        void ClipboardAt(float x, float y, float w, float h, Color paper)
        {
            Fill(R(x + 8f, y + 12f, w, h), new Color(0f, 0f, 0f, 0.4f));
            Fill(R(x, y, w, h), Clipboard);
            Fill(R(x + w * 0.5f - 70f, y - 16f, 140f, 36f), Clip);
            Fill(R(x + w * 0.5f - 70f, y + 12f, 140f, 8f), new Color32(0x8e, 0x91, 0x97, 0xff));
            Fill(R(x + 20f, y + 30f, w - 40f, h - 50f), paper);
        }

        // ------------------------------------------------------------------ campaign

        void DrawCampaign()
        {
            var c = Campaign;
            if (c == null) { screen = Screen.Menu; return; }
            BeginDesk();
            DrawDeskSurface(coffeeRing: false);

            // ---- the roster ----
            Tilted(-0.8f, 360f, 380f, () =>
            {
                ClipboardAt(50f, 50f, 620f, 640f, MemoPaper);
                Text(92f, 96f, 360f, 30f, string.IsNullOrEmpty(c.rosterTitle) ? "ROSTER" : c.rosterTitle, typeStyle, 20f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(400f, 98f, 230f, 26f, $"{InkStars(Progression.TotalStars())} {Progression.TotalStars()}/{c.Count * 3}   {Progression.PaperClips} clips",
                     typeStyle, 14f, InkSoft, TextAnchor.UpperRight);
                Fill(R(92f, 128f, 538f, 2f), Ink);

                const float top = 140f, bottom = 668f;
                int soon = c.comingSoon != null ? c.comingSoon.Length : 0;
                float rowH = Mathf.Min(44f, (bottom - top) / Mathf.Max(1, c.Count + soon));

                for (int i = 0; i < c.Count; i++)
                {
                    var night = c.Get(i);
                    bool open = Progression.NightOpen(i);
                    int stars = Progression.GetNightStars(i);
                    float y = top + i * rowH;
                    bool sel = i == nightSelected;
                    if (sel) Fill(R(86f, y + 3f, 550f, rowH - 6f), new Color(Highlighter.r, Highlighter.g, Highlighter.b, open ? 0.95f : 0.45f));

                    Color ink = open ? Ink : Pencil;
                    Text(96f, y + 4f, 40f, rowH - 6f, $"{i + 1}.", typeStyle, 16f, ink, TextAnchor.MiddleLeft);
                    Text(136f, y + 4f, 300f, rowH - 6f, night.title, typeStyle, 18f, ink, TextAnchor.MiddleLeft, sel ? FontStyle.Bold : FontStyle.Normal);
                    if (night.title.Length <= 16)   // long titles take the room column
                        Text(380f, y + 4f, 150f, rowH - 6f, director.RoomName(night.room).ToLower(), handStyle, 16f, open ? BluePen : Pencil, TextAnchor.MiddleLeft);
                    string right = !open ? "<color=#8b8175>locked</color>" : stars > 0 ? InkStars(stars) : "<color=#a3321f><b>NEW</b></color>";
                    Text(520f, y + 4f, 110f, rowH - 6f, right, typeStyle, 17f, Ink, TextAnchor.MiddleRight);
                    if (i < c.Count - 1) Fill(R(92f, y + rowH - 1f, 538f, 1f), new Color(0f, 0f, 0f, 0.08f));

                    if (Hit(86f, y, 550f, rowH))
                    {
                        if (sel) PlayNight(i);   // click to pick, click again to fly
                        else { nightSelected = i; Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
                    }
                }
                // The story's levels that aren't built yet, pencilled in.
                for (int j = 0; j < soon; j++)
                {
                    float y = top + (c.Count + j) * rowH;
                    Text(96f, y + 4f, 40f, rowH - 6f, $"{c.Count + j + 1}.", typeStyle, 16f, Pencil, TextAnchor.MiddleLeft);
                    Text(136f, y + 4f, 300f, rowH - 6f, c.comingSoon[j], typeStyle, 18f, Pencil, TextAnchor.MiddleLeft);
                    Text(520f, y + 4f, 110f, rowH - 6f, "coming soon", handStyle, 15f, Pencil, TextAnchor.MiddleRight);
                }
            });

            // ---- the chosen night's memo ----
            var selNight = c.Get(nightSelected);
            if (selNight != null)
            {
                bool open = Progression.NightOpen(nightSelected);
                Tilted(1.5f, 970f, 340f, () =>
                {
                    Sheet(730f, 60f, 480f, 560f, MemoYellow, 14f);
                    string brief = string.IsNullOrEmpty(c.briefingWord) ? "MEMO" : c.briefingWord;
                    string stamp = string.IsNullOrEmpty(selNight.when) ? "" : $" · {selNight.when}";
                    Text(760f, 82f, 420f, 22f, $"{brief} · {LevelWord.ToUpper()} {nightSelected + 1}{stamp}", typeStyle, 13f, new Color32(0x6d, 0x60, 0x4b, 0xff));
                    string where = !string.IsNullOrEmpty(selNight.place) ? selNight.place : director.RoomName(selNight.room);
                    if (stamp.Length > 0) where = director.RoomName(selNight.room);   // the long place name won't fit beside the time
                    Text(760f, 82f, 420f, 22f, where.ToUpper(), typeStyle, 13f, new Color32(0x6d, 0x60, 0x4b, 0xff), TextAnchor.UpperRight);
                    heavyStyle.wordWrap = true;
                    Text(760f, 110f, 420f, 56f, selNight.title.ToUpper(), heavyStyle, 34f, Ink);
                    heavyStyle.wordWrap = false;
                    Text(760f, 170f, 420f, 220f, open ? selNight.memo : $"Clear the previous {LevelWord.ToLower()} to read this {brief.ToLower()}.",
                         typeStyle, selNight.memo.Length > 420 ? 12.5f : 14f, Ink);

                    // dotted rule, then the facts
                    for (float dx = 0f; dx < 420f; dx += 8f) Fill(R(760f + dx, 400f, 4f, 1.5f), new Color32(0x8a, 0x7a, 0x5e, 0xff));
                    float fy = 412f;
                    if (open)
                    {
                        string goal = selNight.Goal(director.DefaultDogfightTarget, director.DefaultSquadronTarget);
                        bool longGoal = goal.Length > 44;
                        Text(760f, fy, 420f, longGoal ? 40f : 22f, $"GOAL ....... {goal}", typeStyle, 13f, Ink); fy += longGoal ? 42f : 26f;
                        if (selNight.threeStarTime > 0f)
                        {
                            Text(760f, fy, 420f, 22f, $"★★ ......... under {selNight.twoStarTime:0} s", typeStyle, 13f, Ink); fy += 22f;
                            Text(760f, fy, 420f, 22f, $"★★★ ........ under {selNight.threeStarTime:0} s", typeStyle, 13f, Ink); fy += 26f;
                        }
                        string reward = Progression.NightCleared(nightSelected)
                            ? $"BEST ....... {InkStars(Progression.GetNightStars(nightSelected))}"
                            : $"REWARD ..... {RewardText(selNight)}";
                        Text(760f, fy, 420f, 22f, reward, typeStyle, 13f, Ink);
                    }

                    // FLY button: a dark ink stamp.
                    Fill(R(760f, 548f, 420f, 50f), open ? Ink : Pencil);
                    Text(760f, 548f, 420f, 50f, open ? "FLY  ·  ENTER" : "LOCKED", heavyStyle, 22f, MemoYellow, TextAnchor.MiddleCenter);
                    if (open && Hit(760f, 548f, 420f, 50f)) PlayNight(nightSelected);
                });
            }

            HintStrip(730f, 656f, 480f, $"↑↓ {LevelWord.ToLower()} · Enter fly (or click twice) · U hangar · Esc back");
        }

        // ------------------------------------------------------------------ newsreel (Tiny War)

        /// <summary>
        /// Grandad's newsreel title card before a Tiny War level: black and white film with a flickering
        /// frame, grain and scratches, the time stamp, the level's title and his narration typed out.
        /// The game waits behind it (paused) until you carry on.
        /// </summary>
        void DrawNewsreel()
        {
            var night = director.Night;
            if (night == null) { Show(Screen.None); return; }
            BeginDesk();
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            float t = Time.unscaledTime - newsreelStart;
            float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(Time.unscaledTime * 9f, 0.3f);

            Fill(new Rect(0, 0, sw, sh), new Color(0.05f, 0.05f, 0.05f, 1f));
            // The projected frame: a soft grey screen, brighter in the middle, a dark rounded vignette.
            Tex(R(-120f, -260f, 1520f, 1240f), softDot, new Color(0.62f, 0.6f, 0.56f, 0.5f * flicker));
            Fill(R(140f, 70f, 1000f, 580f), new Color(0.14f, 0.14f, 0.13f, 0.9f));
            Tex(R(180f, -40f, 920f, 800f), softDot, new Color(0.9f, 0.88f, 0.84f, 0.16f * flicker));
            // Grain and scratches (redrawn at the film's 18 frames a second).
            var rnd = new System.Random(Mathf.FloorToInt(Time.unscaledTime * 18f));
            for (int i = 0; i < 70; i++)
            {
                float gx = 140f + (float)rnd.NextDouble() * 1000f, gy = 70f + (float)rnd.NextDouble() * 580f;
                Fill(R(gx, gy, 2.5f, 2.5f), new Color(1f, 1f, 1f, 0.08f + 0.1f * (float)rnd.NextDouble()));
            }
            for (int i = 0; i < 2; i++)
            {
                float sx = 160f + (float)rnd.NextDouble() * 960f;
                Fill(R(sx, 70f, 1.4f, 580f), new Color(1f, 1f, 1f, 0.12f * (float)rnd.NextDouble()));
            }
            // Frame edge and a sprocket strip down each side.
            Fill(R(140f, 70f, 1000f, 3f), new Color(0.85f, 0.83f, 0.78f, 0.6f)); Fill(R(140f, 647f, 1000f, 3f), new Color(0.85f, 0.83f, 0.78f, 0.6f));
            for (float y = 80f; y < 640f; y += 38f) { Fill(R(108f, y, 18f, 22f), new Color(0.75f, 0.73f, 0.68f, 0.35f)); Fill(R(1154f, y, 18f, 22f), new Color(0.75f, 0.73f, 0.68f, 0.35f)); }

            Color film = new Color(0.94f, 0.92f, 0.86f, flicker);
            Color dim = new Color(0.8f, 0.78f, 0.72f, 0.85f * flicker);
            string reel = $"TINY WAR NEWSREEL  ·  REEL {director.NightIndex + 1}";
            Text(180f, 96f, 920f, 26f, reel, typeStyle, 16f, dim, TextAnchor.UpperCenter);
            Fill(R(440f, 128f, 400f, 2f), dim);
            Text(180f, 140f, 920f, 26f, string.IsNullOrEmpty(night.when) ? "" : night.when, typeStyle, 18f, film, TextAnchor.UpperCenter);
            heavyStyle.wordWrap = true;
            Text(180f, 176f, 920f, 80f, night.title.ToUpper(), heavyStyle, 52f, film, TextAnchor.UpperCenter);
            heavyStyle.wordWrap = false;

            // The narration types itself out (about 38 characters a second).
            string text = night.newsreel;
            int shown = Mathf.Clamp(Mathf.FloorToInt((t - 0.5f) * 38f), 0, text.Length);
            Text(260f, 286f, 760f, 260f, text.Substring(0, shown), typeStyle, 21f, film, TextAnchor.UpperCenter);
            if (shown >= text.Length)
            {
                Text(260f, 552f, 760f, 30f, "(Grandad, narrating)", handStyle, 22f, dim, TextAnchor.UpperCenter);
                float blink = Mathf.PingPong(Time.unscaledTime * 1.5f, 1f);
                Text(180f, 600f, 920f, 26f, "ENTER · FLY", typeStyle, 15f, new Color(film.r, film.g, film.b, 0.35f + 0.65f * blink), TextAnchor.UpperCenter);
            }
            if (Hit(140f, 70f, 1000f, 580f) && t > 0.6f) Show(Screen.None);
        }

        // ------------------------------------------------------------------ hangar

        void DrawUnlocks()
        {
            var cfg = director.ProgressionConfig;
            if (cfg == null) { screen = Screen.Menu; return; }
            var folds = ShopFolds();
            var fold = TabFold();
            var shop = cfg.ShopFor(fold);
            if (foldSelector == null) foldSelector = FindAnyObjectByType<FoldSelector>();
            if (preview == null) preview = FindAnyObjectByType<PlanePreview>();
            BeginDesk();
            DrawDeskSurface(coffeeRing: false);

            // ---- the folder and its tabs (one per fold) ----
            Fill(R(48f, 92f, 1196f, 604f), new Color(0f, 0f, 0f, 0.4f));
            if (folds.Length > 0)
            {
                float tabW = Mathf.Min(220f, 1100f / folds.Length);
                for (int t = 0; t < folds.Length; t++)
                {
                    float tx = 70f + t * (tabW + 8f);
                    bool sel = t == foldTab;
                    bool open = Progression.IsUnlocked(folds[t].unlockId);
                    bool flying = foldSelector != null && foldSelector.Current == folds[t];
                    Fill(R(tx, sel ? 40f : 48f, tabW, sel ? 46f : 38f), sel ? Manila : ManilaDark);
                    string label = folds[t].displayName.ToUpper() + (flying ? "  ✈" : "") + (open ? "" : "  (locked)");
                    Text(tx, sel ? 40f : 48f, tabW, sel ? 46f : 38f, label, typeStyle, sel ? 17f : 15f, open ? Ink : Pencil, TextAnchor.MiddleCenter, sel ? FontStyle.Bold : FontStyle.Normal);
                    if (Hit(tx, 40f, tabW, 46f)) SetTab(t);
                }
            }
            Fill(R(40f, 84f, 1200f, 604f), Manila);
            Text(70f, 96f, 400f, 40f, "HANGAR", heavyStyle, 30f, Ink);
            Text(860f, 96f, 350f, 40f, $"{Progression.PaperClips} paper clips", handStyle, 28f, Ink, TextAnchor.UpperRight, FontStyle.Bold);

            if (fold != null) DrawPinnedPlane(fold, shop);
            DrawPartsList(shop);

            if (!string.IsNullOrEmpty(shopMessage))
                Text(520f, 608f, 690f, 34f, Inked(shopMessage), handStyle, 22f, Ink, TextAnchor.MiddleLeft);
            HintStrip(90f, 650f, 1100f, "←→ fold · ↑↓ look at an upgrade · Enter buy (or click twice) · F fly this fold · drag the plane to spin · U / Esc back");
        }

        /// <summary>The fold on its turntable, pinned to the folder like a photo, with its stats and the fly stamp.</summary>
        void DrawPinnedPlane(PlaneFold fold, System.Collections.Generic.List<UnlockItem> shop)
        {
            bool unlocked = Progression.IsUnlocked(fold.unlockId);
            Tilted(-1.5f, 270f, 330f, () =>
            {
                Sheet(70f, 140f, 400f, 380f, Color.white, 10f);
                Rect pic = R(84f, 154f, 372f, 300f);
                if (preview != null)
                {
                    var ghost = default(PreviewLoadout);
                    if (shopSelected >= 0 && shopSelected < shop.Count && Progression.CanBuy(shop[shopSelected]) != Progression.BuyResult.AlreadyOwned)
                        ghost = PlanePreview.LoadoutFor(shop[shopSelected].id);
                    preview.Request(fold, PlanePreview.OwnedLoadout(fold), ghost);
                    Fill(pic, new Color32(0x1c, 0x22, 0x33, 0xff));
                    GUI.DrawTexture(pic, preview.Texture, ScaleMode.ScaleAndCrop, false);
                    var e = Event.current;
                    if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition)) { preview.Drag(-e.delta.x * 0.6f); e.Use(); }
                }
                else Fill(pic, new Color32(0xdd, 0xd6, 0xc8, 0xff));
                Text(84f, 462f, 372f, 46f, fold.displayName.ToUpper(), handStyle, 30f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                Fill(R(262f, 132f, 16f, 16f), new Color32(0xc0, 0x30, 0x30, 0xff));   // the pin
                if (!unlocked)
                    Tilted(-12f, 270f, 300f, () =>
                    {
                        Fill(R(170f, 276f, 200f, 4f), RedPen); Fill(R(170f, 320f, 200f, 4f), RedPen);
                        Text(170f, 280f, 200f, 40f, "LOCKED", heavyStyle, 28f, RedPen, TextAnchor.MiddleCenter);
                    });
            });

            // stats, typed, with ink bars
            float y = 538f;
            Text(70f, y, 400f, 22f, fold.description, typeStyle, 12f, InkSoft); y += 24f;
            float heavyPaper = Progression.IsUnlocked(UnlockIds.ForFold(fold, UnlockIds.HeavyPaper)) ? 1.25f : 1f;
            if (fold.flight != null)
            {
                InkBar(ref y, "SPEED", fold.flight.maxSpeed / 50f);
                InkBar(ref y, "GLIDE", fold.flight.glideRatio / 15f);
            }
            if (fold.paper != null) InkBar(ref y, "PAPER", fold.paper.maxIntegrity * heavyPaper / 150f);

            // fly stamp
            bool current = foldSelector != null && foldSelector.Current == fold;
            Color stamp = current ? GreenInk : unlocked ? Ink : Pencil;
            Fill(R(330f, 560f, 140f, 2f), stamp); Fill(R(330f, 600f, 140f, 2f), stamp);
            Fill(R(330f, 560f, 2f, 42f), stamp); Fill(R(468f, 560f, 2f, 42f), stamp);
            Text(330f, 560f, 140f, 42f, current ? "FLYING ✈" : unlocked ? "F · FLY IT" : "LOCKED", typeStyle, 15f, stamp, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (!current && unlocked && Hit(330f, 560f, 140f, 42f)) Equip(fold);
        }

        void InkBar(ref float y, string label, float value01)
        {
            Text(70f, y, 70f, 18f, label, typeStyle, 12f, Ink);
            Fill(R(140f, y + 4f, 170f, 10f), new Color(0f, 0f, 0f, 0.1f));
            Fill(R(140f, y + 4f, 170f * Mathf.Clamp01(value01), 10f), Ink);
            y += 20f;
        }

        /// <summary>The fold's shop as a typed parts list.</summary>
        void DrawPartsList(System.Collections.Generic.List<UnlockItem> shop)
        {
            Sheet(510f, 140f, 700f, 462f, MemoPaper, 8f);
            Text(536f, 152f, 400f, 24f, "PARTS LIST", typeStyle, 15f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            Text(800f, 154f, 384f, 22f, "COST", typeStyle, 13f, InkSoft, TextAnchor.UpperRight);
            Fill(R(536f, 178f, 648f, 2f), Ink);

            const float top = 184f, bottom = 596f;
            float rowH = Mathf.Min(66f, (bottom - top) / Mathf.Max(1, shop.Count));
            for (int i = 0; i < shop.Count; i++)
            {
                var item = shop[i];
                float y = top + i * rowH;
                bool sel = i == shopSelected;
                var state = Progression.CanBuy(item);
                bool greyed = state == Progression.BuyResult.NeedsRequirement || state == Progression.BuyResult.NeedsBlueprint;
                if (sel) Fill(R(530f, y + 3f, 660f, rowH - 6f), new Color(Highlighter.r, Highlighter.g, Highlighter.b, 0.95f));

                Color ink = state == Progression.BuyResult.AlreadyOwned ? GreenInk : greyed ? Pencil : Ink;
                Text(540f, y + 6f, 440f, 24f, item.displayName, typeStyle, 17f, ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(540f, y + 30f, 440f, rowH - 32f, item.description, typeStyle, 12f, greyed ? Pencil : InkSoft);

                string status = state switch
                {
                    Progression.BuyResult.AlreadyOwned => "<color=#1f7a3a><b>OWNED</b></color>",
                    Progression.BuyResult.NeedsRequirement => $"<color=#8b8175>needs {NameOf(item.requires)}</color>",
                    Progression.BuyResult.NeedsBlueprint => $"<color=#8b8175>blueprint: {NightName(item.requiresNight)}</color>\n<color=#8b8175>{item.cost} clips</color>",
                    Progression.BuyResult.TooExpensive => $"<color=#a3321f>{item.cost} clips</color>",
                    _ => $"<b>{item.cost} clips</b>"
                };
                Text(960f, y + 8f, 224f, rowH - 10f, status, typeStyle, 13f, Ink, TextAnchor.UpperRight);

                if (Hit(530f, y, 660f, rowH))
                {
                    if (sel) Buy(item);   // click to look at it on the plane, click again to buy
                    else { shopSelected = i; shopMessage = ""; Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
                }
            }
        }

        // ------------------------------------------------------------------ options

        void DrawOptions()
        {
            BeginDesk();
            DrawDeskSurface();
            Tilted(0.6f, 640f, 370f, () =>
            {
                ClipboardAt(330f, 34f, 620f, 676f, MemoPaper);
                Text(372f, 78f, 360f, 30f, "FORM 7B · SETTINGS", typeStyle, 20f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(700f, 82f, 210f, 24f, "fill in, sign, return", handStyle, 17f, InkSoft, TextAnchor.UpperRight);
                Fill(R(372f, 112f, 538f, 2f), Ink);

                const float top = 120f;
                float rowH = Mathf.Min(44f, (680f - top) / OptRows.Length);
                for (int i = 0; i < OptRows.Length; i++)
                {
                    var o = OptRows[i];
                    float y = top + i * rowH;
                    bool sel = i == optSelected;
                    if (sel) Fill(R(366f, y + 3f, 550f, rowH - 6f), new Color(Highlighter.r, Highlighter.g, Highlighter.b, 0.95f));

                    Color label = o == Opt.Quit ? RedPen : Ink;
                    Text(378f, y, 300f, rowH, OptLabel(o), typeStyle, 16f, label, TextAnchor.MiddleLeft, sel ? FontStyle.Bold : FontStyle.Normal);
                    if (o != Opt.Back && o != Opt.Quit) Fill(R(640f, y + rowH - 9f, 268f, 1f), new Color(0f, 0f, 0f, 0.25f));   // the form's blank line

                    if (IsSlider(o)) InkSlider(640f, y, rowH, SliderValue(o));
                    else
                    {
                        string value = Inked(OptValue(o));
                        if (value.Length > 0) Text(640f, y, 268f, rowH, value, handStyle, 20f, BluePen, TextAnchor.MiddleCenter, FontStyle.Bold);
                    }
                    if (OptValue(o).Length > 0)
                    {
                        Text(616f, y, 24f, rowH, "◀", typeStyle, 14f, InkSoft, TextAnchor.MiddleCenter);
                        Text(908f, y, 24f, rowH, "▶", typeStyle, 14f, InkSoft, TextAnchor.MiddleCenter);
                    }

                    // Mouse: left half steps down, right half steps up (or activates Back / Quit).
                    bool hasValue = OptValue(o).Length > 0;
                    if (Hit(366f, y, 275f, rowH)) { optSelected = i; if (hasValue) Adjust(o, -1); else Activate(o); }
                    if (Hit(641f, y, 275f, rowH)) { optSelected = i; if (hasValue) Adjust(o, +1); else Activate(o); }
                }
            });
            HintStrip(980f, 650f, 270f, "↑↓ pick · ←→ change · Esc back");
        }

        static bool IsSlider(Opt o) => o == Opt.Master || o == Opt.Effects || o == Opt.Ambience;
        static float SliderValue(Opt o) => GameOptions.GetVolume(o == Opt.Master ? "Master" : o == Opt.Effects ? "Effects" : "Ambience");

        /// <summary>A volume written as ten boxes, ticked off in blue pen.</summary>
        void InkSlider(float x, float y, float rowH, float value01)
        {
            int filled = Mathf.RoundToInt(value01 * 10f);
            for (int b = 0; b < 10; b++)
            {
                float bx = x + 18f + b * 20f, by = y + rowH * 0.5f - 7f;
                Fill(R(bx, by, 14f, 14f), new Color(0f, 0f, 0f, 0.3f));
                Fill(R(bx + 1.5f, by + 1.5f, 11f, 11f), MemoPaper);
                if (b < filled) Fill(R(bx + 3f, by + 3f, 8f, 8f), BluePen);
            }
            Text(x + 220f, y, 50f, rowH, $"{Mathf.RoundToInt(value01 * 100f)}%", handStyle, 17f, BluePen, TextAnchor.MiddleLeft);
        }

        // ------------------------------------------------------------------ results

        void DrawResults()
        {
            var r = director.LastResult;
            if (r == null) { screen = Screen.Menu; return; }
            bool night = r.night >= 0;
            BeginDesk();
            Dim(0.55f);   // the game stays visible behind the report

            Tilted(-1f, 640f, 360f, () =>
            {
                Sheet(380f, 70f, 520f, 580f, MemoPaper, 16f);
                bool story = Campaign != null && !string.IsNullOrEmpty(Campaign.id);
                Text(410f, 92f, 460f, 22f, story && night ? "AFTER-ACTION REPORT" : "SHIFT REPORT", typeStyle, 14f, InkSoft);
                if (night && Campaign != null && Campaign.Get(r.night) != null)
                    Text(410f, 92f, 460f, 22f, $"{LevelWord.ToUpper()} {r.night + 1} · {Campaign.Get(r.night).title.ToUpper()}", typeStyle, 14f, InkSoft, TextAnchor.UpperRight);
                Fill(R(410f, 118f, 460f, 2f), Ink);

                heavyStyle.wordWrap = true;   // long titles ("THE AUDITOR IS DEFEATED!") wrap to two lines
                Text(410f, 130f, 460f, 96f, r.title, heavyStyle, 34f, r.failed ? RedPen : GreenInk, TextAnchor.MiddleCenter);
                heavyStyle.wordWrap = false;
                Text(410f, 230f, 460f, 60f, Inked(r.detail), typeStyle, 15f, Ink, TextAnchor.UpperCenter);

                float y = 300f;
                if (night && !r.failed)
                {
                    Text(410f, y, 460f, 54f, InkStars(r.stars), typeStyle, 44f, Ink, TextAnchor.MiddleCenter); y += 56f;
                    if (r.newStars && r.stars > 1) { Text(410f, y, 460f, 28f, "new best!", handStyle, 22f, RedPen, TextAnchor.MiddleCenter); y += 30f; }
                    if (!string.IsNullOrEmpty(r.reward)) { Text(410f, y, 460f, 28f, r.reward, typeStyle, 15f, GreenInk, TextAnchor.MiddleCenter, FontStyle.Bold); y += 30f; }
                    if (r.campaignComplete) { Text(410f, y, 460f, 30f, string.IsNullOrEmpty(Campaign.completeLine) ? "CAMPAIGN COMPLETE!" : Campaign.completeLine, typeStyle, 17f, BrownNote, TextAnchor.MiddleCenter, FontStyle.Bold); y += 32f; }
                }
                if (r.medal > 0)
                {
                    Text(410f, y, 460f, 30f, $"{ProgressionSettings.MedalName(r.medal).ToUpper()} MEDAL" + (r.newMedal ? "  (new!)" : ""),
                         typeStyle, 18f, BrownNote, TextAnchor.MiddleCenter, FontStyle.Bold);
                    y += 34f;
                }
                if (r.clipsEarned > 0)
                    Text(410f, y, 460f, 30f, $"+{r.clipsEarned} paper clips   ({Progression.PaperClips} total)", handStyle, 22f, Ink, TextAnchor.MiddleCenter);

                // NEW BEST: a red rubber stamp across the corner.
                if (r.newBest)
                    Tilted(-14f, 790f, 200f, () =>
                    {
                        Fill(R(712f, 180f, 160f, 3f), RedPen); Fill(R(712f, 218f, 160f, 3f), RedPen);
                        Text(712f, 182f, 160f, 36f, "NEW BEST", heavyStyle, 22f, RedPen, TextAnchor.MiddleCenter);
                    });

                string hint;
                if (r.training) hint = "Enter: start the Night Shift · R: fly it again · Esc: menu";
                else if (!night) hint = "Enter: retry · Esc: change mode";
                else if (r.failed) hint = "Enter: try again · Esc: campaign";
                else if (NextNightAfter(r) != r.night) hint = $"Enter: {LevelWord} {r.night + 2} · R: replay · Esc: campaign";
                else hint = "Enter: replay · Esc: campaign";
                Fill(R(410f, 592f, 460f, 1f), new Color(0f, 0f, 0f, 0.25f));
                Text(410f, 600f, 460f, 32f, hint, typeStyle, 13f, InkSoft, TextAnchor.MiddleCenter);
            });
        }
    }
}
