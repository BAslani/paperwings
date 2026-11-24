using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 23: scale, air colours, faction looks.
    /// Menu: PaperWings > Stage Builders > Stage 23 (Scale, Air Colours, Factions)
    ///
    ///  - WORLD SCALE: props are built at about 1 unit = 2.3 cm (the desk is 70 units = 160 cm), so the
    ///    dart (2 units) reads as a ~5 cm plane in a big office. This stage fixes the outliers at that
    ///    scale: the office and break-room doors (were ~90 cm tall, now ~2 m), the filing cabinets
    ///    (were shorter than the desk) and the mouse (too small). The printer stand moves into the
    ///    corner to make room for a radiator (Stage 24).
    ///  - AIR COLOURS: every air effect now takes its hue from AirPalette (heat orange, steam white,
    ///    updraft green, draft cyan, downdraft violet). The settings assets get the palette too.
    ///  - FACTIONS: Accounting's rivals fly green-bar ledger printouts with red trails; Marketing's
    ///    wingmen fly glossy flyers with blue trails. AI trails stay faintly on at any speed, so you
    ///    can tell friend from foe at a glance.
    /// Safe to re-run.
    /// </summary>
    public static class Stage23Builder
    {
        const string Root = StageKit.Root;

        [MenuItem("PaperWings/Stage Builders/Stage 23 (Scale, Air Colours, Factions)", priority = 123)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            Run(scene);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 23 built.\n\n- Doors, filing cabinets and the mouse are at the world scale now.\n" +
                "- Air effects use one colour per kind: heat orange, steam white, updraft green, draft cyan, downdraft violet.\n" +
                "- Rivals: ledger paper + red trails. Wingmen: flyer paper + blue trails.", "Nice");
        }

        internal static void Run(Scene scene)
        {
            FixScale(scene);
            PaletteSettings();
            FactionLooks();
        }

        // ------------------------------------------------------------------ scale

        static void FixScale(Scene scene)
        {
            const float Left = -170f, Right = 170f, Front = -150f, DeskTop = 30f;

            // Office door (left wall): a 2 m door is ~86 units at this scale.
            var dressing = Find(scene, "OfficeRoom", "Dressing");
            if (dressing != null)
            {
                SetQuad(dressing, "DoorFrame", new Vector3(Left + 0.05f, 44f, Front + 30f), 38f, 88f);
                SetQuad(dressing, "Door", new Vector3(Left + 0.1f, 43f, Front + 30f), 34f, 86f);

                // Filing cabinets: four-drawer cabinets (~1.3 m) stand well above the desk.
                int i = 0;
                foreach (Transform child in dressing)
                {
                    if (child.name != "FilingCabinet") continue;
                    child.position = new Vector3(Right - 11f, 26f, -52f + i * 22f);
                    child.localScale = new Vector3(20f, 52f, 21f);
                    i++;
                }
            }

            // The mouse: ~6 x 10 cm.
            var mouse = Find(scene, "Props", "Mouse");
            if (mouse != null)
            {
                mouse.localScale = new Vector3(2.6f, 1.1f, 4.2f);
                mouse.position = new Vector3(mouse.position.x, DeskTop + 0.55f, mouse.position.z);
            }

            // Printer stand into the back-right corner (the right window gets a radiator in Stage 24).
            var furniture = Find(scene, "OfficeRoom", "Furniture");
            if (furniture != null)
            {
                foreach (var n in new[] { "PrinterStand", "BigPrinter", "BigPrinterTray" })
                {
                    var t = furniture.Find(n);
                    if (t != null) t.position = new Vector3(152f, t.position.y, t.position.z);
                }
            }

            // Break room door.
            var shell = Find(scene, "BreakRoom", "Shell");
            if (shell != null)
            {
                var door = shell.Find("Door");
                if (door != null)
                {
                    door.position = new Vector3(600f - 70f, 43f, -110.9f);
                    door.localScale = new Vector3(34f, 86f, 0.2f);
                }
            }
        }

        static void SetQuad(Transform parent, string name, Vector3 pos, float w, float h)
        {
            var t = parent.Find(name);
            if (t == null) return;
            t.position = pos;
            t.localScale = new Vector3(w, h, 1f);
        }

        static Transform Find(Scene scene, string root, string child)
        {
            var r = StageKit.FindRoot(scene, root);
            return r != null ? r.Find(child) : null;
        }

        // ------------------------------------------------------------------ air colours

        static void PaletteSettings()
        {
            // The runtime forces the hue anyway; this keeps the assets honest in the Inspector.
            foreach (var path in new[] { "Settings/Thermal_Laptop.asset", "Settings/Thermal_Microwave.asset" })
            {
                var s = StageKit.Load<ThermalSettings>(path);
                if (s == null) continue;
                s.shimmerColor = AirPalette.Of(AirLook.Heat, s.shimmerColor.a);
                EditorUtility.SetDirty(s);
            }
            var steam = StageKit.Load<SteamSettings>("Settings/Steam_Tea.asset");
            if (steam != null) { steam.steamColor = AirPalette.Of(AirLook.Steam, steam.steamColor.a); EditorUtility.SetDirty(steam); }
            var fan = StageKit.Load<FanSettings>("Settings/Fan_Desk.asset");
            if (fan != null) { fan.streakColor = AirPalette.Of(AirLook.Draft, fan.streakColor.a); EditorUtility.SetDirty(fan); }
            var vent = StageKit.Load<VentSettings>("Settings/Vent_Ceiling.asset");
            if (vent != null) { vent.streakColor = AirPalette.Of(AirLook.Downdraft, vent.streakColor.a); EditorUtility.SetDirty(vent); }
        }

        // ------------------------------------------------------------------ factions

        static void FactionLooks()
        {
            var ledger = TextureFactory.LedgerPaper();
            var flyer = TextureFactory.FlyerPaper();
            BuilderUtil.CreateMaterial("M_PaperRival", new Color(1f, 0.72f, 0.66f), ledger, Vector2.one);
            BuilderUtil.CreateMaterial("M_PaperWingman", new Color(0.82f, 0.9f, 1f), flyer, Vector2.one);

            Trails("Prefabs/RivalDart.prefab", 0.35f, new Color(1f, 0.3f, 0.2f), new Color(1f, 0.55f, 0.25f));
            Trails("Prefabs/WingmanDart.prefab", 0.35f, new Color(0.35f, 0.65f, 1f), new Color(0.6f, 0.85f, 1f));
            Trails("Prefabs/PaperDart.prefab", 0f, Color.white, new Color(0.7f, 0.85f, 1f));   // you: white, speed only
        }

        static void Trails(string prefabPath, float alwaysOn, Color head, Color tail)
        {
            string path = Root + prefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var fx = root.GetComponent<PlaneTrails>();
            if (fx != null)
            {
                var so = new SerializedObject(fx);
                so.FindProperty("alwaysOn").floatValue = alwaysOn;
                so.FindProperty("trailColor").colorValue = head;
                so.FindProperty("trailEndColor").colorValue = tail;
                so.ApplyModifiedPropertiesWithoutUndo();
                fx.ApplyColors();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
