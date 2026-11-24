using UnityEngine;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// The main menu as things on your desk at 2 AM ("Desk Memo" design):
    ///   - a typed memo on a clipboard lists what you can play, the chosen line marked with highlighter
    ///   - sticky notes: the Hangar (paper clips), the room picker, Options
    ///   - a title card, a scrap with your stars, a coffee-ring stain, the desk lamp's pool of light
    ///
    /// Everything is laid out on a 1280 x 720 design grid that is scaled to fit the screen
    /// (the desk itself fills the edges). Keyboard / gamepad input is unchanged (ModeMenu.Update).
    ///
    /// UNITY NOTE: fonts come from the operating system (Font.CreateDynamicFontFromOSFont), so no font
    /// files are needed; each list is tried in order and Unity falls back to its own font if none exist.
    /// GUIUtility.RotateAroundPivot tilts the papers; IMGUI clicks still land correctly on tilted buttons.
    /// </summary>
    public partial class ModeMenu
    {
        // ---- design grid ----
        const float DesignW = 1280f, DesignH = 720f;
        float ds, dox, doy;   // scale and offset of the design grid on screen

        Rect R(float x, float y, float w, float h) => new Rect(dox + x * ds, doy + y * ds, w * ds, h * ds);
        int Fs(float size) => Mathf.Max(1, Mathf.RoundToInt(size * ds));

        // ---- palette (from the design) ----
        static readonly Color DeskWood = new Color32(0x2b, 0x1d, 0x14, 0xff);
        static readonly Color DeskSeam = new Color32(0x22, 0x16, 0x0f, 0xff);
        static readonly Color Paper = new Color32(0xf6, 0xf1, 0xe4, 0xff);
        static readonly Color MemoPaper = new Color32(0xfb, 0xf8, 0xef, 0xff);
        static readonly Color Ink = new Color32(0x2a, 0x26, 0x20, 0xff);
        static readonly Color InkSoft = new Color32(0x5b, 0x51, 0x45, 0xff);
        static readonly Color Clipboard = new Color32(0x8a, 0x5a, 0x2e, 0xff);
        static readonly Color Clip = new Color32(0xb9, 0xbc, 0xc2, 0xff);
        static readonly Color StickyYellow = new Color32(0xff, 0xe0, 0x66, 0xff);
        static readonly Color StickyBlue = new Color32(0x9a, 0xd4, 0xf5, 0xff);
        static readonly Color StickyPink = new Color32(0xff, 0xad, 0xc7, 0xff);
        static readonly Color Highlighter = new Color32(0xff, 0xe0, 0x66, 0xff);
        static readonly Color RedPen = new Color32(0xa3, 0x32, 0x1f, 0xff);
        static readonly Color BrownNote = new Color32(0x8a, 0x5a, 0x10, 0xff);

        // ---- fonts, styles and textures (made once) ----
        static Font typewriter, handwriting, heavy;
        static Texture2D softDot, ring;
        GUIStyle typeStyle, handStyle, heavyStyle;

        void EnsureDeskAssets()
        {
            if (typewriter == null) typewriter = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas", "Lucida Console" }, 24);
            if (handwriting == null) handwriting = Font.CreateDynamicFontFromOSFont(new[] { "Ink Free", "Segoe Print", "Bradley Hand", "Comic Sans MS" }, 32);
            if (heavy == null) heavy = Font.CreateDynamicFontFromOSFont(new[] { "Arial Black", "Impact", "Arial" }, 48);
            if (softDot == null) softDot = MakeCircle(128, ringWidth: 0f);
            if (ring == null) ring = MakeCircle(128, ringWidth: 0.05f);
            if (typeStyle == null)
            {
                typeStyle = new GUIStyle(GUI.skin.label) { font = typewriter, richText = true, wordWrap = true, padding = new RectOffset() };
                handStyle = new GUIStyle(GUI.skin.label) { font = handwriting, richText = true, wordWrap = true, padding = new RectOffset() };
                heavyStyle = new GUIStyle(GUI.skin.label) { font = heavy, richText = true, wordWrap = false, padding = new RectOffset() };
            }
        }

        /// <summary>A white disc (soft-edged) or ring texture, for the lamp's light and the coffee stain.</summary>
        static Texture2D MakeCircle(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 centre .. 1 edge
                    float a = ringWidth <= 0f
                        ? Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)                        // soft pool
                        : Mathf.Clamp01(1f - Mathf.Abs(d - (1f - ringWidth)) / ringWidth) * (d <= 1f ? 1f : 0f);   // thin ring
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---- drawing helpers ----
        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Tex(Rect r, Texture2D t, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t);
            GUI.color = old;
        }

        /// <summary>A sheet of paper with a drop shadow under it.</summary>
        void Sheet(float x, float y, float w, float h, Color paper, float shadow = 10f)
        {
            Fill(R(x + shadow * 0.5f, y + shadow, w, h), new Color(0f, 0f, 0f, 0.38f));
            Fill(R(x, y, w, h), paper);
        }

        void Text(float x, float y, float w, float h, string text, GUIStyle style, float size, Color color,
                  TextAnchor anchor = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal)
        {
            style.fontSize = Fs(size);
            style.alignment = anchor;
            style.fontStyle = fontStyle;
            style.normal.textColor = color;
            GUI.Label(R(x, y, w, h), text, style);
        }

        /// <summary>Draws something tilted around a design-grid point (papers are never quite straight).</summary>
        void Tilted(float degrees, float cx, float cy, System.Action draw)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, new Vector2(dox + cx * ds, doy + cy * ds));
            draw();
            GUI.matrix = saved;
        }

        bool Hit(float x, float y, float w, float h) => GUI.Button(R(x, y, w, h), GUIContent.none, GUIStyle.none);

        // ------------------------------------------------------------------ the desk

        /// <summary>Fits the 1280 x 720 design grid to the screen (and loads the desk's fonts and textures).</summary>
        void BeginDesk()
        {
            EnsureDeskAssets();
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            ds = Mathf.Min(sw / DesignW, sh / DesignH);
            dox = (sw - DesignW * ds) * 0.5f;
            doy = (sh - DesignH * ds) * 0.5f;
        }

        /// <summary>The desk itself: wood with board seams across the whole screen, the lamp's light, a coffee ring.</summary>
        void DrawDeskSurface(bool coffeeRing = true)
        {
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            Fill(new Rect(0, 0, sw, sh), DeskWood);
            for (float bx = -Mathf.Ceil(dox / (160f * ds)) * 160f; dox + bx * ds < sw; bx += 160f)
                Fill(new Rect(dox + bx * ds, 0, Mathf.Max(1f, 3f * ds), sh), DeskSeam);
            Tex(R(180f, -260f, 1100f, 1000f), softDot, new Color(1f, 0.84f, 0.59f, 0.32f));
            if (coffeeRing) Tex(R(1010f, 560f, 150f, 150f), ring, new Color(0.27f, 0.16f, 0.06f, 0.5f));
        }

        void DrawMenu()
        {
            BeginDesk();
            DrawDeskSurface();
            bool rooms = director.RoomCount > 1;

            DrawTitleCard();
            DrawMemo();
            DrawHangarNote();
            if (rooms) DrawRoomNote();
            DrawOptionsNote();
            DrawStarsScrap();
            DrawHintStrip(rooms);
        }

        void DrawTitleCard()
        {
            Tilted(-2.5f, 305f, 140f, () =>
            {
                Sheet(70f, 54f, 470f, 172f, Paper, 14f);
                Text(100f, 70f, 420f, 118f, "PAPER\nWINGS", heavyStyle, 52f, new Color32(0x1f, 0x1b, 0x16, 0xff));
                Text(102f, 190f, 420f, 26f, "THE NIGHT SHIFT WAR", typeStyle, 18f, InkSoft);
            });
        }

        // ---- the memo on the clipboard: every way to play ----

        void DrawMemo()
        {
            Tilted(1f, 370f, 475f, () =>
            {
                Fill(R(118f, 262f, 520f, 450f), new Color(0f, 0f, 0f, 0.4f));   // board shadow
                Fill(R(110f, 250f, 520f, 450f), Clipboard);
                Fill(R(300f, 234f, 140f, 36f), Clip);
                Fill(R(300f, 262f, 140f, 8f), new Color32(0x8e, 0x91, 0x97, 0xff));
                Fill(R(132f, 280f, 476f, 398f), MemoPaper);

                Text(166f, 300f, 300f, 28f, "MEMO: TONIGHT'S SHIFT", typeStyle, 21f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(400f, 304f, 174f, 24f, "FROM: MARKETING", typeStyle, 13f, InkSoft, TextAnchor.UpperRight);
                Fill(R(166f, 332f, 408f, 2f), Ink);

                // Rows: the chosen one is taller and highlighted; the rest share what's left.
                int n = EntryCount;
                const float top = 344f, bottom = 668f, selH = 62f;
                float rowH = n > 1 ? Mathf.Min(46f, (bottom - top - selH) / (n - 1)) : selH;
                float y = top;
                for (int i = 0; i < n; i++)
                {
                    bool sel = i == selected;
                    float h = sel ? selH : rowH;
                    MemoRow(i, y, h, sel);
                    if (Hit(160f, y, 420f, h)) Choose(i);
                    y += h;
                }
            });
        }

        void MemoRow(int i, float y, float h, bool sel)
        {
            RowInfo(i, out string label, out string detail, out string note, out bool available);

            if (sel) Fill(R(158f, y + 5f, 424f, h - 10f), new Color(Highlighter.r, Highlighter.g, Highlighter.b, available ? 0.95f : 0.45f));

            Color ink = available ? Ink : new Color32(0x8b, 0x81, 0x75, 0xff);
            Text(168f, y + (sel ? 9f : 10f), 30f, 26f, $"{i + 1}.", typeStyle, 17f, ink);
            Text(202f, y + (sel ? 6f : 8f), 260f, 30f, label, typeStyle, sel ? 24f : 21f, ink, TextAnchor.UpperLeft, sel ? FontStyle.Bold : FontStyle.Normal);
            if (sel && detail.Length > 0)
                Text(202f, y + 34f, 370f, 22f, detail, typeStyle, 13f, new Color32(0x4b, 0x42, 0x37, 0xff));

            if (!available)
            {
                // Crossed out, with a note in red pen.
                Fill(R(200f, y + (sel ? 20f : 21f), Mathf.Min(240f, label.Length * 13f), 2f), new Color32(0x8b, 0x81, 0x75, 0xff));
                Text(380f, y + 6f, 196f, 30f, "not in this room", handStyle, 19f, RedPen, TextAnchor.UpperRight);
            }
            else if (note.Length > 0)
                Text(380f, y + (sel ? 10f : 12f), 196f, 24f, note, typeStyle, 13f, BrownNote, TextAnchor.UpperRight);
            if (sel) Text(548f, y + 16f, 26f, 28f, "►", typeStyle, 16f, Ink, TextAnchor.UpperRight);
        }

        /// <summary>What a memo line says: its name, a one-line detail (shown when chosen), a note on the right.</summary>
        void RowInfo(int i, out string label, out string detail, out string note, out bool available)
        {
            available = true;
            note = "";
            if (i == TrainingEntry)
            {
                bool done = ModeDirector.TrainingDone;
                label = "Flight School";
                detail = done ? "Fly the lessons again, fully upgraded." : "Learn to throw, steer, climb and fight.";
                note = done ? "passed" : "start here";
                return;
            }
            if (i == CampaignEntry)
            {
                int next = FirstUnclearedNight();
                var night = Campaign.Get(next);
                bool allDone = Progression.NightCleared(Campaign.Count - 1);
                label = "Campaign";
                detail = allDone ? "Every night cleared. Go back for three stars."
                       : night != null ? $"Next: Night {next + 1} · {night.title} ({director.RoomName(night.room).ToLower()})" : "";
                note = $"★ {Progression.TotalStars()}/{Campaign.Count * 3}";
                return;
            }

            GameMode m = Modes[i - Offset];
            label = ModeDirector.ModeName(m);
            available = director.RoomSupports(roomSelected, m);
            detail = available ? ShortDescribe(m) : $"Not in the {director.RoomName(roomSelected)}.";
            if (!available) return;

            int medal = Progression.GetMedal(ModeDirector.MedalKey(m, roomSelected));
            float best = director.BestTime(m, roomSelected);
            var route = director.RouteOf(roomSelected);
            if (m == GameMode.RingRun && route != null && route.BestTime > 0f) best = route.BestTime;
            string medalName = medal > 0 ? ProgressionSettings.MedalName(medal).ToLower() : "";
            string time = best > 0f ? $"{best:0.0}s" : "";
            note = medalName.Length > 0 && time.Length > 0 ? $"{medalName} · {time}" : medalName + time;
        }

        /// <summary>One short typed line per mode (the memo has room for about 45 characters).</summary>
        static string ShortDescribe(GameMode m) => m switch
        {
            GameMode.FreeFlight => "No rivals, no clock. Just ride the air.",
            GameMode.RingRun => "Every ring, as fast as you can.",
            GameMode.Dogfight => "Shoot down five of Accounting's planes.",
            GameMode.Squadron => "You and two wingmen vs. a squadron.",
            _ => "Each room's boss."
        };

        // ---- sticky notes ----

        void DrawHangarNote()
        {
            if (director.ProgressionConfig == null) return;
            Tilted(3f, 875f, 220f, () =>
            {
                Sheet(760f, 120f, 230f, 200f, StickyYellow, 9f);
                Text(782f, 134f, 190f, 46f, "Hangar", handStyle, 38f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(782f, 182f, 190f, 60f, "upgrades for each fold", handStyle, 22f, Ink);
                Text(782f, 250f, 190f, 38f, $"{Progression.PaperClips} clips", handStyle, 28f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(782f, 290f, 190f, 20f, "press U · (Y)", typeStyle, 13f, new Color32(0x5c, 0x4a, 0x10, 0xff));
                if (Hit(760f, 120f, 230f, 200f)) OpenUnlocks();
            });
        }

        void DrawRoomNote()
        {
            Tilted(-4f, 1115f, 275f, () =>
            {
                Sheet(1010f, 180f, 210f, 190f, StickyBlue, 9f);
                Color navy = new Color32(0x1f, 0x3d, 0x52, 0xff);
                Text(1028f, 192f, 180f, 32f, "Fly in the…", handStyle, 24f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(1028f, 228f, 44f, 44f, "◀", typeStyle, 22f, navy, TextAnchor.MiddleCenter);
                Text(1158f, 228f, 44f, 44f, "▶", typeStyle, 22f, navy, TextAnchor.MiddleCenter);
                Text(1066f, 230f, 98f, 40f, director.RoomName(roomSelected), handStyle, 26f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                var room = director.RoomAt(roomSelected);
                if (room != null && room.description.Length > 0)
                    Text(1028f, 282f, 176f, 80f, room.description, handStyle, 16f, navy);
                if (Hit(1024f, 226f, 48f, 48f)) ChangeRoom(-1);
                if (Hit(1156f, 226f, 48f, 48f)) ChangeRoom(+1);
            });
        }

        void DrawOptionsNote()
        {
            Tilted(-2f, 895f, 475f, () =>
            {
                Sheet(800f, 400f, 190f, 150f, StickyPink, 9f);
                Text(820f, 414f, 160f, 42f, "Options", handStyle, 34f, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Text(820f, 456f, 160f, 50f, "sound, controls, brightness", handStyle, 18f, Ink);
                Text(820f, 502f, 160f, 24f, $"difficulty: {Difficulty.Name(Difficulty.Current).ToLower()}", handStyle, 17f, RedPen);
                Text(820f, 528f, 160f, 18f, "press O", typeStyle, 12f, new Color32(0x6a, 0x2a, 0x40, 0xff));
                if (Hit(800f, 400f, 190f, 150f)) OpenOptions();
            });
        }

        void DrawStarsScrap()
        {
            if (Campaign == null || Campaign.Count == 0) return;
            Tilted(5f, 1120f, 442f, () =>
            {
                Sheet(1040f, 420f, 170f, 44f, Paper, 7f);
                Text(1040f, 420f, 170f, 44f, $"★ {Progression.TotalStars()} / {Campaign.Count * 3} stars", typeStyle, 17f, Ink, TextAnchor.MiddleCenter);
            });
        }

        void DrawHintStrip(bool rooms)
        {
            string resume = director.HasMode && !director.RunComplete ? " · Esc resume" : "";
            string hint = "↑↓ choose · Enter fly · U hangar · O options" + (rooms ? " · ◀▶ room" : "") + resume;
            Fill(R(660f, 648f, 580f, 36f), new Color(Paper.r, Paper.g, Paper.b, 0.9f));
            Text(660f, 648f, 580f, 36f, hint, typeStyle, 14f, new Color32(0x3b, 0x34, 0x2b, 0xff), TextAnchor.MiddleCenter);
        }
    }
}
