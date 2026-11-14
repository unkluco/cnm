using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LightSouls.Common;
using LightSouls.Combat;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Weapons;
using LightSouls.Combat.UI;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Testing;

namespace LightSouls.Combat.Editor
{
    /// <summary>
    /// Builder tự động tạo toàn bộ Prefab nhân vật Humanoid,
    /// 3 loại quái vật (2 cận chiến Zombie/Goblin + 1 bắn xa Cultist),
    /// Prefab đạn quái vật, thành phần EnemySpawner, Touch Controls,
    /// thanh máu World-Space, Damage Popup và chụp ảnh nghiệm thu.
    /// </summary>
    public static class CombatPrototypeBuilder
    {
        private const string MaterialsRoot = "Assets/Prefabs/Combat/Materials";
        private const string PrefabsRoot = "Assets/Prefabs/Combat";
        private const string WeaponsRoot = "Assets/ScriptableObjects/Combat/Weapons";
        private const string TestScenePath = "Assets/Scenes/CharacterCombatTest.unity";
        private const string TestNavMeshFolder = "Assets/Scenes/CharacterCombatTest";
        private const string TestNavMeshPath = TestNavMeshFolder + "/NavMesh-Floor_Arena.asset";
        private const string ScreenshotsRoot = "Docs/Reports/Screenshots";

        [MenuItem("LightSouls/Combat/Build Prototype Assets & CharacterCombatTest Scene")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[CombatPrototypeBuilder] Hãy thoát Play Mode trước khi tạo lại assets và scene.");
                return;
            }

            EnsureFolder("Assets/Prefabs/Combat");
            EnsureFolder(MaterialsRoot);
            EnsureFolder(PrefabsRoot);
            EnsureFolder("Assets/ScriptableObjects/Combat");
            EnsureFolder(WeaponsRoot);
            EnsureFolder("Assets/Scenes");
            EnsureFolder(ScreenshotsRoot);

            // 1. Tạo Circle & Icon Sprites cho UI
            Sprite circleRingSprite = GetOrCreateCircleSprite("Sprite_Circle_Ring", true);
            Sprite circleFilledSprite = GetOrCreateCircleSprite("Sprite_Circle_Filled", false);
            Sprite iconAttackSprite = GetOrCreateIconSprite("Icon_Attack");
            Sprite iconSwordSprite = GetOrCreateIconSprite("Icon_Sword");
            Sprite iconGunSprite = GetOrCreateIconSprite("Icon_Gun");
            Sprite iconDashSprite = GetOrCreateIconSprite("Icon_Dash");

            // 2. Tạo Materials
            Material matPlayerSkin = GetOrCreateMaterial("Mat_Player_Skin", new Color(0.95f, 0.78f, 0.65f, 1f));
            Material matPlayerArmor = GetOrCreateMaterial("Mat_Player_Armor", new Color(0.18f, 0.45f, 0.85f, 1f));
            Material matPlayerLimbs = GetOrCreateMaterial("Mat_Player_Limbs", new Color(0.15f, 0.25f, 0.45f, 1f));
            Material matVisor = GetOrCreateMaterial("Mat_Visor", new Color(1f, 0.95f, 0.2f, 1f));
            Material matFloor = GetOrCreateMaterial("Mat_Floor", new Color(0.24f, 0.26f, 0.3f, 1f));
            Material matBullet = GetOrCreateMaterial("Mat_Bullet", new Color(1f, 0.75f, 0.15f, 1f));
            Material matDashGhost = GetOrCreateMaterial("Mat_Dash_Ghost", new Color(0.85f, 0.95f, 1f, 0.6f));

            // Materials Quái vật
            Material matZombieSkin = GetOrCreateMaterial("Mat_Zombie_Skin", new Color(0.35f, 0.48f, 0.32f, 1f));
            Material matZombieArmor = GetOrCreateMaterial("Mat_Zombie_Armor", new Color(0.25f, 0.25f, 0.28f, 1f));
            Material matRedGlow = GetOrCreateMaterial("Mat_Red_Glow", new Color(1f, 0.15f, 0.15f, 1f));

            Material matGoblinSkin = GetOrCreateMaterial("Mat_Goblin_Skin", new Color(0.85f, 0.42f, 0.22f, 1f));
            Material matGoblinLeather = GetOrCreateMaterial("Mat_Goblin_Leather", new Color(0.38f, 0.28f, 0.18f, 1f));
            Material matYellowGlow = GetOrCreateMaterial("Mat_Yellow_Glow", new Color(1f, 0.85f, 0.15f, 1f));

            Material matCultistRobe = GetOrCreateMaterial("Mat_Cultist_Robe", new Color(0.28f, 0.12f, 0.45f, 1f));
            Material matPurpleGlow = GetOrCreateMaterial("Mat_Purple_Glow", new Color(0.85f, 0.35f, 1.0f, 1f));
            Material matWood = GetOrCreateMaterial("Mat_Wood_Dark", new Color(0.2f, 0.15f, 0.1f, 1f));
            Material matSteel = GetOrCreateMaterial("Mat_Steel_Dark", new Color(0.45f, 0.48f, 0.52f, 1f));

            // 3. Tạo Projectile Prefab của Player
            GameObject playerProjPrefab = BuildPlayerProjectilePrefab(matBullet);
            LinkProjectileToWeapons(playerProjPrefab);

            // 4. Tạo Enemy Projectile Prefab (Layer 10: EnemyAttack)
            GameObject enemyProjPrefab = BuildEnemyProjectilePrefab(matPurpleGlow);

            // 5. Tạo Damage Popup Prefab (Canvas World Space)
            GameObject popupPrefab = BuildDamagePopupPrefab();

            // 6. Tạo Player Humanoid Prefab
            GameObject playerPrefab = BuildPlayerPrefab(matPlayerSkin, matPlayerArmor, matPlayerLimbs, matVisor, matDashGhost);

            // 7. Tạo 3 Prefab Quái vật (2 Cận chiến + 1 Bắn xa)
            GameObject zombiePrefab = BuildZombiePrefab(matZombieSkin, matZombieArmor, matRedGlow, matWood, matSteel, popupPrefab);
            GameObject goblinPrefab = BuildGoblinPrefab(matGoblinSkin, matGoblinLeather, matYellowGlow, matSteel, popupPrefab);
            GameObject cultistPrefab = BuildCultistPrefab(matCultistRobe, matPurpleGlow, matWood, enemyProjPrefab, popupPrefab);

            // 8. Tạo Touch Controls Prefab (Analog tròn + Nút tròn Icon + HUD Player)
            GameObject touchControlsPrefab = BuildTouchControlsPrefab(
                circleRingSprite, circleFilledSprite, iconAttackSprite, iconSwordSprite, iconGunSprite, iconDashSprite);

            // 9. Tạo Scene CharacterCombatTest với EnemySpawner và 3 SpawnPoints
            BuildTestScene(playerPrefab, zombiePrefab, goblinPrefab, cultistPrefab, touchControlsPrefab, matFloor, popupPrefab, matDashGhost);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 10. Chụp ảnh màn hình trực tiếp từ Scene
            CaptureGameplayScreenshot();

            Debug.Log("[CombatPrototypeBuilder] Đã hoàn thành khởi tạo toàn bộ 3 loại quái, đạn quái, EnemySpawner và chụp ảnh nghiệm thu!");
        }

        /// <summary>
        /// Lấy InputActionReference (sub-asset) của action trong InputSystem_Actions, ví dụ "Player/Move".
        /// </summary>
        private static UnityEngine.InputSystem.InputActionReference FindActionReference(string actionPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/InputSystem_Actions.inputactions"))
            {
                if (asset is UnityEngine.InputSystem.InputActionReference reference &&
                    reference.action != null &&
                    $"{reference.action.actionMap.name}/{reference.action.name}" == actionPath)
                {
                    return reference;
                }
            }

            Debug.LogError($"[CombatPrototypeBuilder] Không tìm thấy action {actionPath} trong InputSystem_Actions.");
            return null;
        }

