using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>Helpers for Stages 29-32 (missions, room events, boss parts). See StageKit for the basics.</summary>
    public static class StageKit29
    {
        public const string P = StageKit.Root + "Prefabs/";
        public const string S = StageKit.Root + "Settings/";

        /// <summary>A see-through URP material (boss shields, glass).</summary>
        public static Material Glass(string name, Color color)
        {
            string path = StageKit.Root + "Art/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A one-shot burst of paper bits (or confetti, with a colourful material).</summary>
        public static ParticleSystem Burst(Transform parent, string name, Vector3 localPos, int count, float speedMax, float size, Material mat = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = Stage2Builder.AddParticles(go, mat != null ? mat : Stage2Builder.ParticleMaterial("M_PaperBits", null, additive: false), false, size * 0.4f, size);
            var main = ps.main;
            main.loop = false; main.prewarm = false; main.playOnAwake = false;
            main.duration = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMax * 0.35f, speedMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            return ps;
        }

        /// <summary>Confetti: a burst with random bright colours.</summary>
        public static ParticleSystem Confetti(Transform parent, Vector3 localPos, int count = 80)
        {
            var ps = Burst(parent, "Confetti", localPos, count, 12f, 0.5f);
            var main = ps.main;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.33f),
                              new GradientColorKey(new Color(0.3f, 0.7f, 1f), 0.66f), new GradientColorKey(new Color(0.4f, 1f, 0.5f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0.25f;
            return ps;
        }

        /// <summary>Smoke that keeps pouring out (for broken boss parts). Stopped until played.</summary>
        public static ParticleSystem Smoke(Transform parent, Vector3 localPos)
        {
            var ps = StageKit.Emitter(parent, "Smoke", localPos, new Vector3(-90f, 0f, 0f), StageKit.ShimmerMat, false, 1.5f, 3.5f);
            var main = ps.main;
            main.playOnAwake = false;
            main.startColor = new Color(0.15f, 0.15f, 0.17f, 0.6f);
            main.startSpeed = 4f;
            main.startLifetime = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 14f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.6f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        /// <summary>Puts a mission group in the room's Extra Groups (replacing one with the same name).</summary>
        public static void AddExtraGroup(Room room, GameObject group)
        {
            var list = new List<GameObject>();
            if (room.extraGroups != null) foreach (var g in room.extraGroups) if (g != null && g.name != group.name) list.Add(g);
            list.Add(group);
            room.extraGroups = list.ToArray();
            group.SetActive(false);
            EditorUtility.SetDirty(room);
        }

        /// <summary>A fresh child group (any old one with that name is deleted).</summary>
        public static Transform FreshGroup(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return StageKit.Group(parent, name);
        }

        /// <summary>A convoy route: a ConvoySpawner whose children are the waypoints, in world space.</summary>
        public static ConvoySpawner Convoy(Transform parent, string name, ConvoySpawner.Kind kind, PathFlyer prefab, Vector3[] points,
                                           int count, int maxAlive, float interval, float speed, float health, string flyerName, float firstDelay = 3f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = points[0];
            var c = go.AddComponent<ConvoySpawner>();
            for (int i = 0; i < points.Length; i++)
            {
                var wp = new GameObject($"WP{i + 1:00}").transform;
                wp.SetParent(go.transform, false);
                wp.position = points[i];
            }
            var so = new SerializedObject(c);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("flyerPrefab").objectReferenceValue = prefab;
            so.FindProperty("count").intValue = count;
            so.FindProperty("maxAlive").intValue = maxAlive;
            so.FindProperty("interval").floatValue = interval;
            so.FindProperty("firstDelay").floatValue = firstDelay;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("health").floatValue = health;
            so.FindProperty("flyerName").stringValue = flyerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            return c;
        }

        /// <summary>A kinematic rigidbody: tells physics this object's colliders move (doors, gantries, the flying boss).</summary>
        public static void Kinematic(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>A RoomEvent on a new object; fill in its arrays afterwards.</summary>
        public static RoomEvent Event(Transform parent, string name, string title, string hint, Color color,
                                      float firstDelay, float warn, float active, float rest)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<RoomEvent>();
            e.title = title;
            e.hint = hint;
            e.color = color;
            e.firstDelay = firstDelay;
            e.warnTime = warn;
            e.activeTime = active;
            e.restTime = rest;
            return e;
        }

        /// <summary>The two outermost vertices of a mesh (where wingtip trails go).</summary>
        public static void Wingtips(Mesh mesh, out Vector3 left, out Vector3 right)
        {
            left = right = Vector3.zero;
            foreach (var v in mesh.vertices)
            {
                if (v.x < left.x) left = v;
                if (v.x > right.x) right = v;
            }
        }

        /// <summary>Sets every light under 'root' into a list (for blackouts and alarms).</summary>
        public static Light[] LightsUnder(Transform root)
        {
            var list = new List<Light>(root.GetComponentsInChildren<Light>(true));
            return list.ToArray();
        }

        public static void SetArray<T>(Object target, string prop, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
