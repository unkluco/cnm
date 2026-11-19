using System;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LightSouls.Combat.Editor
{
    /// <summary>Regenerates the ten special enemy assets without rebuilding existing combat prefabs.</summary>
    public static class SpecialEnemySetBuilder
    {
        private const string Root = "Assets/Prefabs/Combat/SpecialEnemies";
        private const string Materials = Root + "/Materials";
        private const string Template = "Assets/Prefabs/Combat/Enemy_Melee_Zombie.prefab";
        private const string Projectile = "Assets/Prefabs/Combat/Enemy_Projectile.prefab";

        private sealed class Spec
        {
            public readonly SpecialEnemyKind Kind;
            public readonly string Name;
            public readonly Color Color;
            public readonly float Health, Speed, Range, Cooldown, Damage, Windup, Radius;

            public Spec(SpecialEnemyKind kind, string name, Color color, float health, float speed,
                float range, float cooldown, float damage, float windup, float radius)
            {
                Kind = kind; Name = name; Color = color; Health = health; Speed = speed;
                Range = range; Cooldown = cooldown; Damage = damage; Windup = windup; Radius = radius;
            }
        }

        private static readonly Spec[] Specs =
        {
            new Spec(SpecialEnemyKind.Charger, "Kỵ Sĩ Xung Kích", new Color(0.93f,0.22f,0.19f), 78, 3.3f, 5.8f, 2.5f, 20, 0.75f, 1.1f),
            new Spec(SpecialEnemyKind.StoneGolem, "Thạch Cự Nhân", new Color(0.87f,0.57f,0.24f), 145, 1.8f, 3.0f, 3.1f, 25, 1.05f, 3.5f),
            new Spec(SpecialEnemyKind.EmberMage, "Hỏa Pháp Sư", new Color(1f,0.37f,0.08f), 62, 2.6f, 8.0f, 3.1f, 14, 0.9f, 1.35f),
            new Spec(SpecialEnemyKind.FrostMage, "Băng Linh", new Color(0.20f,0.83f,1f), 72, 2.4f, 8.0f, 3.0f, 12, 0.85f, 2.3f),
            new Spec(SpecialEnemyKind.StormArcher, "Xạ Thủ Bão Tia", new Color(0.95f,0.93f,0.23f), 70, 2.8f, 9f, 2.8f, 17, 0.8f, 2f),
            new Spec(SpecialEnemyKind.Phantom, "Bóng Ma Dịch Chuyển", new Color(0.73f,0.35f,1f), 68, 3.4f, 6.5f, 3.1f, 19, 0.8f, 1.75f),
            new Spec(SpecialEnemyKind.BloodLeech, "Huyết Quỷ", new Color(0.85f,0.08f,0.40f), 95, 3.2f, 2.1f, 1.8f, 16, 0.55f, 2.3f),
            new Spec(SpecialEnemyKind.ShieldGuard, "Hộ Vệ Khiên", new Color(0.26f,0.52f,1f), 112, 2.0f, 2.1f, 2.1f, 20, 0.85f, 2.5f),
            new Spec(SpecialEnemyKind.LightPriest, "Tu Sĩ Hồi Phục", new Color(0.35f,0.96f,0.64f), 65, 2.4f, 7.5f, 3.6f, 12, 0.9f, 2f),
            new Spec(SpecialEnemyKind.BombWisp, "Lõi Nổ", new Color(1f,0.65f,0.12f), 48, 4.0f, 2.7f, 2.0f, 24, 1f, 3.3f)
        };

        [MenuItem("LightSouls/Combat/Build Special Enemy Set")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building enemy prefabs.");
            EnsureFolder(Root);
            EnsureFolder(Materials);
            MakeMarkerMaterial();
            var prefabs = new List<GameObject>();
            foreach (var spec in Specs) prefabs.Add(BuildPrefab(spec));

            // Both the real dungeon and the isolated combat scene use the same pool.
            foreach (string path in new[] { "Assets/Scenes/Game.unity", "Assets/Scenes/CharacterCombatTest.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var spawner = Object.FindAnyObjectByType<EnemySpawner>();
                if (spawner == null) throw new InvalidOperationException("Missing EnemySpawner: " + path);
                var so = new SerializedObject(spawner);
                var pool = so.FindProperty("specialEnemyPrefabs");
                pool.arraySize = prefabs.Count;
                for (int i = 0; i < prefabs.Count; i++) pool.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpecialEnemySetBuilder] Configured " + prefabs.Count + " unique enemy prefabs.");
        }

        private static void MakeMarkerMaterial()
        {
            const string path = "Assets/Resources/VFX/Mat_CombatMarker.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = Color.white;
            EditorUtility.SetDirty(material);
        }

        private static GameObject BuildPrefab(Spec spec)
        {
            var root = PrefabUtility.LoadPrefabContents(Template);
            try
            {
                root.name = "Enemy_Special_" + spec.Kind;
                var normal = root.GetComponent<MeleeEnemy>();
                if (normal == null) throw new InvalidOperationException("Invalid enemy template.");
                Object.DestroyImmediate(normal);
                var special = root.AddComponent<SpecialEnemy>();
                special.SetLimbReferences(Find(root, "Torso"), Find(root, "LeftArm"), Find(root, "RightArm"),
                    Find(root, "LeftLeg"), Find(root, "RightLeg"));
                var props = new SerializedObject(special);
                props.FindProperty("kind").enumValueIndex = (int)spec.Kind;
                props.FindProperty("enemyName").stringValue = spec.Name;
                props.FindProperty("moveSpeed").floatValue = spec.Speed;
                props.FindProperty("attackRange").floatValue = spec.Range;
                props.FindProperty("attackCooldown").floatValue = spec.Cooldown;
                props.FindProperty("damage").floatValue = spec.Damage;
                props.FindProperty("windup").floatValue = spec.Windup;
                props.FindProperty("abilityRadius").floatValue = spec.Radius;
                props.FindProperty("accent").colorValue = spec.Color;
                props.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Projectile);
                props.ApplyModifiedPropertiesWithoutUndo();

                var health = new SerializedObject(root.GetComponent<HealthComponent>());
                health.FindProperty("maxHealth").floatValue = spec.Health;
                health.FindProperty("currentHealth").floatValue = spec.Health;
                health.ApplyModifiedPropertiesWithoutUndo();

                var body = MaterialFor(spec.Kind + "_Body", Color.Lerp(spec.Color, new Color(0.075f,0.09f,0.13f), 0.6f));
                var accent = MaterialFor(spec.Kind + "_Accent", spec.Color);
                var visual = Find(root, "Visual_Zombie");
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = renderer.name == "Eyes" || renderer.name == "Head" ? accent : body;
                var club = Find(root, "Visual_Club");
                if (club != null) Object.DestroyImmediate(club.gameObject);
                AddSilhouette(root, spec.Kind, body, accent);
                AddNameLabel(root, spec.Name, spec.Color);
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + root.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddSilhouette(GameObject root, SpecialEnemyKind kind, Material body, Material accent)
        {
            Transform head = Find(root, "Head"), torso = Find(root, "Torso"), right = Find(root, "RightArm");
            // Attach decorations to the rig root so the existing animation and hit colliders remain intact.
            Transform rig = Find(root, "Visual_Zombie");
            if (rig == null) rig = root.transform;
            switch (kind)
            {
                case SpecialEnemyKind.Charger:
                    Shape(rig, PrimitiveType.Cube, "Horn_L", new Vector3(-0.35f,1.9f,0.1f), new Vector3(0.14f,0.5f,0.14f), accent, new Vector3(25,0,27));
                    Shape(rig, PrimitiveType.Cube, "Horn_R", new Vector3(0.35f,1.9f,0.1f), new Vector3(0.14f,0.5f,0.14f), accent, new Vector3(25,0,-27));
                    break;
                case SpecialEnemyKind.StoneGolem:
                    Shape(rig, PrimitiveType.Cube, "RockPlate", new Vector3(0,1.15f,0.25f), new Vector3(0.88f,0.73f,0.4f), accent);
                    Shape(rig, PrimitiveType.Cube, "RockShoulder_L", new Vector3(-0.48f,1.5f,0), new Vector3(0.43f,0.37f,0.48f), body);
                    Shape(rig, PrimitiveType.Cube, "RockShoulder_R", new Vector3(0.48f,1.5f,0), new Vector3(0.43f,0.37f,0.48f), body);
                    break;
                case SpecialEnemyKind.EmberMage:
                case SpecialEnemyKind.FrostMage:
                case SpecialEnemyKind.LightPriest:
                    Shape(rig, PrimitiveType.Cylinder, "Staff", new Vector3(0.52f,1.0f,0.24f), new Vector3(0.09f,0.9f,0.09f), body);
                    Shape(rig, kind == SpecialEnemyKind.FrostMage ? PrimitiveType.Cube : PrimitiveType.Sphere,
                        "StaffCrystal", new Vector3(0.52f,1.96f,0.24f), new Vector3(0.31f,0.31f,0.31f), accent, new Vector3(35,45,0));
                    if (kind == SpecialEnemyKind.LightPriest)
                        Shape(rig, PrimitiveType.Cylinder, "Halo", new Vector3(0,2.05f,0), new Vector3(0.64f,0.045f,0.64f), accent);
                    else
                        Shape(rig, PrimitiveType.Cube, "Crest", new Vector3(0,1.93f,0), new Vector3(0.24f,0.42f,0.24f), accent, new Vector3(0,0,45));
                    break;
                case SpecialEnemyKind.StormArcher:
                    Shape(rig, PrimitiveType.Cube, "Bow_Upper", new Vector3(0.55f,1.53f,0.45f), new Vector3(0.08f,0.65f,0.09f), accent, new Vector3(18,0,-18));
                    Shape(rig, PrimitiveType.Cube, "Bow_Lower", new Vector3(0.55f,0.98f,0.45f), new Vector3(0.08f,0.65f,0.09f), accent, new Vector3(-18,0,18));
                    break;
                case SpecialEnemyKind.Phantom:
                    Shape(rig, PrimitiveType.Capsule, "SpectralMantle", new Vector3(0,1.04f,-0.12f), new Vector3(0.8f,0.62f,0.35f), body);
                    Shape(rig, PrimitiveType.Sphere, "SpiritLight", new Vector3(0,1.85f,0), new Vector3(0.27f,0.27f,0.27f), accent);
                    break;
                case SpecialEnemyKind.BloodLeech:
                    Shape(rig, PrimitiveType.Cube, "Fang_L", new Vector3(-0.13f,1.5f,0.36f), new Vector3(0.09f,0.24f,0.09f), accent);
                    Shape(rig, PrimitiveType.Cube, "Fang_R", new Vector3(0.13f,1.5f,0.36f), new Vector3(0.09f,0.24f,0.09f), accent);
                    break;
                case SpecialEnemyKind.ShieldGuard:
                    Shape(rig, PrimitiveType.Cube, "TowerShield", new Vector3(-0.56f,1.12f,0.42f), new Vector3(0.56f,0.99f,0.15f), accent);
                    Shape(rig, PrimitiveType.Cube, "ShieldBoss", new Vector3(-0.56f,1.12f,0.53f), new Vector3(0.22f,0.23f,0.14f), body);
                    break;
                case SpecialEnemyKind.BombWisp:
                    Shape(rig, PrimitiveType.Sphere, "VolatileCore", new Vector3(0,1.0f,0.1f), new Vector3(0.92f,0.92f,0.92f), accent);
                    Shape(rig, PrimitiveType.Cube, "Fuse", new Vector3(0,1.67f,0.05f), new Vector3(0.13f,0.38f,0.13f), accent, new Vector3(0,0,27));
                    break;
            }
        }

        private static void Shape(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Material material, Vector3 rotation = default)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.transform.localRotation = Quaternion.Euler(rotation);
            shape.layer = 7;
            var collider = shape.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddNameLabel(GameObject root, string name, Color color)
        {
            var canvas = Find(root, "WorldHealthBarCanvas");
            if (canvas == null) return;
            var label = new GameObject("SpecialName", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(canvas, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(240f, 26f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = name;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.Lerp(color, Color.white, 0.55f);
            text.raycastTarget = false;
        }

        private static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static Material MaterialFor(string name, Color color)
        {
            string path = Materials + "/Mat_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