        [MenuItem("LightSouls/Combat/Force Reserialize CharacterCombatTest Scene")]
        public static void ForceReserializeTestScene()
        {
            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, TestScenePath);
            AssetDatabase.ForceReserializeAssets(
                new[] { TestScenePath },
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata
            );
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatPrototypeBuilder] Đã Force Reserialize Scene!");
        }

        [MenuItem("LightSouls/Combat/Capture Gameplay Screenshot")]
        public static void CaptureGameplayScreenshot()
        {
            EnsureFolder(ScreenshotsRoot);
            string screenshotPath = $"{ScreenshotsRoot}/CharacterCombatTest_Gameplay.png";

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[CombatPrototypeBuilder] Không tìm thấy Camera để chụp ảnh màn hình.");
                return;
            }

            if (cam.transform.position == Vector3.zero || cam.transform.position.y < 2f)
            {
                cam.transform.position = new Vector3(0f, 9.5f, -6.5f);
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 2f;
                }
            }

            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(screenshotPath, bytes);

            // Sao chép sang artifact directory để hiển thị trong chat
            string artifactDir = "/Users/mac/.gemini/antigravity/brain/532cbccd-90e1-480d-b783-d3d36dfd2f26";
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllBytes(Path.Combine(artifactDir, "CharacterCombatTest_Gameplay.png"), bytes);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CombatPrototypeBuilder] Đã chụp ảnh màn hình minh chứng thành công tại: {screenshotPath}");
        }

        private static Sprite GetOrCreateCircleSprite(string spriteName, bool isRing)
        {
            string path = $"{MaterialsRoot}/{spriteName}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerRadius = size * 0.48f;
            float innerRadius = isRing ? size * 0.38f : 0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = 0f;

                    if (isRing)
                    {
                        if (dist <= outerRadius && dist >= innerRadius)
                        {
                            alpha = 1f;
                        }
                        else if (dist < innerRadius && dist >= innerRadius - 1.5f)
                        {
                            alpha = (dist - (innerRadius - 1.5f)) / 1.5f;
                        }
                        else if (dist > outerRadius && dist <= outerRadius + 1.5f)
                        {
                            alpha = 1f - (dist - outerRadius) / 1.5f;
                        }
                    }
                    else
                    {
                        if (dist <= outerRadius)
                        {
                            alpha = 1f;
                        }
                        else if (dist <= outerRadius + 1.5f)
                        {
                            alpha = 1f - (dist - outerRadius) / 1.5f;
                        }
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetOrCreateIconSprite(string iconName)
        {
            string path = $"{MaterialsRoot}/{iconName}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && existing.texture != null && existing.texture.width >= 128)
            {
                return existing;
            }

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);
            Color white = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            if (iconName.Contains("Sword"))
            {
                DrawThickLine(tex, new Vector2(46, 46), new Vector2(98, 98), 8f, white);
                DrawThickLine(tex, new Vector2(98, 98), new Vector2(104, 104), 4f, white);
                DrawThickLine(tex, new Vector2(38, 54), new Vector2(54, 38), 7f, white);
                DrawThickLine(tex, new Vector2(26, 26), new Vector2(46, 46), 5f, white);
                DrawCircle(tex, new Vector2(24, 24), 7f, white);
            }
            else if (iconName.Contains("Gun"))
            {
                DrawFilledRect(tex, 28, 66, 68, 20, white);
                for (int row = 0; row < 36; row++)
                {
                    int xStart = 38 - (row * 12) / 36;
                    DrawFilledRect(tex, xStart, 30 + row, 20, 1, white);
                }
                DrawThickLine(tex, new Vector2(58, 64), new Vector2(66, 64), 3.5f, white);
                DrawThickLine(tex, new Vector2(66, 64), new Vector2(66, 50), 3.5f, white);
                DrawThickLine(tex, new Vector2(66, 50), new Vector2(54, 50), 3.5f, white);
                DrawThickLine(tex, new Vector2(58, 60), new Vector2(56, 54), 3f, white);
            }
            else if (iconName.Contains("Dash"))
            {
                // Vệt gió lướt nhanh và mũi tên lao về phía trước (Dash Skill)
                DrawThickLine(tex, new Vector2(24, 76), new Vector2(74, 76), 6f, white);
                DrawThickLine(tex, new Vector2(16, 64), new Vector2(92, 64), 7f, white);
                DrawThickLine(tex, new Vector2(28, 52), new Vector2(80, 52), 6f, white);
                DrawThickLine(tex, new Vector2(74, 64), new Vector2(98, 64), 7f, white);
                DrawThickLine(tex, new Vector2(82, 80), new Vector2(98, 64), 6f, white);
                DrawThickLine(tex, new Vector2(82, 48), new Vector2(98, 64), 6f, white);
            }
            else // Attack
            {
                DrawThickLine(tex, new Vector2(24, 24), new Vector2(104, 104), 8f, white);
                DrawThickLine(tex, new Vector2(34, 48), new Vector2(48, 34), 6f, white);
                DrawThickLine(tex, new Vector2(104, 24), new Vector2(24, 104), 8f, white);
                DrawThickLine(tex, new Vector2(94, 48), new Vector2(80, 34), 6f, white);
                DrawCircle(tex, center, 10f, white);
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawThickLine(Texture2D tex, Vector2 p1, Vector2 p2, float thickness, Color col)
        {
            float len = Vector2.Distance(p1, p2);
            int steps = Mathf.CeilToInt(len * 2f);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(p1, p2, (float)i / steps);
                DrawCircle(tex, p, thickness * 0.5f, col);
            }
        }

        private static void DrawCircle(Texture2D tex, Vector2 center, float radius, Color col)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(center.x - radius - 1), 0, tex.width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(center.x + radius + 1), 0, tex.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(center.y - radius - 1), 0, tex.height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(center.y + radius + 1), 0, tex.height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= radius)
                    {
                        tex.SetPixel(x, y, col);
                    }
                    else if (d <= radius + 1.2f)
                    {
                        float alpha = 1f - (d - radius) / 1.2f;
                        Color existing = tex.GetPixel(x, y);
                        tex.SetPixel(x, y, new Color(col.r, col.g, col.b, Mathf.Max(existing.a, alpha)));
                    }
                }
            }
        }

        private static void DrawFilledRect(Texture2D tex, int startX, int startY, int w, int h, Color col)
        {
            for (int y = startY; y < startY + h; y++)
            {
                for (int x = startX; x < startX + w; x++)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }

        private static Material GetOrCreateMaterial(string matName, Color color)
        {
            string path = $"{MaterialsRoot}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader);
                mat.color = color;
                mat.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static GameObject BuildPlayerProjectilePrefab(Material bulletMat)
        {
            string path = $"{PrefabsRoot}/Weapon_Projectile.prefab";
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Weapon_Projectile";
            go.transform.localScale = Vector3.one * 0.3f;
            go.layer = 9; // PlayerAttack = 9

            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = bulletMat;

            go.AddComponent<Projectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildEnemyProjectilePrefab(Material bulletMat)
        {
            string path = $"{PrefabsRoot}/Enemy_Projectile.prefab";
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Enemy_Projectile";
            go.transform.localScale = Vector3.one * 0.35f;
            go.layer = 10; // EnemyAttack = 10

            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = bulletMat;

            go.AddComponent<Projectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void LinkProjectileToWeapons(GameObject projPrefab)
        {
            string[] weaponFiles = { "Weapon_Gun_Pistol", "Weapon_Gun_Shotgun" };
            foreach (var name in weaponFiles)
            {
                string path = $"{WeaponsRoot}/{name}.asset";
                WeaponDataSO so = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
                if (so != null)
                {
                    SerializedObject serObj = new SerializedObject(so);
                    SerializedProperty prop = serObj.FindProperty("projectilePrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = projPrefab;
                        serObj.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(so);
                    }
                }
            }
        }

        private static GameObject BuildDamagePopupPrefab()
        {
            string path = $"{PrefabsRoot}/Combat_DamagePopup.prefab";
            GameObject obj = new GameObject("Combat_DamagePopup");
            Canvas canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 60f);
            obj.transform.localScale = Vector3.one * 0.02f;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text t = textObj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 50;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.75f, 0.15f, 1f);

            DamagePopup popup = obj.AddComponent<DamagePopup>();
            SerializedObject ser = new SerializedObject(popup);
            ser.FindProperty("damageText").objectReferenceValue = t;
            ser.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
            return prefab;
        }

        private static GameObject BuildPlayerPrefab(Material skinMat, Material armorMat, Material limbMat, Material visorMat, Material dashGhostMat)
        {
            string path = $"{PrefabsRoot}/Player_Character.prefab";
            GameObject root = new GameObject("Player_Character");
            root.tag = "Player";
            root.layer = 6; // Player = 6

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            GameObject model = new GameObject("HumanoidFigure");
            model.transform.SetParent(root.transform, false);

            // 1. Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(model.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            head.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Kính bảo hộ / Mắt (Visor)
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor_Eyes";
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
            visor.transform.localScale = new Vector3(0.75f, 0.28f, 0.3f);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.GetComponent<Renderer>().sharedMaterial = visorMat;

            // 2. Torso (Thân áo giáp - sẽ nghiêng về phía trước khi di chuyển)
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso";
            torso.transform.SetParent(model.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso.transform.localScale = new Vector3(0.48f, 0.65f, 0.28f);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = armorMat;

            // 3. Left Arm (Tay trái vung nhịp nhàng theo bước chân)
            GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(model.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.35f, 1.0f, 0f);
            leftArm.transform.localScale = new Vector3(0.16f, 0.5f, 0.16f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = limbMat;

            // 4. Right Arm Pivot (Khớp vai tay phải: đưa lên 1 góc ~35 độ phía trước để giương vũ khí)
            GameObject rightArmPivot = new GameObject("RightArmPivot");
            rightArmPivot.transform.SetParent(model.transform, false);
            rightArmPivot.transform.localPosition = new Vector3(0.35f, 1.25f, 0.05f);
            rightArmPivot.transform.localRotation = Quaternion.Euler(-35f, -8f, 12f);

            GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(rightArmPivot.transform, false);
            rightArm.transform.localPosition = new Vector3(0f, -0.22f, 0.06f);
            rightArm.transform.localScale = new Vector3(0.16f, 0.44f, 0.16f);
            Object.DestroyImmediate(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = limbMat;

            // 5. Left Leg
            GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftLeg.name = "LeftLeg";
            leftLeg.transform.SetParent(model.transform, false);
            leftLeg.transform.localPosition = new Vector3(-0.15f, 0.42f, 0f);
            leftLeg.transform.localScale = new Vector3(0.18f, 0.68f, 0.18f);
            Object.DestroyImmediate(leftLeg.GetComponent<Collider>());
            leftLeg.GetComponent<Renderer>().sharedMaterial = limbMat;

            // 6. Right Leg
            GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightLeg.name = "RightLeg";
            rightLeg.transform.SetParent(model.transform, false);
            rightLeg.transform.localPosition = new Vector3(0.15f, 0.42f, 0f);
            rightLeg.transform.localScale = new Vector3(0.18f, 0.68f, 0.18f);
            Object.DestroyImmediate(rightLeg.GetComponent<Collider>());
            rightLeg.GetComponent<Renderer>().sharedMaterial = limbMat;

            // 7. Weapon Socket (Bàn tay / Điểm gắn cán vũ khí tại cuối cẳng tay)
            GameObject socket = new GameObject("WeaponSocket");
            socket.transform.SetParent(rightArmPivot.transform, false);
            socket.transform.localPosition = new Vector3(0f, -0.42f, 0.12f);
            socket.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

            GameObject swordVis = new GameObject("Visual_Sword");
            swordVis.transform.SetParent(socket.transform, false);
            swordVis.transform.localPosition = Vector3.zero;
            swordVis.transform.localRotation = Quaternion.identity;

            Material matSwordMetal = GetOrCreateMaterial("Mat_Sword_Blade", new Color(0.9f, 0.94f, 1f, 1f));
            Material matSwordGold = GetOrCreateMaterial("Mat_Sword_Gold", new Color(1f, 0.82f, 0.2f, 1f));

            // Cán vũ khí (Hilt) gắn trực tiếp vào vị trí tay cầm của nhân vật
            GameObject hilt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hilt.name = "Hilt";
            hilt.transform.SetParent(swordVis.transform, false);
            hilt.transform.localPosition = Vector3.zero;
            hilt.transform.localScale = new Vector3(0.05f, 0.14f, 0.05f);
            Object.DestroyImmediate(hilt.GetComponent<Collider>());
            hilt.GetComponent<Renderer>().sharedMaterial = limbMat;

            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "Guard";
            guard.transform.SetParent(swordVis.transform, false);
            guard.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            guard.transform.localScale = new Vector3(0.32f, 0.05f, 0.09f);
            Object.DestroyImmediate(guard.GetComponent<Collider>());
            guard.GetComponent<Renderer>().sharedMaterial = matSwordGold;

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(swordVis.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0.60f, 0f);
            blade.transform.localScale = new Vector3(0.12f, 0.92f, 0.03f);
            Object.DestroyImmediate(blade.GetComponent<Collider>());
            blade.GetComponent<Renderer>().sharedMaterial = matSwordMetal;

            // TrailRenderer trên lưỡi kiếm (vệt kéo dài 0.5s dạng sóng mờ dần)
            TrailRenderer swordTrail = blade.AddComponent<TrailRenderer>();
            swordTrail.time = 0.5f;
            swordTrail.startWidth = 0.35f;
            swordTrail.endWidth = 0f;
            swordTrail.minVertexDistance = 0.05f;
            Material trailMat = GetOrCreateMaterial("Mat_Sword_Trail", new Color(1f, 1f, 1f, 0.85f));
            swordTrail.material = trailMat;
            swordTrail.emitting = false;

            MeleeWeapon meleeScript = swordVis.AddComponent<MeleeWeapon>();
            SerializedObject serMelee = new SerializedObject(meleeScript);
            serMelee.FindProperty("slashTrail").objectReferenceValue = swordTrail;
            serMelee.FindProperty("visualBlade").objectReferenceValue = swordVis.transform;
            serMelee.ApplyModifiedPropertiesWithoutUndo();

            // 8. Attack Origin
            GameObject muzzle = new GameObject("AttackOrigin");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.0f, 0.6f);

            // 9. Dash Trail Effect Component
            DashTrailEffect dashEffect = root.AddComponent<DashTrailEffect>();
            dashEffect.Initialize(model.transform, dashGhostMat);

            // Components
            HealthComponent health = root.AddComponent<HealthComponent>();
            PlayerMovement movement = root.AddComponent<PlayerMovement>();
            PlayerCombat combat = root.AddComponent<PlayerCombat>();
            PlayerController controller = root.AddComponent<PlayerController>();

            SerializedObject serMove = new SerializedObject(movement);
            serMove.FindProperty("torso").objectReferenceValue = torso.transform;
            serMove.FindProperty("leftArm").objectReferenceValue = leftArm.transform;
            serMove.FindProperty("rightArm").objectReferenceValue = rightArmPivot.transform;
            serMove.FindProperty("leftLeg").objectReferenceValue = leftLeg.transform;
            serMove.FindProperty("rightLeg").objectReferenceValue = rightLeg.transform;
            serMove.FindProperty("dashTrailEffect").objectReferenceValue = dashEffect;
            serMove.FindProperty("moveAction").objectReferenceValue = FindActionReference("Player/Move");
            serMove.FindProperty("dashAction").objectReferenceValue = FindActionReference("Player/Dash");
            serMove.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serCombat = new SerializedObject(combat);
            serCombat.FindProperty("weaponSocket").objectReferenceValue = socket.transform;
            serCombat.FindProperty("attackOrigin").objectReferenceValue = muzzle.transform;
            serCombat.FindProperty("attackAction").objectReferenceValue = FindActionReference("Player/Attack");
            serCombat.FindProperty("switchWeaponAction").objectReferenceValue = FindActionReference("Player/SwitchWeapon");

            SerializedProperty invProp = serCombat.FindProperty("weaponInventory");
            invProp.ClearArray();

            // Xen kẽ kiếm và súng để mỗi lần bấm Đổi vũ khí đều thấy rõ thay đổi (nút hiển thị loại vũ khí kế tiếp).
            string[] weaponNames = { "Weapon_Sword_Iron", "Weapon_Gun_Pistol", "Weapon_Sword_Greatsword", "Weapon_Gun_Shotgun" };
            for (int i = 0; i < weaponNames.Length; i++)
            {
                WeaponDataSO w = AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"{WeaponsRoot}/{weaponNames[i]}.asset");
                if (w != null)
                {
                    invProp.InsertArrayElementAtIndex(i);
                    invProp.GetArrayElementAtIndex(i).objectReferenceValue = w;
                }
            }
            serCombat.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // -------------------------------------------------------------
        // XÂY DỰNG 3 LOẠI QUÁI VẬT: ZOMBIE, GOBLIN, CULTIST
        // -------------------------------------------------------------

        private static GameObject BuildZombiePrefab(
            Material skinMat, Material armorMat, Material eyeMat, Material woodMat, Material steelMat, GameObject popupPrefab)
        {
            string path = $"{PrefabsRoot}/Enemy_Melee_Zombie.prefab";
            GameObject root = new GameObject("Enemy_Melee_Zombie");
            root.tag = "Enemy";
            root.layer = 7; // Enemy = 7

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 1.85f;
            col.radius = 0.45f;
            col.center = new Vector3(0f, 0.925f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 2.2f;
            agent.stoppingDistance = 1.3f;
            agent.height = 1.85f;
            agent.radius = 0.45f;
            agent.acceleration = 10f;
            agent.angularSpeed = 160f;

            HealthComponent health = root.AddComponent<HealthComponent>();
            SerializedObject serHp = new SerializedObject(health);
            serHp.FindProperty("maxHealth").floatValue = 80f;
            serHp.FindProperty("currentHealth").floatValue = 80f;
            serHp.ApplyModifiedPropertiesWithoutUndo();

            GameObject model = new GameObject("Visual_Zombie");
            model.transform.SetParent(root.transform, false);

            // Đầu Zombie
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(model.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Mắt đỏ phát sáng
            GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyes.name = "Eyes";
            eyes.transform.SetParent(head.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 0.04f, 0.45f);
            eyes.transform.localScale = new Vector3(0.65f, 0.2f, 0.2f);
            Object.DestroyImmediate(eyes.GetComponent<Collider>());
            eyes.GetComponent<Renderer>().sharedMaterial = eyeMat;

            // Thân giáp rỉ sét
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso";
            torso.transform.SetParent(model.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso.transform.localScale = new Vector3(0.58f, 0.68f, 0.34f);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = armorMat;

            // Tay trái
            GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(model.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.4f, 1.0f, 0f);
            leftArm.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Tay phải cầm chùy gai
            GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(model.transform, false);
            rightArm.transform.localPosition = new Vector3(0.4f, 1.0f, 0.08f);
            rightArm.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            rightArm.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            Object.DestroyImmediate(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Chùy gai (Spiked Club)
            GameObject club = new GameObject("Visual_Club");
            club.transform.SetParent(rightArm.transform, false);
            club.transform.localPosition = new Vector3(0f, -0.4f, 0.25f);
            club.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            GameObject clubHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            clubHandle.name = "Handle";
            clubHandle.transform.SetParent(club.transform, false);
            clubHandle.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);
            Object.DestroyImmediate(clubHandle.GetComponent<Collider>());
            clubHandle.GetComponent<Renderer>().sharedMaterial = woodMat;

            GameObject clubHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clubHead.name = "ClubHead";
            clubHead.transform.SetParent(club.transform, false);
            clubHead.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            clubHead.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);
            Object.DestroyImmediate(clubHead.GetComponent<Collider>());
            clubHead.GetComponent<Renderer>().sharedMaterial = steelMat;

            // Chân
            GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftLeg.name = "LeftLeg";
            leftLeg.transform.SetParent(model.transform, false);
            leftLeg.transform.localPosition = new Vector3(-0.18f, 0.42f, 0f);
            leftLeg.transform.localScale = new Vector3(0.2f, 0.68f, 0.2f);
            Object.DestroyImmediate(leftLeg.GetComponent<Collider>());
            leftLeg.GetComponent<Renderer>().sharedMaterial = armorMat;

            GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightLeg.name = "RightLeg";
            rightLeg.transform.SetParent(model.transform, false);
            rightLeg.transform.localPosition = new Vector3(0.18f, 0.42f, 0f);
            rightLeg.transform.localScale = new Vector3(0.2f, 0.68f, 0.2f);
            Object.DestroyImmediate(rightLeg.GetComponent<Collider>());
            rightLeg.GetComponent<Renderer>().sharedMaterial = armorMat;

            // Điểm tấn công AttackPoint
            GameObject atkPoint = new GameObject("AttackPoint");
            atkPoint.transform.SetParent(root.transform, false);
            atkPoint.transform.localPosition = new Vector3(0f, 1.0f, 1.1f);

            // Component MeleeEnemy
            MeleeEnemy enemy = root.AddComponent<MeleeEnemy>();
            SerializedObject serEnemy = new SerializedObject(enemy);
            serEnemy.FindProperty("enemyName").stringValue = "Xác Sống Hầm Ngục";
            serEnemy.FindProperty("moveSpeed").floatValue = 2.2f;
            serEnemy.FindProperty("attackRange").floatValue = 1.6f;
            serEnemy.FindProperty("attackCooldown").floatValue = 1.3f;
            serEnemy.FindProperty("damage").floatValue = 16f;
            serEnemy.FindProperty("hitRadius").floatValue = 1.1f;
            serEnemy.FindProperty("attackPoint").objectReferenceValue = atkPoint.transform;
            serEnemy.FindProperty("visualArm").objectReferenceValue = rightArm.transform;
            serEnemy.FindProperty("torso").objectReferenceValue = torso.transform;
            serEnemy.FindProperty("leftArm").objectReferenceValue = leftArm.transform;
            serEnemy.FindProperty("rightArm").objectReferenceValue = rightArm.transform;
            serEnemy.FindProperty("leftLeg").objectReferenceValue = leftLeg.transform;
            serEnemy.FindProperty("rightLeg").objectReferenceValue = rightLeg.transform;
            serEnemy.FindProperty("forwardLeanAngle").floatValue = 18f;
            serEnemy.FindProperty("walkCycleSpeed").floatValue = 8f;
            serEnemy.ApplyModifiedPropertiesWithoutUndo();

            // Gắn thanh máu World-Space lơ lửng trên đầu
            AttachWorldHealthBar(root, health, popupPrefab, 2.3f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildGoblinPrefab(
            Material skinMat, Material leatherMat, Material eyeMat, Material steelMat, GameObject popupPrefab)
        {
            string path = $"{PrefabsRoot}/Enemy_Melee_Goblin.prefab";
            GameObject root = new GameObject("Enemy_Melee_Goblin");
            root.tag = "Enemy";
            root.layer = 7; // Enemy = 7

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 1.35f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 0.675f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 4.2f;
            agent.stoppingDistance = 1.0f;
            agent.height = 1.35f;
            agent.radius = 0.35f;
            agent.acceleration = 16f;
            agent.angularSpeed = 240f;

            HealthComponent health = root.AddComponent<HealthComponent>();
            SerializedObject serHp = new SerializedObject(health);
            serHp.FindProperty("maxHealth").floatValue = 45f;
            serHp.FindProperty("currentHealth").floatValue = 45f;
            serHp.ApplyModifiedPropertiesWithoutUndo();

            GameObject model = new GameObject("Visual_Goblin");
            model.transform.SetParent(root.transform, false);

            // Đầu Goblin
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(model.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Tai nhọn 2 bên
            GameObject leftEar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftEar.name = "LeftEar";
            leftEar.transform.SetParent(head.transform, false);
            leftEar.transform.localPosition = new Vector3(-0.25f, 0.08f, 0f);
            leftEar.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            leftEar.transform.localScale = new Vector3(0.18f, 0.08f, 0.08f);
            Object.DestroyImmediate(leftEar.GetComponent<Collider>());
            leftEar.GetComponent<Renderer>().sharedMaterial = skinMat;

            GameObject rightEar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightEar.name = "RightEar";
            rightEar.transform.SetParent(head.transform, false);
            rightEar.transform.localPosition = new Vector3(0.25f, 0.08f, 0f);
            rightEar.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            rightEar.transform.localScale = new Vector3(0.18f, 0.08f, 0.08f);
            Object.DestroyImmediate(rightEar.GetComponent<Collider>());
            rightEar.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Mắt vàng
            GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyes.name = "Eyes";
            eyes.transform.SetParent(head.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 0.04f, 0.4f);
            eyes.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);
            Object.DestroyImmediate(eyes.GetComponent<Collider>());
            eyes.GetComponent<Renderer>().sharedMaterial = eyeMat;

            // Thân da nâu
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso";
            torso.transform.SetParent(model.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            torso.transform.localScale = new Vector3(0.38f, 0.48f, 0.24f);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = leatherMat;

            // Tay trái cầm dao găm
            GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(model.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.28f, 0.75f, 0.05f);
            leftArm.transform.localScale = new Vector3(0.13f, 0.42f, 0.13f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Tay phải cầm dao găm
            GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(model.transform, false);
            rightArm.transform.localPosition = new Vector3(0.28f, 0.75f, 0.08f);
            rightArm.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            rightArm.transform.localScale = new Vector3(0.13f, 0.42f, 0.13f);
            Object.DestroyImmediate(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = skinMat;

            // Dao găm
            GameObject dagger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dagger.name = "Dagger";
            dagger.transform.SetParent(rightArm.transform, false);
            dagger.transform.localPosition = new Vector3(0f, -0.3f, 0.2f);
            dagger.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            dagger.transform.localScale = new Vector3(0.06f, 0.35f, 0.02f);
            Object.DestroyImmediate(dagger.GetComponent<Collider>());
            dagger.GetComponent<Renderer>().sharedMaterial = steelMat;

            // Chân
            GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftLeg.name = "LeftLeg";
            leftLeg.transform.SetParent(model.transform, false);
            leftLeg.transform.localPosition = new Vector3(-0.13f, 0.3f, 0f);
            leftLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            Object.DestroyImmediate(leftLeg.GetComponent<Collider>());
            leftLeg.GetComponent<Renderer>().sharedMaterial = leatherMat;

            GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightLeg.name = "RightLeg";
            rightLeg.transform.SetParent(model.transform, false);
            rightLeg.transform.localPosition = new Vector3(0.13f, 0.3f, 0f);
            rightLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            Object.DestroyImmediate(rightLeg.GetComponent<Collider>());
            rightLeg.GetComponent<Renderer>().sharedMaterial = leatherMat;

            GameObject atkPoint = new GameObject("AttackPoint");
            atkPoint.transform.SetParent(root.transform, false);
            atkPoint.transform.localPosition = new Vector3(0f, 0.8f, 0.9f);

            // Component MeleeEnemy
            MeleeEnemy enemy = root.AddComponent<MeleeEnemy>();
            SerializedObject serEnemy = new SerializedObject(enemy);
            serEnemy.FindProperty("enemyName").stringValue = "Yêu Tinh Đột Kích";
            serEnemy.FindProperty("moveSpeed").floatValue = 4.2f;
            serEnemy.FindProperty("attackRange").floatValue = 1.2f;
            serEnemy.FindProperty("attackCooldown").floatValue = 0.7f;
            serEnemy.FindProperty("damage").floatValue = 9f;
            serEnemy.FindProperty("hitRadius").floatValue = 0.9f;
            serEnemy.FindProperty("attackPoint").objectReferenceValue = atkPoint.transform;
            serEnemy.FindProperty("visualArm").objectReferenceValue = rightArm.transform;
            serEnemy.FindProperty("torso").objectReferenceValue = torso.transform;
            serEnemy.FindProperty("leftArm").objectReferenceValue = leftArm.transform;
            serEnemy.FindProperty("rightArm").objectReferenceValue = rightArm.transform;
            serEnemy.FindProperty("leftLeg").objectReferenceValue = leftLeg.transform;
            serEnemy.FindProperty("rightLeg").objectReferenceValue = rightLeg.transform;
            serEnemy.FindProperty("forwardLeanAngle").floatValue = 16f;
            serEnemy.FindProperty("walkCycleSpeed").floatValue = 16f;
            serEnemy.ApplyModifiedPropertiesWithoutUndo();

            AttachWorldHealthBar(root, health, popupPrefab, 1.8f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildCultistPrefab(
            Material robeMat, Material glowMat, Material woodMat, GameObject projectilePrefab, GameObject popupPrefab)
        {
            string path = $"{PrefabsRoot}/Enemy_Ranged_Cultist.prefab";
            GameObject root = new GameObject("Enemy_Ranged_Cultist");
            root.tag = "Enemy";
            root.layer = 7; // Enemy = 7

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.42f;
            col.center = new Vector3(0f, 0.9f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 2.6f;
            agent.stoppingDistance = 6.0f;
            agent.height = 1.8f;
            agent.radius = 0.42f;
            agent.acceleration = 10f;
            agent.angularSpeed = 160f;

            HealthComponent health = root.AddComponent<HealthComponent>();
            SerializedObject serHp = new SerializedObject(health);
            serHp.FindProperty("maxHealth").floatValue = 50f;
            serHp.FindProperty("currentHealth").floatValue = 50f;
            serHp.ApplyModifiedPropertiesWithoutUndo();

            GameObject model = new GameObject("Visual_Cultist");
            model.transform.SetParent(root.transform, false);

            // Nón trùm đầu màu tím
            GameObject hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hood.name = "Hood";
            hood.transform.SetParent(model.transform, false);
            hood.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            hood.transform.localScale = new Vector3(0.42f, 0.45f, 0.42f);
            Object.DestroyImmediate(hood.GetComponent<Collider>());
            hood.GetComponent<Renderer>().sharedMaterial = robeMat;

            // Mắt tím phát sáng
            GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyes.name = "Eyes";
            eyes.transform.SetParent(hood.transform, false);
            eyes.transform.localPosition = new Vector3(0f, -0.05f, 0.48f);
            eyes.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);
            Object.DestroyImmediate(eyes.GetComponent<Collider>());
            eyes.GetComponent<Renderer>().sharedMaterial = glowMat;

            // Áo choàng tím dài
            GameObject robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            robe.name = "Robe";
            robe.transform.SetParent(model.transform, false);
            robe.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            robe.transform.localScale = new Vector3(0.46f, 0.85f, 0.38f);
            Object.DestroyImmediate(robe.GetComponent<Collider>());
            robe.GetComponent<Renderer>().sharedMaterial = robeMat;

            // Tay trái
            GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(model.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.35f, 1.0f, 0f);
            leftArm.transform.localScale = new Vector3(0.14f, 0.5f, 0.14f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = robeMat;

            // Tay phải cầm trượng ma thuật
            GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(model.transform, false);
            rightArm.transform.localPosition = new Vector3(0.35f, 1.0f, 0.1f);
            rightArm.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            rightArm.transform.localScale = new Vector3(0.14f, 0.5f, 0.14f);
            Object.DestroyImmediate(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = robeMat;

            // Trượng ma thuật (Magic Staff)
            GameObject staff = new GameObject("Visual_Staff");
            staff.transform.SetParent(rightArm.transform, false);
            staff.transform.localPosition = new Vector3(0f, -0.2f, 0.35f);
            staff.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            GameObject staffRod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            staffRod.name = "Rod";
            staffRod.transform.SetParent(staff.transform, false);
            staffRod.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);
            Object.DestroyImmediate(staffRod.GetComponent<Collider>());
            staffRod.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Viên pha lê tím đỉnh trượng
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crystal.name = "Crystal";
            crystal.transform.SetParent(staff.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            crystal.transform.localScale = new Vector3(0.22f, 0.28f, 0.22f);
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.GetComponent<Renderer>().sharedMaterial = glowMat;

            // Muzzle Point tại đỉnh trượng
            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(staff.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.15f, 0f);

            // Component RangedEnemy
            RangedEnemy enemy = root.AddComponent<RangedEnemy>();
            SerializedObject serEnemy = new SerializedObject(enemy);
            serEnemy.FindProperty("enemyName").stringValue = "Pháp Sư Hắc Ám";
            serEnemy.FindProperty("moveSpeed").floatValue = 2.6f;
            serEnemy.FindProperty("attackRange").floatValue = 8.5f;
            serEnemy.FindProperty("fleeDistance").floatValue = 3.8f;
            serEnemy.FindProperty("attackCooldown").floatValue = 1.8f;
            serEnemy.FindProperty("damage").floatValue = 12f;
            serEnemy.FindProperty("projectileSpeed").floatValue = 11f;
            serEnemy.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serEnemy.FindProperty("visualStaff").objectReferenceValue = staff.transform;
            serEnemy.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
            serEnemy.FindProperty("torso").objectReferenceValue = robe.transform;
            serEnemy.FindProperty("leftArm").objectReferenceValue = leftArm.transform;
            serEnemy.FindProperty("rightArm").objectReferenceValue = rightArm.transform;
            serEnemy.FindProperty("forwardLeanAngle").floatValue = 10f;
            serEnemy.FindProperty("walkCycleSpeed").floatValue = 9f;
            serEnemy.ApplyModifiedPropertiesWithoutUndo();

            AttachWorldHealthBar(root, health, popupPrefab, 2.3f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AttachWorldHealthBar(GameObject root, HealthComponent health, GameObject popupPrefab, float heightY)
        {
            GameObject canvasObj = new GameObject("WorldHealthBarCanvas");
            canvasObj.transform.SetParent(root.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, heightY, 0f);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(160f, 24f);
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            GameObject sliderObj = new GameObject("HealthSlider");
            sliderObj.transform.SetParent(canvasObj.transform, false);
            RectTransform sliderRt = sliderObj.AddComponent<RectTransform>();
            sliderRt.anchorMin = Vector2.zero;
            sliderRt.anchorMax = Vector2.one;
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.92f, 0.22f, 0.22f, 1f);

            slider.fillRect = fillRt;

            EnemyWorldHealthBar healthBar = canvasObj.AddComponent<EnemyWorldHealthBar>();
            SerializedObject serHb = new SerializedObject(healthBar);
            serHb.FindProperty("healthComponent").objectReferenceValue = health;
            serHb.FindProperty("healthSlider").objectReferenceValue = slider;
            serHb.FindProperty("fillImage").objectReferenceValue = fillImg;
            serHb.FindProperty("popupPrefab").objectReferenceValue = popupPrefab;
            serHb.ApplyModifiedPropertiesWithoutUndo();
        }

        // -------------------------------------------------------------
        // XÂY DỰNG TOUCH CONTROLS PREFAB (ANALOG + BUTTONS + HUD)
        // -------------------------------------------------------------

        private static GameObject BuildTouchControlsPrefab(
            Sprite circleRing, 
            Sprite circleFilled, 
            Sprite iconAttack, 
            Sprite iconSword, 
            Sprite iconGun,
            Sprite iconDash)
        {
            string path = $"{PrefabsRoot}/Combat_TouchControls.prefab";
            GameObject canvasObj = new GameObject("Combat_TouchControls");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject safeArea = new GameObject("SafeArea");
            safeArea.transform.SetParent(canvasObj.transform, false);
            RectTransform safeRect = safeArea.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeArea.AddComponent<SafeAreaFitter>();

            // Cần Analog góc trái dưới
            GameObject stickObj = new GameObject("OnScreenStick");
            stickObj.transform.SetParent(safeArea.transform, false);
            RectTransform stickRect = stickObj.AddComponent<RectTransform>();
            stickRect.anchorMin = new Vector2(0f, 0f);
            stickRect.anchorMax = new Vector2(0f, 0f);
            stickRect.pivot = new Vector2(0.5f, 0.5f);
            stickRect.anchoredPosition = new Vector2(240f, 240f);
            stickRect.sizeDelta = new Vector2(200f, 200f);

            Image stickBg = stickObj.AddComponent<Image>();
            stickBg.sprite = circleRing;
            stickBg.color = new Color(1f, 1f, 1f, 0.45f);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(stickObj.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(80f, 80f);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.sprite = circleFilled;
            handleImg.color = new Color(0.25f, 0.8f, 1f, 0.85f);

            OnScreenStick onScreenStick = stickObj.AddComponent<OnScreenStick>();
            onScreenStick.movementRange = 60f;
            onScreenStick.controlPath = "<Gamepad>/leftStick";

            // Panel nút hành động góc phải dưới (kích thước 460x460 px chứa 3 nút tròn)
            GameObject actionPanel = new GameObject("ActionPanel");
            actionPanel.transform.SetParent(safeArea.transform, false);
            RectTransform actionRect = actionPanel.AddComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(1f, 0f);
            actionRect.anchorMax = new Vector2(1f, 0f);
            actionRect.pivot = new Vector2(1f, 0f);
            actionRect.anchoredPosition = new Vector2(-40f, 40f);
            actionRect.sizeDelta = new Vector2(460f, 460f);

            // 1. Nút Tấn công hình tròn (175x175 px)
            GameObject attackBtn = new GameObject("Button_Attack");
            attackBtn.transform.SetParent(actionPanel.transform, false);
            RectTransform atkRect = attackBtn.AddComponent<RectTransform>();
            atkRect.anchorMin = new Vector2(1f, 0f);
            atkRect.anchorMax = new Vector2(1f, 0f);
            atkRect.pivot = new Vector2(0.5f, 0.5f);
            atkRect.anchoredPosition = new Vector2(-125f, 125f);
            atkRect.sizeDelta = new Vector2(175f, 175f);

            Image atkImg = attackBtn.AddComponent<Image>();
            atkImg.sprite = circleFilled;
            atkImg.color = new Color(0.92f, 0.28f, 0.22f, 0.95f);
            OnScreenButton atkOnScreen = attackBtn.AddComponent<OnScreenButton>();
            atkOnScreen.controlPath = "<Gamepad>/buttonWest";

            GameObject atkIconObj = new GameObject("Icon");
            atkIconObj.transform.SetParent(attackBtn.transform, false);
            RectTransform atkIconRt = atkIconObj.AddComponent<RectTransform>();
            atkIconRt.sizeDelta = new Vector2(75f, 75f);
            atkIconRt.anchoredPosition = new Vector2(0f, 15f);
            Image atkIconImg = atkIconObj.AddComponent<Image>();
            atkIconImg.sprite = iconAttack;
            atkIconImg.color = Color.white;
            atkIconImg.raycastTarget = false;

            GameObject atkLabel = CreateLabel(attackBtn, "ĐÁNH", 20, Color.white);
            RectTransform atkLabelRt = atkLabel.GetComponent<RectTransform>();
            atkLabelRt.anchoredPosition = new Vector2(0f, -42f);
            atkLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // 2. Nút Kỹ năng Lướt (Dash Skill Button) - ĐẶT Ở TRÊN NÚT ATTACK (115x115 px)
            GameObject skillBtn = new GameObject("Button_Skill_Dash");
            skillBtn.transform.SetParent(actionPanel.transform, false);
            RectTransform skillRect = skillBtn.AddComponent<RectTransform>();
            skillRect.anchorMin = new Vector2(1f, 0f);
            skillRect.anchorMax = new Vector2(1f, 0f);
            skillRect.pivot = new Vector2(0.5f, 0.5f);
            skillRect.anchoredPosition = new Vector2(-125f, 305f); // Ngay phía trên Button_Attack
            skillRect.sizeDelta = new Vector2(115f, 115f);

            Image skillImg = skillBtn.AddComponent<Image>();
            skillImg.sprite = circleFilled;
            skillImg.color = new Color(0.12f, 0.72f, 0.95f, 0.95f); // Màu xanh Cyan nổi bật
            OnScreenButton skillOnScreen = skillBtn.AddComponent<OnScreenButton>();
            skillOnScreen.controlPath = "<Gamepad>/rightTrigger";

            GameObject skillIconObj = new GameObject("Icon");
            skillIconObj.transform.SetParent(skillBtn.transform, false);
            RectTransform skillIconRt = skillIconObj.AddComponent<RectTransform>();
            skillIconRt.sizeDelta = new Vector2(56f, 56f);
            skillIconRt.anchoredPosition = new Vector2(0f, 12f);
            Image skillIconImg = skillIconObj.AddComponent<Image>();
            skillIconImg.sprite = iconDash;
            skillIconImg.color = Color.white;
            skillIconImg.raycastTarget = false;

            GameObject skillLabel = CreateLabel(skillBtn, "LƯỚT", 17, Color.white);
            RectTransform skillLabelRt = skillLabel.GetComponent<RectTransform>();
            skillLabelRt.anchoredPosition = new Vector2(0f, -30f);
            skillLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            GameObject cdOverlayObj = new GameObject("CooldownOverlay");
            cdOverlayObj.transform.SetParent(skillBtn.transform, false);
            RectTransform cdRt = cdOverlayObj.AddComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.offsetMin = Vector2.zero;
            cdRt.offsetMax = Vector2.zero;
            Image cdImg = cdOverlayObj.AddComponent<Image>();
            cdImg.sprite = circleFilled;
            cdImg.color = new Color(0f, 0f, 0f, 0.6f);
            cdImg.type = Image.Type.Filled;
            cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.fillOrigin = (int)Image.Origin360.Top;
            cdImg.fillClockwise = false;
            cdImg.fillAmount = 0f;
            cdImg.enabled = false;
            cdImg.raycastTarget = false;

            // 3. Nút Đổi vũ khí hình tròn (115x115 px)
            GameObject switchBtn = new GameObject("Button_SwitchWeapon");
            switchBtn.transform.SetParent(actionPanel.transform, false);
            RectTransform swRect = switchBtn.AddComponent<RectTransform>();
            swRect.anchorMin = new Vector2(1f, 0f);
            swRect.anchorMax = new Vector2(1f, 0f);
            swRect.pivot = new Vector2(0.5f, 0.5f);
            swRect.anchoredPosition = new Vector2(-285f, 195f);
            swRect.sizeDelta = new Vector2(115f, 115f);

            Image swImg = switchBtn.AddComponent<Image>();
            swImg.sprite = circleFilled;
            swImg.color = new Color(0.18f, 0.52f, 0.92f, 0.95f);
            OnScreenButton swOnScreen = switchBtn.AddComponent<OnScreenButton>();
            swOnScreen.controlPath = "<Gamepad>/buttonNorth";

            GameObject swIconObj = new GameObject("Icon");
            swIconObj.transform.SetParent(switchBtn.transform, false);
            RectTransform swIconRt = swIconObj.AddComponent<RectTransform>();
            swIconRt.sizeDelta = new Vector2(52f, 52f);
            swIconRt.anchoredPosition = new Vector2(0f, 12f);
            Image swIconImg = swIconObj.AddComponent<Image>();
            swIconImg.sprite = iconGun;
            swIconImg.color = Color.white;
            swIconImg.raycastTarget = false;

            GameObject swSubLabel = CreateLabel(switchBtn, "SÚNG", 17, Color.white);
            RectTransform swLabelRt = swSubLabel.GetComponent<RectTransform>();
            swLabelRt.anchoredPosition = new Vector2(0f, -30f);
            swSubLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // HUD Player: Tên & Thanh máu ở dưới
            GameObject hudPanel = new GameObject("PlayerHUD_Panel");
            hudPanel.transform.SetParent(safeArea.transform, false);
            RectTransform hudRect = hudPanel.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(35f, -35f);
            hudRect.sizeDelta = new Vector2(380f, 130f);

            Image hudBg = hudPanel.AddComponent<Image>();
            hudBg.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

            GameObject nameTextObj = CreateLabel(hudPanel, "HIỆP SĨ ÁNH SÁNG", 24, new Color(1f, 0.88f, 0.35f));
            RectTransform nameRect = nameTextObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(20f, -12f);
            nameRect.sizeDelta = new Vector2(-40f, 30f);
            nameTextObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            nameTextObj.GetComponent<Text>().fontStyle = FontStyle.Bold;

            GameObject hpSliderObj = new GameObject("PlayerHealthSlider");
            hpSliderObj.transform.SetParent(hudPanel.transform, false);
            RectTransform hpSliderRt = hpSliderObj.AddComponent<RectTransform>();
            hpSliderRt.anchorMin = new Vector2(0f, 1f);
            hpSliderRt.anchorMax = new Vector2(1f, 1f);
            hpSliderRt.pivot = new Vector2(0f, 1f);
            hpSliderRt.anchoredPosition = new Vector2(20f, -48f);
            hpSliderRt.sizeDelta = new Vector2(-40f, 24f);

            Slider hpSlider = hpSliderObj.AddComponent<Slider>();
            hpSlider.interactable = false;
            hpSlider.transition = Selectable.Transition.None;
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.value = 1f;

            GameObject hpBg = new GameObject("Background");
            hpBg.transform.SetParent(hpSliderObj.transform, false);
            RectTransform hpBgRt = hpBg.AddComponent<RectTransform>();
            hpBgRt.anchorMin = Vector2.zero;
            hpBgRt.anchorMax = Vector2.one;
            hpBgRt.offsetMin = Vector2.zero;
            hpBgRt.offsetMax = Vector2.zero;
            Image hpBgImg = hpBg.AddComponent<Image>();
            hpBgImg.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            GameObject hpFillArea = new GameObject("Fill Area");
            hpFillArea.transform.SetParent(hpSliderObj.transform, false);
            RectTransform hpFillAreaRt = hpFillArea.AddComponent<RectTransform>();
            hpFillAreaRt.anchorMin = Vector2.zero;
            hpFillAreaRt.anchorMax = Vector2.one;
            hpFillAreaRt.offsetMin = Vector2.zero;
            hpFillAreaRt.offsetMax = Vector2.zero;

            GameObject hpFill = new GameObject("Fill");
            hpFill.transform.SetParent(hpFillArea.transform, false);
            RectTransform hpFillRt = hpFill.AddComponent<RectTransform>();
            hpFillRt.anchorMin = Vector2.zero;
            hpFillRt.anchorMax = Vector2.one;
            hpFillRt.offsetMin = Vector2.zero;
            hpFillRt.offsetMax = Vector2.zero;
            Image hpFillImg = hpFill.AddComponent<Image>();
            hpFillImg.color = new Color(0.18f, 0.85f, 0.44f, 1f);
            hpSlider.fillRect = hpFillRt;

            GameObject hpTextObj = CreateLabel(hpSliderObj, "100 / 100", 18, Color.white);
            RectTransform hpTextRt = hpTextObj.GetComponent<RectTransform>();
            hpTextRt.anchorMin = Vector2.zero;
            hpTextRt.anchorMax = Vector2.one;
            hpTextRt.sizeDelta = Vector2.zero;
            hpTextObj.GetComponent<Text>().fontStyle = FontStyle.Bold;

            GameObject weaponInfoObj = CreateLabel(hudPanel, "Vũ khí: Kiếm Sắt [CẬN CHIẾN]", 18, new Color(0.85f, 0.9f, 0.95f));
            RectTransform weaponInfoRt = weaponInfoObj.GetComponent<RectTransform>();
            weaponInfoRt.anchorMin = new Vector2(0f, 0f);
            weaponInfoRt.anchorMax = new Vector2(1f, 0f);
            weaponInfoRt.pivot = new Vector2(0f, 0f);
            weaponInfoRt.anchoredPosition = new Vector2(20f, 12f);
            weaponInfoRt.sizeDelta = new Vector2(-40f, 26f);
            weaponInfoObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            CombatHUD combatHUD = canvasObj.AddComponent<CombatHUD>();
            SerializedObject serHUD = new SerializedObject(combatHUD);
            serHUD.FindProperty("playerNameText").objectReferenceValue = nameTextObj.GetComponent<Text>();
            serHUD.FindProperty("healthSlider").objectReferenceValue = hpSlider;
            serHUD.FindProperty("healthFillImage").objectReferenceValue = hpFillImg;
            serHUD.FindProperty("healthText").objectReferenceValue = hpTextObj.GetComponent<Text>();
            serHUD.FindProperty("weaponNameText").objectReferenceValue = weaponInfoObj.GetComponent<Text>();
            serHUD.FindProperty("attackButtonIconImage").objectReferenceValue = atkIconImg;
            serHUD.FindProperty("switchButtonIconImage").objectReferenceValue = swIconImg;
            serHUD.FindProperty("swordIconSprite").objectReferenceValue = iconSword;
            serHUD.FindProperty("gunIconSprite").objectReferenceValue = iconGun;
            serHUD.FindProperty("switchButtonLabelText").objectReferenceValue = swSubLabel.GetComponent<Text>();
            serHUD.FindProperty("attackButtonIconText").objectReferenceValue = atkLabel.GetComponent<Text>();
            serHUD.FindProperty("skillButtonIconImage").objectReferenceValue = skillIconImg;
            serHUD.FindProperty("skillButtonIconText").objectReferenceValue = skillLabel.GetComponent<Text>();
            serHUD.FindProperty("skillButtonLabelText").objectReferenceValue = skillLabel.GetComponent<Text>();
            serHUD.FindProperty("skillCooldownOverlay").objectReferenceValue = cdImg;
            serHUD.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasObj, path);
            Object.DestroyImmediate(canvasObj);
            return prefab;
        }

        private static GameObject CreateLabel(GameObject parent, string text, int fontSize, Color color)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent.transform, false);
            Text t = obj.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return obj;
        }

        // -------------------------------------------------------------
        // XÂY DỰNG SCENE CHARACTERCOMBATTEST VỚI ENEMYSPAWNER
        // -------------------------------------------------------------

        private static void BuildTestScene(
            GameObject playerPrefab, 
            GameObject zombiePrefab,
            GameObject goblinPrefab,
            GameObject cultistPrefab,
            GameObject touchPrefab, 
            Material floorMat,
            GameObject popupPrefab,
            Material matDashGhost)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sàn đấu 40x40m
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Arena";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.layer = 8; // Environment = 8
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Bake NavMesh Surface cho agent Humanoid
            NavMeshSurface navSurface = floor.AddComponent<NavMeshSurface>();
            navSurface.BuildNavMesh();
            // NavMeshData phải lưu thành asset riêng. Nếu để nhúng trong Scene, Unity buộc
            // cả file .unity lưu dạng nhị phân (NavMeshData luôn serialize nhị phân).
            EnsureFolder(TestNavMeshFolder);
            AssetDatabase.DeleteAsset(TestNavMeshPath);
            AssetDatabase.CreateAsset(navSurface.navMeshData, TestNavMeshPath);

            // Ánh sáng Directional Light
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.2f;
            dirLight.color = new Color(1f, 0.98f, 0.94f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera Top-Down
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 9.5f, -6.5f);
            camObj.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camObj.AddComponent<AudioListener>();
            TopDownCameraFollow follow = camObj.AddComponent<TopDownCameraFollow>();

            // Sinh Player tại (0, 0, 0)
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = Vector3.zero;
            follow.SetTarget(player.transform);

            // Sinh mẫu Sword Slash Wave phía trước Player để minh chứng vệt kiếm trắng trong Screenshot
            GameObject slashWaveSample = new GameObject("Sample_SwordSlashWave");
            slashWaveSample.transform.position = player.transform.position + player.transform.forward * 1.15f + Vector3.up * 1.0f;
            slashWaveSample.transform.rotation = player.transform.rotation;
            MeshFilter mfWave = slashWaveSample.AddComponent<MeshFilter>();
            mfWave.sharedMesh = SwordSlashWave.CreateSlashArcMesh(0.8f, 1.8f, 110f, 24);
            MeshRenderer mrWave = slashWaveSample.AddComponent<MeshRenderer>();
            mrWave.sharedMaterial = matDashGhost;
            slashWaveSample.AddComponent<SwordSlashWave>();

            // Tạo các điểm sinh quái (SpawnPoints)
            GameObject spawnGroup = new GameObject("EnemySpawnPoints");
            spawnGroup.transform.position = Vector3.zero;

            Vector3[] spawnPositions = {
                new Vector3(-4.5f, 0f, 6.5f), // Điểm 1: Zombie
                new Vector3(0f, 0f, 7.5f),    // Điểm 2: Goblin
                new Vector3(4.5f, 0f, 6.5f)    // Điểm 3: Cultist
            };

            Transform[] spawnPoints = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject sp = new GameObject($"SpawnPoint_{i + 1}");
                sp.transform.SetParent(spawnGroup.transform, false);
                sp.transform.position = spawnPositions[i];
                spawnPoints[i] = sp.transform;
            }

            // Tạo EnemySpawner Component trong Scene
            GameObject spawnerObj = new GameObject("EnemySpawner");
            EnemySpawner spawner = spawnerObj.AddComponent<EnemySpawner>();
            GameObject[] enemyList = { zombiePrefab, goblinPrefab, cultistPrefab };
            spawner.SetEnemyPrefabs(enemyList);

            SerializedObject serSpawner = new SerializedObject(spawner);
            SerializedProperty testSpProp = serSpawner.FindProperty("testSpawnPoints");
            testSpProp.ClearArray();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                testSpProp.InsertArrayElementAtIndex(i);
                testSpProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }
            serSpawner.FindProperty("autoSpawnOnStart").boolValue = true;
            serSpawner.ApplyModifiedPropertiesWithoutUndo();

            // Sinh sẵn 3 loại quái trong Edit Mode để thấy rõ visual trong Scene & Screenshot
            GameObject zombie = PrefabUtility.InstantiatePrefab(zombiePrefab) as GameObject;
            zombie.name = "Enemy_Melee_Zombie_1";
            zombie.transform.position = spawnPositions[0];
            zombie.transform.rotation = Quaternion.Euler(0f, 160f, 0f);

            GameObject goblin = PrefabUtility.InstantiatePrefab(goblinPrefab) as GameObject;
            goblin.name = "Enemy_Melee_Goblin_1";
            goblin.transform.position = spawnPositions[1];
            goblin.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject cultist = PrefabUtility.InstantiatePrefab(cultistPrefab) as GameObject;
            cultist.name = "Enemy_Ranged_Cultist_1";
            cultist.transform.position = spawnPositions[2];
            cultist.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            // Sinh mẫu Floating Damage Popup bay trên đầu quái Goblin để minh chứng hiệu ứng
            if (popupPrefab != null)
            {
                GameObject samplePopup = PrefabUtility.InstantiatePrefab(popupPrefab) as GameObject;
                samplePopup.name = "Sample_DamagePopup";
                Vector3 screenHb = cam.WorldToScreenPoint(spawnPositions[1] + Vector3.up * 1.8f);
                Vector3 targetScreen = new Vector3(screenHb.x + 35f, screenHb.y + 40f, screenHb.z);
                samplePopup.transform.position = cam.ScreenToWorldPoint(targetScreen);
                samplePopup.transform.rotation = cam.transform.rotation;
                Text t = samplePopup.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.text = "-45";
                    t.fontSize = 52;
                    t.color = new Color(1f, 0.75f, 0.15f, 1f);
                }
            }

            // Sinh Touch Controls
            GameObject touchObj = PrefabUtility.InstantiatePrefab(touchPrefab) as GameObject;
            Canvas touchCanvas = touchObj.GetComponent<Canvas>();
            if (touchCanvas != null)
            {
                touchCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                touchCanvas.worldCamera = cam;
                touchCanvas.planeDistance = 2f;
            }

            // Sinh EventSystem nếu chưa có
            GameObject eventSystemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UI_EventSystem.prefab");
            if (eventSystemPrefab != null)
            {
                PrefabUtility.InstantiatePrefab(eventSystemPrefab);
            }
            else
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            EditorSceneManager.SaveScene(scene, TestScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = path.Replace("\\", "/");

            if (!path.StartsWith("Assets"))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return;
            }

            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && parent != path && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                if (!AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class CombatPrototypeAutoTrigger
    {
        static CombatPrototypeAutoTrigger()
        {
            EditorApplication.delayCall += CheckAndRun;
        }

        private static void CheckAndRun()
        {
            string triggerFile = "Temp/combat_build_trigger.txt";
            if (File.Exists(triggerFile))
            {
                try
                {
                    File.Delete(triggerFile);
                }
                catch { }

                Debug.Log("[CombatPrototypeAutoTrigger] Phát hiện trigger, tự động chạy BuildAll...");
                CombatPrototypeBuilder.BuildAll();
            }
        }
    }
}
