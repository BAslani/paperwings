using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>
    /// F1: a small hidden testing panel that also works in a built game (the PaperWings > Dev menu
    /// only exists inside the Unity Editor). Free clips, unlock everything, open every night,
    /// reset progress. Untick "Available In Builds" before giving the game to players.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        [SerializeField] ModeDirector director;
        [Tooltip("Allow F1 in built players (always available in the Editor).")]
        [SerializeField] bool availableInBuilds = true;

        bool open;
        bool confirmReset;
        string message = "";

        void Update()
        {
            if (!Application.isEditor && !availableInBuilds) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) { open = !open; confirmReset = false; message = ""; }
        }

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -20;   // above the menus
            float s = Screen.height / 1080f;
            var r = new Rect(20 * s, 20 * s, 340 * s, 330 * s);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), richText = true };
            var btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(17 * s) };
            float x = r.x + 14 * s, w = r.width - 28 * s, y = r.y + 10 * s, h = 36 * s;

            GUI.Label(new Rect(x, y, w, 30 * s), "<b>DEV PANEL</b>  <color=#999999>(F1 to close)</color>", label);
            y += 36 * s;

            if (GUI.Button(new Rect(x, y, w, h), "+500 paper clips", btn)) { Progression.AddClips(500, "dev"); message = "+500 clips"; }
            y += h + 6 * s;

            var cfg = director != null ? director.ProgressionConfig : null;
            GUI.enabled = cfg != null;
            if (GUI.Button(new Rect(x, y, w, h), "Unlock every fold and upgrade", btn))
            {
                Progression.UnlockAll(cfg.AllItems().Select(u => u.id).Append(UnlockIds.Staples));
                message = "Everything unlocked";
            }
            y += h + 6 * s;

            var campaign = director != null ? director.Campaign : null;
            GUI.enabled = campaign != null;
            if (GUI.Button(new Rect(x, y, w, h), "Open every campaign night", btn))
            {
                Progression.ClearNights(campaign.Count - 1);
                message = "All nights open";
            }
            y += h + 6 * s;
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, y, w, h), confirmReset ? "Click again to RESET everything" : "Reset progress", btn))
            {
                if (confirmReset) { Progression.ResetAll(); message = "Progress reset"; confirmReset = false; }
                else confirmReset = true;
            }
            y += h + 10 * s;

            GUI.Label(new Rect(x, y, w, 28 * s), $"<color=#FFD040>{Progression.PaperClips} clips</color>   <color=#80FF90>{message}</color>", label);
            y += 30 * s;
            GUI.Label(new Rect(x, y, w, 28 * s), $"<color=#888888>{1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} fps</color>", label);
        }
    }
}
