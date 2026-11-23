using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.UI
{
    /// <summary>Which upgrades a preview shows fitted to the plane.</summary>
    public struct PreviewLoadout
    {
        public int pencils;        // 0, 2 or 4
        public bool propeller;
        public bool magazine;
        public bool heavyPaper;

        public bool Equals(PreviewLoadout o) =>
            pencils == o.pencils && propeller == o.propeller && magazine == o.magazine && heavyPaper == o.heavyPaper;
    }

    /// <summary>
    /// The hangar preview (Stage 28): a turntable with a 3D model of a fold and its upgrades, filmed
    /// by its own camera into a texture that the menus draw (the Unlocks screen and the in-hand fold card).
    ///
    /// How it works: the turntable lives far below the level (no one else can see it), and its camera
    /// renders only while some menu asked for the picture this frame. Upgrades you own are fitted in
    /// normal colours; the upgrade you're looking at in the shop (if you don't own it yet) is shown as a
    /// glowing blue "blueprint", so you see what you'd be buying and where it goes.
    ///
    /// Menus call <see cref="Request"/> every OnGUI, then draw <see cref="Texture"/>. Everything spins on
    /// unscaled time, so it keeps turning while the game is paused behind the menu.
    /// </summary>
    public class PlanePreview : MonoBehaviour
    {
        [Header("Materials (set by Build Stage 28)")]
        [Tooltip("The plane's paper material (tinted and textured per fold).")]
        [SerializeField] Material paperMaterial;
        [Tooltip("Glowing blueprint material for upgrades you don't own yet.")]
        [SerializeField] Material ghostMaterial;
        [SerializeField] Material pencilBody;
        [SerializeField] Material pencilTip;
        [SerializeField] Material pencilEraser;
        [SerializeField] Material magazineMetal;
        [Tooltip("The propeller (falls back to the fold picker's).")]
        [SerializeField] GameObject propellerPrefab;

        [Header("Stand")]
        [SerializeField] int textureSize = 512;
        [SerializeField] Color background = new Color(0.1f, 0.11f, 0.16f, 1f);
        [Tooltip("Turntable speed (degrees/sec).")]
        [SerializeField] float spinSpeed = 40f;

        public RenderTexture Texture { get; private set; }

        static readonly Vector3 StageOrigin = new Vector3(0f, -3000f, 0f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Transform stage, pivot, model;
        Camera cam;
        int lastRequestFrame = -10;
        float extraYaw;          // mouse drag
        PlaneFold shownFold;
        PreviewLoadout shownOwned, shownGhost;
        Renderer[] ghostRenderers = new Renderer[0];
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            BuildStage();
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (stage != null) Destroy(stage.gameObject);
        }

        void BuildStage()
        {
            stage = new GameObject("PlanePreviewStage").transform;
            stage.position = StageOrigin;
            pivot = new GameObject("Turntable").transform;
            pivot.SetParent(stage, false);

            Texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32) { name = "PlanePreview", antiAliasing = 4 };
            Texture.Create();

            cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            cam.transform.SetParent(stage, false);
            cam.transform.localPosition = new Vector3(0f, 1.5f, -4.6f);
            cam.transform.LookAt(stage.position + Vector3.down * 0.05f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 25f;   // sees the stand only, never the level
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.targetTexture = Texture;
            cam.enabled = false;

            // Lights: point lights only (a directional light would light the whole level).
            AddLight("Key", new Vector3(-2.5f, 3f, -3f), new Color(1f, 0.95f, 0.85f), 6f);
            AddLight("Rim", new Vector3(2.5f, 1.5f, 3f), new Color(0.6f, 0.75f, 1f), 4f);
            AddLight("Fill", new Vector3(0f, -2f, -2f), new Color(0.9f, 0.85f, 1f), 1.5f);
        }

        void AddLight(string n, Vector3 local, Color c, float intensity)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(stage, false);
            l.transform.localPosition = local;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = 12f;
            l.shadows = LightShadows.None;
        }

        /// <summary>The upgrades a fold owns right now (from the save).</summary>
        public static PreviewLoadout OwnedLoadout(PlaneFold fold)
        {
            bool Has(string up) => fold != null && Progression.IsUnlocked(UnlockIds.ForFold(fold, up));
            return new PreviewLoadout
            {
                pencils = Has(UnlockIds.Pencils) ? (Has(UnlockIds.PencilRack) ? 4 : 2) : 0,
                propeller = Has(UnlockIds.Propeller),
                magazine = Has(UnlockIds.StapleMagazine),
                heavyPaper = Has(UnlockIds.HeavyPaper)
            };
        }

        /// <summary>A loadout with just the upgrade behind this shop id (for the blueprint), e.g. "dart:pencils_plus".</summary>
        public static PreviewLoadout LoadoutFor(string unlockId)
        {
            var l = new PreviewLoadout();
            if (string.IsNullOrEmpty(unlockId)) return l;
            string up = unlockId.Contains(":") ? unlockId.Substring(unlockId.IndexOf(':') + 1) : unlockId;
            if (up == UnlockIds.Pencils) l.pencils = 2;
            else if (up == UnlockIds.PencilRack) l.pencils = 4;
            else if (up == UnlockIds.Propeller) l.propeller = true;
            else if (up == UnlockIds.StapleMagazine) l.magazine = true;
            else if (up == UnlockIds.HeavyPaper) l.heavyPaper = true;
            return l;
        }

        /// <summary>
        /// Ask for a picture this frame: <paramref name="owned"/> upgrades fitted normally,
        /// <paramref name="ghost"/> upgrades shown as a glowing blueprint (pass default for none).
        /// </summary>
        public void Request(PlaneFold fold, PreviewLoadout owned, PreviewLoadout ghost = default)
        {
            lastRequestFrame = Time.frameCount;
            if (fold != shownFold || !owned.Equals(shownOwned) || !ghost.Equals(shownGhost))
            {
                shownFold = fold;
                shownOwned = owned;
                shownGhost = ghost;
                Rebuild();
            }
            if (!cam.enabled) cam.enabled = true;
        }

        /// <summary>Spin the turntable by hand (e.g. a mouse drag over the picture).</summary>
        public void Drag(float degrees) => extraYaw += degrees;

        void LateUpdate()
        {
            // Nobody asked for a picture recently: stop rendering it.
            if (cam != null && cam.enabled && Time.frameCount - lastRequestFrame > 2) cam.enabled = false;
            if (pivot == null || !cam.enabled) return;

            float dt = Time.unscaledDeltaTime;
            pivot.localRotation = Quaternion.Euler(0f, spinSpeed * dt + extraYaw, 0f) * pivot.localRotation;
            extraYaw = 0f;

            // Blueprint parts pulse.
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f);
            block.Clear();
            block.SetColor(EmissionId, new Color(0.35f, 0.7f, 1.4f) * pulse);
            foreach (var r in ghostRenderers) if (r != null) r.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ the model

        void Rebuild()
        {
            if (model != null) Destroy(model.gameObject);
            var ghosts = new System.Collections.Generic.List<Renderer>();
            if (shownFold == null || shownFold.mesh == null) { ghostRenderers = new Renderer[0]; return; }

            model = new GameObject("Model").transform;
            model.SetParent(pivot, false);
            // A slight nose-down, banked pose reads better than flat side-on.
            model.localRotation = Quaternion.Euler(12f, 0f, -8f);
            Bounds b = shownFold.mesh.bounds;
            float size = Mathf.Max(b.size.x, b.size.z);
            float fit = 2.2f / Mathf.Max(0.5f, size);
            model.localScale = Vector3.one * fit;
            model.localPosition = -new Vector3(b.center.x, 0f, b.center.z) * fit;

            // Paper.
            var paper = Part("Paper", null, Vector3.zero, Vector3.zero, Vector3.one, paperMaterial);
            paper.AddComponent<MeshFilter>().sharedMesh = shownFold.mesh;
            TintPaper(paper.GetComponent<Renderer>(), shownFold.paperColor);

            // Heavy paper: a second, thicker sheet under the first.
            bool heavyGhost = shownGhost.heavyPaper && !shownOwned.heavyPaper;
            if (shownOwned.heavyPaper || heavyGhost)
            {
                var under = Part("HeavyPaper", null, new Vector3(0f, -0.045f, 0f), Vector3.zero, new Vector3(1.02f, 1f, 1.02f),
                                 heavyGhost ? ghostMaterial : paperMaterial);
                under.AddComponent<MeshFilter>().sharedMesh = shownFold.mesh;
                if (heavyGhost) ghosts.Add(under.GetComponent<Renderer>());
                else TintPaper(under.GetComponent<Renderer>(), shownFold.paperColor * 0.82f);
            }

            // Pencils under the wings (owned ones, then blueprint ones in the remaining slots).
            int owned = shownOwned.pencils;
            int wanted = Mathf.Max(owned, shownGhost.pencils);
            float half = b.extents.x;
            float[] xs = { -0.35f, 0.35f, -0.62f, 0.62f };
            for (int i = 0; i < Mathf.Min(wanted, 4); i++)
                Pencil(new Vector3(b.center.x + xs[i] * half, b.min.y - 0.06f, b.center.z - 0.05f), i >= owned, ghosts);

            // Staple magazine: a little clip under the nose.
            bool magGhost = shownGhost.magazine && !shownOwned.magazine;
            if (shownOwned.magazine || magGhost)
            {
                Material m = magGhost ? ghostMaterial : magazineMetal;
                float noseZ = b.max.z - 0.45f;
                var box = Prim(PrimitiveType.Cube, "Magazine", new Vector3(0f, b.min.y - 0.07f, noseZ), Vector3.zero, new Vector3(0.16f, 0.09f, 0.4f), m);
                var strip = Prim(PrimitiveType.Cube, "MagazineStrip", new Vector3(0f, b.min.y - 0.12f, noseZ), Vector3.zero, new Vector3(0.1f, 0.02f, 0.36f), m);
                if (magGhost) { ghosts.Add(box.GetComponent<Renderer>()); ghosts.Add(strip.GetComponent<Renderer>()); }
            }

            // Propeller on the tail.
            bool propGhost = shownGhost.propeller && !shownOwned.propeller;
            GameObject prefab = propellerPrefab;
            if (prefab == null)
            {
                var selector = FindAnyObjectByType<FoldSelector>();
                if (selector != null) prefab = selector.PropellerPrefab;
            }
            if ((shownOwned.propeller || propGhost) && prefab != null)
            {
                var prop = Instantiate(prefab, model, false);
                prop.transform.localPosition = shownFold.propellerMount;
                foreach (var c in prop.GetComponentsInChildren<Collider>()) Destroy(c);
                if (propGhost)
                    foreach (var r in prop.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = ghostMaterial;
                        ghosts.Add(r);
                    }
            }

            ghostRenderers = ghosts.ToArray();
        }

        void TintPaper(Renderer r, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, c);
            if (shownFold.paperTexture != null) mpb.SetTexture(BaseMapId, shownFold.paperTexture);
            r.SetPropertyBlock(mpb);
        }

        void Pencil(Vector3 at, bool ghost, System.Collections.Generic.List<Renderer> ghosts)
        {
            Material body = ghost ? ghostMaterial : pencilBody;
            Material tip = ghost ? ghostMaterial : pencilTip;
            Material eraser = ghost ? ghostMaterial : pencilEraser;
            // Cylinders stand on Y; rotate 90 on X so they lie nose-to-tail.
            var p1 = Prim(PrimitiveType.Cylinder, "Pencil", at, new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.3f, 0.07f), body);
            var p2 = Prim(PrimitiveType.Cylinder, "PencilTip", at + new Vector3(0f, 0f, 0.34f), new Vector3(90f, 0f, 0f), new Vector3(0.045f, 0.05f, 0.045f), tip);
            var p3 = Prim(PrimitiveType.Cylinder, "PencilEraser", at + new Vector3(0f, 0f, -0.33f), new Vector3(90f, 0f, 0f), new Vector3(0.072f, 0.035f, 0.072f), eraser);
            if (ghost) { ghosts.Add(p1.GetComponent<Renderer>()); ghosts.Add(p2.GetComponent<Renderer>()); ghosts.Add(p3.GetComponent<Renderer>()); }
        }

        GameObject Prim(PrimitiveType type, string n, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = n;
            go.transform.SetParent(model, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        GameObject Part(string n, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent != null ? parent : model, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
