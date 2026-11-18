using System;
using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// The plane is paper: bumps wear it down and steam soaks it.
    ///  - Integrity: every glancing bump costs paper. Less paper = worse glide, sluggish
    ///    controls and a bent wing that pulls to one side. At zero it crumples (a crash).
    ///  - Wetness: rises in wet air (steam), falls over time and faster in dry heat (laptop).
    ///    Wet paper sinks faster and handles worse.
    ///  - Visuals: the mesh crumples, the paper discolours, and bits fly off on impact.
    /// Every throw starts with a fresh, dry plane.
    ///
    /// It never touches FlightSettings. It writes PlaneController.Modifiers each frame instead.
    /// </summary>
    [RequireComponent(typeof(PlaneController))]
    public class PaperDamage : MonoBehaviour
    {
        [SerializeField] PaperSettings settings;
        [Tooltip("The dart's visual mesh (on the 'Visual' child). It gets crumpled at runtime.")]
        [SerializeField] MeshFilter visualMesh;
        [SerializeField] ParticleSystem tearBits;

        /// <summary>Fired when damage is taken. Argument: amount.</summary>
        public event Action<float> Damaged;

        public float Integrity01 => settings != null ? integrity / settings.maxIntegrity : 1f;
        public float Wetness01 => wetness;

        PlaneController plane;
        Renderer visualRenderer;
        MaterialPropertyBlock block;
        Color paperColor = Color.white;

        float integrity;
        float wetness;
        float bentSide;   // -1 / +1 once bent, 0 = straight

        Mesh mesh;
        Vector3[] baseVertices, workVertices;
        float shownCrumple = -1f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            plane = GetComponent<PlaneController>();
            integrity = settings != null ? settings.maxIntegrity : 100f;
            block = new MaterialPropertyBlock();

            if (visualMesh != null)
            {
                visualRenderer = visualMesh.GetComponent<Renderer>();
                if (visualRenderer != null && visualRenderer.sharedMaterial != null && visualRenderer.sharedMaterial.HasProperty(BaseColorId))
                    paperColor = visualRenderer.sharedMaterial.GetColor(BaseColorId);
                SetVisualMesh(visualMesh.sharedMesh);
            }
        }

        /// <summary>
        /// Switches to another fold's paper: new settings, new mesh, new base colour. Resets to a fresh sheet.
        /// </summary>
        public void Configure(PaperSettings newSettings, Mesh sourceMesh, Color baseColor)
        {
            if (newSettings != null) settings = newSettings;
            paperColor = baseColor;
            SetVisualMesh(sourceMesh);
            Repair();
        }

        void SetVisualMesh(Mesh source)
        {
            if (visualMesh == null || source == null) return;
            if (mesh != null) Destroy(mesh);   // drop our previous runtime copy

            // Work on a private copy so we never modify the mesh asset on disk.
            mesh = Instantiate(source);
            mesh.name = source.name + " (crumpled)";
            visualMesh.sharedMesh = mesh;
            baseVertices = mesh.vertices;
            workVertices = new Vector3[baseVertices.Length];
            shownCrumple = -1f;   // force a rebuild
        }

        void OnEnable()  { if (plane) plane.Bumped += OnBumped; }
        void OnDisable() { if (plane) plane.Bumped -= OnBumped; }

        void OnBumped(float impactSpeed)
        {
            if (settings == null || !settings.damageEnabled) return;
            ApplyDamage(settings.bumpBaseDamage + impactSpeed * settings.bumpDamagePerImpact);
        }

        /// <summary>The plane that last damaged us (null for bumps), and when. Used to credit kills.</summary>
        public PlaneController LastAttacker { get; private set; }
        public float LastAttackTime { get; private set; }

        /// <summary>Removes paper. 'source' = the plane responsible (e.g. whoever fired the staple), if any.</summary>
        public void ApplyDamage(float amount, PlaneController source = null)
        {
            if (settings == null || plane.State != PlaneState.Flying || amount <= 0f) return;

            if (source != null)
            {
                LastAttacker = source;
                LastAttackTime = Time.time;
            }
            integrity = Mathf.Max(0f, integrity - amount);
            if (bentSide == 0f) bentSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            if (tearBits != null) tearBits.Emit(Mathf.RoundToInt(4f + amount * 0.6f));
            Damaged?.Invoke(amount);

            if (integrity <= 0f)
                plane.GoDown(crash: true, reason: source != null ? "Shot down! Stapled to pieces." : "Crumpled! Too many bumps.");
        }

        public void Repair()
        {
            if (settings != null) integrity = settings.maxIntegrity;
            wetness = 0f;
            bentSide = 0f;
            plane.Modifiers.Reset();
        }

        void Update()
        {
            if (settings == null) return;

            // Back in hand = fresh sheet of paper.
            if (plane.State == PlaneState.Held && (integrity < settings.maxIntegrity || wetness > 0f || shownCrumple > 0f))
                Repair();

            if (plane.State == PlaneState.Flying) UpdateWetness(Time.deltaTime);
            ApplyFlightModifiers();
            UpdateVisuals();
        }

        void UpdateWetness(float dt)
        {
            if (!settings.wetnessEnabled) { wetness = 0f; return; }
            var air = plane.Air;
            // Soak in wet air. Dry slowly anywhere and quickly in DRY heat (the laptop), but never
            // while you're still in the wet air: being in steam should only ever make you wetter.
            float soak = settings.wetFromSteam * air.moisture;
            float dry = (settings.dryRate + settings.heatDryRate * air.dryHeat) * (1f - air.moisture);
            wetness = Mathf.Clamp01(wetness + (soak - dry) * dt);
        }

        void ApplyFlightModifiers()
        {
            float damage01 = 1f - Integrity01;
            var m = plane.Modifiers;
            m.glideScale = Mathf.Lerp(1f, settings.glideAtZero, damage01);
            m.controlScale = Mathf.Lerp(1f, settings.controlAtZero, damage01) * Mathf.Lerp(1f, settings.wetControlScale, wetness);
            m.rollBias = bentSide * settings.maxRollBias * damage01;
            m.extraSink = settings.wetExtraSink * wetness;
        }

        void UpdateVisuals()
        {
            float damage01 = 1f - Integrity01;

            // Crumple: only rebuild the mesh when the damage visibly changed.
            if (mesh != null && Mathf.Abs(damage01 - shownCrumple) > 0.01f)
            {
                float amount = settings.crumpleAmount * damage01;
                for (int i = 0; i < baseVertices.Length; i++)
                {
                    Vector3 v = baseVertices[i];
                    // Noise from the vertex's own position: corners shared by several faces
                    // (stored as separate vertices for flat shading) move together, so no cracks.
                    Vector3 n = Noise3(v);
                    workVertices[i] = v + new Vector3(n.x * 0.4f, n.y, n.z * 0.25f) * amount;
                }
                mesh.vertices = workVertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                shownCrumple = damage01;
            }

            // Colour: worn paper yellows, wet paper goes grey-blue.
            if (visualRenderer != null)
            {
                Color c = Color.Lerp(paperColor, settings.damagedColor, damage01);
                c = Color.Lerp(c, settings.wetColor, wetness * 0.8f);
                block.SetColor(BaseColorId, c);
                visualRenderer.SetPropertyBlock(block);
            }
        }

        static Vector3 Noise3(Vector3 p)
        {
            float x = Mathf.PerlinNoise(p.x * 4.1f + 11.3f, p.z * 3.7f + 2.9f) * 2f - 1f;
            float y = Mathf.PerlinNoise(p.z * 4.7f + 5.1f, p.x * 5.3f + 7.7f) * 2f - 1f;
            float z = Mathf.PerlinNoise(p.y * 6.1f + 3.3f, p.x * 2.9f + 13.1f) * 2f - 1f;
            return new Vector3(x, y, z);
        }
    }
}
