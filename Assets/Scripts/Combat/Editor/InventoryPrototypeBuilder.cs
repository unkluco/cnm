using System;
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
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;
using LightSouls.Combat.UI;
using LightSouls.Combat.Testing;
using Object = UnityEngine.Object;

namespace LightSouls.Combat.Editor
{
    /// <summary>
    /// Builder chuyên biệt cho hệ thống Túi đồ & Thanh nhanh (Inventory & Hotbar):
    /// - Tạo Icon Sprites cho toàn bộ 9 vật phẩm & UI slot.
    /// - Tạo ScriptableObjects cho Weapon (6), Equipment (2), Consumable (1), ItemDatabase, LootTable.
    /// - Cập nhật Player_Character.prefab (thêm PlayerInventory, PlayerInteraction).
    /// - Cập nhật Combat_TouchControls.prefab (thêm Hotbar 5 ô + ..., thêm nút Tương tác Interact).
    /// - Tạo Prefab Combat_Backpack.prefab (Canvas sort order 8, 15 ô balo, 5 ô hotbar, 2 ô trang bị, panel chi tiết).
    /// - Tạo Prefab Item_Pickup.prefab (IInteractable, Trigger layer Interactable).
    /// - Tạo Scene Assets/Scenes/InventoryTest.unity kèm NavMesh lưu riêng thành NavMesh-Floor_Inventory.asset.
    /// - Chụp ảnh nghiệm thu Device Simulator vào Docs/Reports/Screenshots/InventoryTest_Gameplay.png.
    /// </summary>
    public static class InventoryPrototypeBuilder
    {
        private const string SpritesRoot = "Assets/Sprites/Items";
        private const string ItemsRoot = "Assets/ScriptableObjects/Combat/Items";
        private const string WeaponsRoot = "Assets/ScriptableObjects/Combat/Weapons";
        private const string PrefabsRoot = "Assets/Prefabs/Combat";
        private const string SceneFolder = "Assets/Scenes/InventoryTest";
        private const string ScenePath = "Assets/Scenes/InventoryTest.unity";
        private const string NavMeshAssetPath = SceneFolder + "/NavMesh-Floor_Inventory.asset";
        private const string ScreenshotsRoot = "Docs/Reports/Screenshots";

        [MenuItem("LightSouls/Combat/Build Inventory & Test Scene")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[InventoryPrototypeBuilder] Thoát Play Mode trước khi build.");
                return;
            }

            Debug.Log("[InventoryPrototypeBuilder] Bắt đầu xây dựng hệ thống Inventory & Hotbar...");

            EnsureFolder("Assets/Sprites");
            EnsureFolder(SpritesRoot);
            EnsureFolder("Assets/ScriptableObjects/Combat");
            EnsureFolder(ItemsRoot);
            EnsureFolder(WeaponsRoot);
            EnsureFolder(PrefabsRoot);
            EnsureFolder(SceneFolder);
            EnsureFolder(ScreenshotsRoot);

            // 1. Tạo các Icon Sprites
            var icons = BuildAllItemSprites();

            // 2. Tạo/Cập nhật các ScriptableObjects
            var itemAssets = BuildAllItemScriptableObjects(icons);

            // 3. Tạo Item_Pickup Prefab
            GameObject pickupPrefab = BuildItemPickupPrefab();

            // 4. Tạo Combat_Backpack Prefab (Canvas Sort Order 8)
            GameObject backpackPrefab = BuildBackpackPrefab(icons);

            // 5. Cập nhật Player_Character Prefab
            GameObject playerPrefab = UpdatePlayerPrefab(itemAssets.database, itemAssets.ironSword, itemAssets.potion);

            // 6. Cập nhật Combat_TouchControls Prefab (Hotbar + Interact Button)
            GameObject touchControlsPrefab = UpdateTouchControlsPrefab(icons, backpackPrefab);

            // 7. Tạo Scene InventoryTest.unity kèm NavMesh
            BuildInventoryTestScene(playerPrefab, touchControlsPrefab, backpackPrefab, pickupPrefab, itemAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 8. Chụp ảnh màn hình trực tiếp từ Scene
            CaptureInventoryTestScreenshot();

            Debug.Log("[InventoryPrototypeBuilder] HOÀN TẤT! Toàn bộ hệ thống Inventory & Hotbar đã sẵn sàng nghiệm thu.");
        }

        #region 1. Tạo Icon Sprites

        public class ItemIcons
        {
            public Sprite ironSword;
            public Sprite greatsword;
            public Sprite pistol;
            public Sprite shotgun;
            public Sprite spear;
            public Sprite dagger;
            public Sprite leatherVest;
            public Sprite swiftBoots;
            public Sprite healthPotion;
            public Sprite backpackIcon;
            public Sprite interactIcon;
            public Sprite slotFrame;
            public Sprite slotHighlight;
        }

        private static ItemIcons BuildAllItemSprites()
        {
            var icons = new ItemIcons
            {
                ironSword = CreateOrGetItemSprite("Icon_IronSword", (tex) => DrawSwordIcon(tex, new Color(0.85f, 0.9f, 0.95f), new Color(0.7f, 0.55f, 0.2f))),
                greatsword = CreateOrGetItemSprite("Icon_Greatsword", (tex) => DrawGreatswordIcon(tex, new Color(0.7f, 0.75f, 0.85f), new Color(0.85f, 0.2f, 0.2f))),
                pistol = CreateOrGetItemSprite("Icon_Pistol", (tex) => DrawPistolIcon(tex, new Color(0.3f, 0.32f, 0.35f), new Color(0.55f, 0.35f, 0.2f))),
                shotgun = CreateOrGetItemSprite("Icon_Shotgun", (tex) => DrawShotgunIcon(tex, new Color(0.35f, 0.38f, 0.42f), new Color(0.5f, 0.3f, 0.15f))),
                spear = CreateOrGetItemSprite("Icon_Spear", (tex) => DrawSpearIcon(tex, new Color(0.88f, 0.92f, 0.98f), new Color(0.48f, 0.32f, 0.18f))),
                dagger = CreateOrGetItemSprite("Icon_Dagger", (tex) => DrawDaggerIcon(tex, new Color(0.4f, 0.95f, 0.8f), new Color(0.2f, 0.2f, 0.25f))),
                leatherVest = CreateOrGetItemSprite("Icon_LeatherVest", (tex) => DrawArmorIcon(tex, new Color(0.55f, 0.38f, 0.22f), new Color(0.85f, 0.7f, 0.3f))),
                swiftBoots = CreateOrGetItemSprite("Icon_SwiftBoots", (tex) => DrawBootsIcon(tex, new Color(0.25f, 0.55f, 0.85f), new Color(0.95f, 0.95f, 1f))),
                healthPotion = CreateOrGetItemSprite("Icon_HealthPotion", (tex) => DrawPotionIcon(tex, new Color(0.95f, 0.2f, 0.25f), new Color(0.6f, 0.4f, 0.25f))),
                backpackIcon = CreateOrGetItemSprite("Icon_Backpack", (tex) => DrawBackpackIcon(tex, new Color(0.8f, 0.65f, 0.45f))),
                interactIcon = CreateOrGetItemSprite("Icon_Interact", (tex) => DrawInteractIcon(tex, new Color(1f, 0.9f, 0.2f))),
                slotFrame = CreateOrGetItemSprite("Slot_Frame", (tex) => DrawSlotFrame(tex, new Color(0.12f, 0.15f, 0.22f, 0.85f), new Color(0.35f, 0.42f, 0.55f, 0.9f))),
                slotHighlight = CreateOrGetItemSprite("Slot_Highlight", (tex) => DrawSlotFrame(tex, new Color(1f, 0.85f, 0.2f, 0.15f), new Color(1f, 0.88f, 0.25f, 1f)))
            };

            return icons;
        }

        private static Sprite CreateOrGetItemSprite(string name, Action<Texture2D> drawer)
        {
            string path = $"{SpritesRoot}/{name}.png";
            bool exists = File.Exists(path);

            if (!exists)
            {
                int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] clear = new Color[size * size];
                for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
                tex.SetPixels(clear);

                drawer(tex);
                tex.Apply();

                byte[] bytes = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                File.WriteAllBytes(path, bytes);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawSwordIcon(Texture2D tex, Color blade, Color hilt)
        {
            int s = tex.width;
            for (int i = 24; i < 104; i++)
            {
                int x = i;
                int y = i;
                DrawThickLine(tex, x - 2, y, x + 2, y, blade);
            }
            // Guard
            for (int d = -14; d <= 14; d++)
            {
                int gx = 44 + d;
                int gy = 44 - d;
                if (gx >= 0 && gx < s && gy >= 0 && gy < s) tex.SetPixel(gx, gy, hilt);
            }
            // Pommel
            DrawCircle(tex, 30, 30, 4, hilt);
        }

        private static void DrawGreatswordIcon(Texture2D tex, Color blade, Color gem)
        {
            int s = tex.width;
            for (int i = 22; i < 108; i++)
            {
                int x = i;
                int y = i;
                DrawThickLine(tex, x - 5, y + 2, x + 5, y - 2, blade);
            }
            // Guard
            for (int d = -20; d <= 20; d++)
            {
                int gx = 42 + d;
                int gy = 42 - d;
                DrawCircle(tex, gx, gy, 2, blade * 0.7f);
            }
            DrawCircle(tex, 42, 42, 5, gem);
            DrawCircle(tex, 24, 24, 6, blade * 0.8f);
        }

        private static void DrawPistolIcon(Texture2D tex, Color metal, Color grip)
        {
            // Barrel & slide
            FillRect(tex, 30, 68, 70, 20, metal);
            FillRect(tex, 90, 72, 10, 14, metal * 0.8f);
            // Grip
            FillRect(tex, 34, 30, 22, 40, grip);
            // Trigger guard
            DrawCircleOutline(tex, 58, 56, 10, metal);
        }

        private static void DrawShotgunIcon(Texture2D tex, Color metal, Color stock)
        {
            // Twin barrels
            FillRect(tex, 40, 66, 75, 10, metal);
            FillRect(tex, 40, 56, 75, 8, metal * 0.85f);
            // Stock
            FillRect(tex, 15, 45, 30, 18, stock);
            FillRect(tex, 20, 36, 18, 12, stock * 0.9f);
            // Receiver
            FillRect(tex, 38, 54, 18, 24, metal * 0.6f);
        }

        private static void DrawSpearIcon(Texture2D tex, Color tip, Color shaft)
        {
            int s = tex.width;
            for (int i = 18; i < 95; i++)
            {
                DrawCircle(tex, i, i, 2, shaft);
            }
            // Spear head (triangle tip)
            for (int step = 0; step < 26; step++)
            {
                int cx = 95 + step;
                int cy = 95 + step;
                int r = Mathf.Max(1, 8 - step / 3);
                DrawThickLine(tex, cx - r, cy + r, cx + r, cy - r, tip);
            }
        }

        private static void DrawDaggerIcon(Texture2D tex, Color blade, Color handle)
        {
            for (int i = 28; i < 92; i++)
            {
                int x = i;
                int y = i + (int)(Mathf.Sin(i * 0.08f) * 4f);
                DrawThickLine(tex, x - 3, y + 2, x + 3, y - 2, blade);
            }
            // Hilt
            for (int d = -10; d <= 10; d++)
            {
                tex.SetPixel(42 + d, 42 - d, handle * 1.5f);
            }
            FillRect(tex, 28, 28, 12, 12, handle);
        }

        private static void DrawArmorIcon(Texture2D tex, Color leather, Color trim)
        {
            // Cuirass torso shape
            FillRect(tex, 34, 30, 60, 54, leather);
            // Shoulder pads
            FillRect(tex, 24, 72, 22, 22, leather * 1.1f);
            FillRect(tex, 82, 72, 22, 22, leather * 1.1f);
            // Neck cutout
            FillRect(tex, 52, 74, 24, 16, Color.clear);
            // Trim & Buckles
            DrawThickLine(tex, 34, 32, 94, 32, trim);
            DrawThickLine(tex, 34, 55, 94, 55, trim);
            DrawCircle(tex, 64, 55, 4, trim * 1.2f);
        }

        private static void DrawBootsIcon(Texture2D tex, Color boot, Color wing)
        {
            // Left boot
            FillRect(tex, 28, 48, 24, 42, boot);
            FillRect(tex, 28, 28, 42, 24, boot * 0.9f);
            // Wing accents
            DrawThickLine(tex, 48, 80, 72, 98, wing);
            DrawThickLine(tex, 48, 70, 78, 85, wing);
            DrawThickLine(tex, 48, 60, 72, 70, wing);
            // Sole
            FillRect(tex, 26, 24, 46, 6, wing * 0.7f);
        }

        private static void DrawPotionIcon(Texture2D tex, Color liquid, Color cork)
        {
            // Round glass flask body
            DrawCircle(tex, 64, 50, 32, liquid * 0.5f);
            DrawCircle(tex, 64, 46, 26, liquid);
            // Liquid shine
            DrawCircle(tex, 54, 56, 8, Color.white * 0.85f);
            // Flask neck
            FillRect(tex, 54, 76, 20, 24, new Color(0.8f, 0.9f, 1f, 0.7f));
            // Cork stopper
            FillRect(tex, 52, 96, 24, 14, cork);
        }

        private static void DrawBackpackIcon(Texture2D tex, Color col)
        {
            // Bag body
            FillRect(tex, 32, 28, 64, 60, col);
            // Flap
            FillRect(tex, 30, 68, 68, 24, col * 1.15f);
            // Pocket
            FillRect(tex, 44, 32, 40, 30, col * 0.85f);
            // Straps
            DrawThickLine(tex, 44, 28, 44, 90, col * 0.6f);
            DrawThickLine(tex, 84, 28, 84, 90, col * 0.6f);
        }

        private static void DrawInteractIcon(Texture2D tex, Color col)
        {
            // Diamond / Hand interaction icon
            for (int y = 20; y <= 108; y++)
            {
                int halfW = (int)((1f - Mathf.Abs(y - 64) / 44f) * 36f);
                if (halfW > 0)
                {
                    DrawThickLine(tex, 64 - halfW, y, 64 + halfW, y, col);
                }
            }
            DrawCircle(tex, 64, 64, 14, Color.white);
        }

        private static void DrawSlotFrame(Texture2D tex, Color bg, Color border)
        {
            int s = tex.width;
            FillRect(tex, 4, 4, s - 8, s - 8, bg);
            // Border
            for (int i = 0; i < s; i++)
            {
                for (int b = 0; b < 4; b++)
                {
                    tex.SetPixel(i, b, border);
                    tex.SetPixel(i, s - 1 - b, border);
                    tex.SetPixel(b, i, border);
                    tex.SetPixel(s - 1 - b, i, border);
                }
            }
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color col)
        {
            int sx = tex.width;
            int sy = tex.height;
            for (int j = y; j < y + h; j++)
            {
                if (j < 0 || j >= sy) continue;
                for (int i = x; i < x + w; i++)
                {
                    if (i < 0 || i >= sx) continue;
                    tex.SetPixel(i, j, col);
                }
            }
        }

        private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color col)
        {
            int sx = tex.width;
            int sy = tex.height;
            int rSq = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= sy) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= sx) continue;
                    int dSq = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (dSq <= rSq) tex.SetPixel(x, y, col);
                }
            }
        }

        private static void DrawCircleOutline(Texture2D tex, int cx, int cy, int r, Color col)
        {
            int sx = tex.width;
            int sy = tex.height;
            for (int a = 0; a < 360; a += 2)
            {
                float rad = a * Mathf.Deg2Rad;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                if (x >= 0 && x < sx && y >= 0 && y < sy) tex.SetPixel(x, y, col);
            }
        }

        private static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
        {
            int sx = tex.width;
            int sy = tex.height;
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
            {
                for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
                {
                    if (x >= 0 && x < sx && y >= 0 && y < sy) tex.SetPixel(x, y, col);
                }
            }
        }

        #endregion

        #region 2. Tạo / Cập nhật ScriptableObjects

        public class ItemAssetCollection
        {
            public WeaponDataSO ironSword;
            public WeaponDataSO greatsword;
            public WeaponDataSO pistol;
            public WeaponDataSO shotgun;
            public WeaponDataSO spear;
            public WeaponDataSO dagger;
            public EquipmentDataSO leatherVest;
            public EquipmentDataSO swiftBoots;
            public ConsumableDataSO potion;
            public ItemDatabaseSO database;
            public LootTableSO lootTable;
        }

        private static ItemAssetCollection BuildAllItemScriptableObjects(ItemIcons icons)
        {
            var coll = new ItemAssetCollection();

            GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsRoot}/Player_Projectile.prefab");

            // 1. Weapons
            coll.ironSword = CreateOrUpdateWeapon("Weapon_Sword_Iron", "weapon.iron_sword", "Kiếm Sắt",
                "Thanh kiếm thép cân bằng, tốc độ linh hoạt, sát thương ổn định.",
                WeaponType.Melee, 25f, 0.35f, 2.2f, icons.ironSword, 100, 100f, 1.5f, 3f);

            coll.greatsword = CreateOrUpdateWeapon("Weapon_Sword_Greatsword", "weapon.greatsword", "Đại Kiếm",
                "Thanh đại kiếm uy lực khổng lồ, sát thương khủng khiếp nhưng vung chậm.",
                WeaponType.Melee, 50f, 0.8f, 2.8f, icons.greatsword, 200, 120f, 2.0f, 6f);

            coll.pistol = CreateOrUpdateWeapon("Weapon_Gun_Pistol", "weapon.pistol", "Súng Lục",
                "Khẩu súng lục bắn phát một, tốc độ đạn cao, tầm bắn xa và chính xác.",
                WeaponType.Ranged, 20f, 0.4f, 12f, icons.pistol, 150, 0f, 0f, 0f,
                bulletPrefab, 18f, 3f, 1, 0f);

            coll.shotgun = CreateOrUpdateWeapon("Weapon_Gun_Shotgun", "weapon.shotgun", "Súng Săn",
                "Khẩu súng săn bắn ra nhiều tia đạn cùng lúc, cực mạnh ở cự ly gần.",
                WeaponType.Ranged, 12f, 0.7f, 8f, icons.shotgun, 250, 0f, 0f, 0f,
                bulletPrefab, 16f, 2.5f, 5, 25f);

            coll.spear = CreateOrUpdateWeapon("Weapon_Spear", "weapon.spear", "Thương Sắt",
                "Ngọn thương dài đâm xa vượt trội, tầm với tốt giúp giữ khoảng cách an toàn với quái.",
                WeaponType.Melee, 30f, 0.5f, 3.2f, icons.spear, 180, 50f, 1.2f, 4f);

            coll.dagger = CreateOrUpdateWeapon("Weapon_Dagger", "weapon.dagger", "Dao Găm",
                "Dao găm siêu tốc, tầm ngắn nhưng tốc độ đâm chém chớp nhoáng liên hoàn.",
                WeaponType.Melee, 14f, 0.18f, 1.4f, icons.dagger, 120, 80f, 1.1f, 1.5f);

            // 2. Equipments
            coll.leatherVest = CreateOrUpdateEquipment("Armor_LeatherVest", "armor.leather_vest", "Áo Giáp Da",
                "Áo giáp làm bằng da thú dẻo dai, giúp giảm 20% sát thương nhận vào từ kẻ địch.",
                EquipmentSlotType.Armor, 0.2f, 1.0f, 0f, icons.leatherVest, 150);

            coll.swiftBoots = CreateOrUpdateEquipment("Boots_SwiftBoots", "boots.swift_boots", "Giày Thần Tốc",
                "Đôi giày nhẹ có gắn lông vũ ma thuật, giúp tăng 30% tốc độ di chuyển.",
                EquipmentSlotType.Boots, 0.0f, 1.3f, 0f, icons.swiftBoots, 150);

            // 3. Consumable
            coll.potion = CreateOrUpdateConsumable("Consumable_HealthPotion", "consumable.health_potion", "Bình Máu",
                "Bình thuốc đỏ hồi phục 40 điểm sinh lực ngay lập tức khi sử dụng.",
                40f, 10, icons.healthPotion, 50);

            // 4. Database
            string dbPath = $"{ItemsRoot}/ItemDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            SerializedObject serDb = new SerializedObject(db);
            SerializedProperty itemsProp = serDb.FindProperty("allItems");
            itemsProp.ClearArray();
            ItemDefinitionSO[] allItems = {
                coll.ironSword, coll.greatsword, coll.pistol, coll.shotgun,
                coll.spear, coll.dagger, coll.leatherVest, coll.swiftBoots, coll.potion
            };
            for (int i = 0; i < allItems.Length; i++)
            {
                itemsProp.InsertArrayElementAtIndex(i);
                itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = allItems[i];
            }
            serDb.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            coll.database = db;

            // 5. Loot Table
            string lootPath = $"{ItemsRoot}/LootTable.asset";
            var loot = AssetDatabase.LoadAssetAtPath<LootTableSO>(lootPath);
            if (loot == null)
            {
                loot = ScriptableObject.CreateInstance<LootTableSO>();
                AssetDatabase.CreateAsset(loot, lootPath);
            }
            SerializedObject serLoot = new SerializedObject(loot);
            SerializedProperty dropsProp = serLoot.FindProperty("dropEntries");
            dropsProp.ClearArray();
            (ItemDefinitionSO item, float weight, int min, int max)[] dropDefs = {
                (coll.potion, 35f, 1, 3),
                (coll.ironSword, 15f, 1, 1),
                (coll.spear, 12f, 1, 1),
                (coll.dagger, 12f, 1, 1),
                (coll.pistol, 10f, 1, 1),
                (coll.shotgun, 8f, 1, 1),
                (coll.greatsword, 6f, 1, 1),
                (coll.leatherVest, 10f, 1, 1),
                (coll.swiftBoots, 10f, 1, 1)
            };
            for (int i = 0; i < dropDefs.Length; i++)
            {
                dropsProp.InsertArrayElementAtIndex(i);
                var elem = dropsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Item").objectReferenceValue = dropDefs[i].item;
                elem.FindPropertyRelative("Weight").floatValue = dropDefs[i].weight;
                elem.FindPropertyRelative("MinCount").intValue = dropDefs[i].min;
                elem.FindPropertyRelative("MaxCount").intValue = dropDefs[i].max;
            }
            serLoot.ApplyModifiedProperties();
            EditorUtility.SetDirty(loot);
            coll.lootTable = loot;

            return coll;
        }

        private static WeaponDataSO CreateOrUpdateWeapon(string assetName, string itemId, string displayName,
            string description, WeaponType type, float damage, float attackRate, float attackRange,
            Sprite icon, int price, float swingArc = 90f, float hitRadius = 1.5f, float knockback = 2f,
            GameObject projectilePrefab = null, float projSpeed = 18f, float projLife = 3f, int projCount = 1, float spread = 0f)
        {
            string path = $"{WeaponsRoot}/{assetName}.asset";
            WeaponDataSO data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponDataSO>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject ser = new SerializedObject(data);
            ser.FindProperty("itemId").stringValue = itemId;
            ser.FindProperty("displayName").stringValue = displayName;
            ser.FindProperty("description").stringValue = description;
            ser.FindProperty("icon").objectReferenceValue = icon;
            ser.FindProperty("price").intValue = price;
            ser.FindProperty("maxStack").intValue = 1;

            ser.FindProperty("weaponType").enumValueIndex = (int)type;
            ser.FindProperty("damage").floatValue = damage;
            ser.FindProperty("attackRate").floatValue = attackRate;
            ser.FindProperty("attackRange").floatValue = attackRange;

            ser.FindProperty("swingArcAngle").floatValue = swingArc;
            ser.FindProperty("hitRadius").floatValue = hitRadius;
            ser.FindProperty("knockbackForce").floatValue = knockback;

            if (projectilePrefab != null)
            {
                ser.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            }
            ser.FindProperty("projectileSpeed").floatValue = projSpeed;
            ser.FindProperty("projectileLifetime").floatValue = projLife;
            ser.FindProperty("projectileCount").intValue = projCount;
            ser.FindProperty("spreadAngle").floatValue = spread;

            ser.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static EquipmentDataSO CreateOrUpdateEquipment(string assetName, string itemId, string displayName,
            string description, EquipmentSlotType slotType, float damageReduc, float speedMult, float bonusHp,
            Sprite icon, int price)
        {
            string path = $"{ItemsRoot}/{assetName}.asset";
            EquipmentDataSO data = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EquipmentDataSO>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject ser = new SerializedObject(data);
            ser.FindProperty("itemId").stringValue = itemId;
            ser.FindProperty("displayName").stringValue = displayName;
            ser.FindProperty("description").stringValue = description;
            ser.FindProperty("icon").objectReferenceValue = icon;
            ser.FindProperty("price").intValue = price;
            ser.FindProperty("maxStack").intValue = 1;

            ser.FindProperty("slotType").enumValueIndex = (int)slotType;
            ser.FindProperty("damageReduction").floatValue = damageReduc;
            ser.FindProperty("speedMultiplier").floatValue = speedMult;
            ser.FindProperty("bonusMaxHealth").floatValue = bonusHp;

            ser.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static ConsumableDataSO CreateOrUpdateConsumable(string assetName, string itemId, string displayName,
            string description, float healAmount, int maxStack, Sprite icon, int price)
        {
            string path = $"{ItemsRoot}/{assetName}.asset";
            ConsumableDataSO data = AssetDatabase.LoadAssetAtPath<ConsumableDataSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ConsumableDataSO>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject ser = new SerializedObject(data);
            ser.FindProperty("itemId").stringValue = itemId;
            ser.FindProperty("displayName").stringValue = displayName;
            ser.FindProperty("description").stringValue = description;
            ser.FindProperty("icon").objectReferenceValue = icon;
            ser.FindProperty("price").intValue = price;
            ser.FindProperty("maxStack").intValue = maxStack;

            ser.FindProperty("healAmount").floatValue = healAmount;

            ser.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return data;
        }

        #endregion

        #region 3. Tạo Prefab Item_Pickup

        private static GameObject BuildItemPickupPrefab()
        {
            string path = $"{PrefabsRoot}/Item_Pickup.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject("Item_Pickup");
            root.layer = 12; // Interactable

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.8f;

            ItemPickup pickup = root.AddComponent<ItemPickup>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            visual.transform.localScale = Vector3.one * 0.7f;

            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Simple;

            SerializedObject serPickup = new SerializedObject(pickup);
            serPickup.FindProperty("iconRenderer").objectReferenceValue = sr;
            serPickup.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        #endregion

        #region 4. Tạo Prefab Combat_Backpack

        private static GameObject BuildBackpackPrefab(ItemIcons icons)
        {
            string path = $"{PrefabsRoot}/Combat_Backpack.prefab";

            GameObject root = new GameObject("Combat_Backpack");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8; // Sort Order 8 theo quy ước

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            BackpackUI backpackUI = root.AddComponent<BackpackUI>();

            // Safe Area Container
            GameObject safeArea = new GameObject("SafeAreaContainer");
            safeArea.transform.SetParent(root.transform, false);
            RectTransform safeRt = safeArea.AddComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeArea.AddComponent<SafeAreaFitter>();

            // Modal Dim Backdrop
            GameObject dim = new GameObject("DimOverlay");
            dim.transform.SetParent(safeArea.transform, false);
            RectTransform dimRt = dim.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            Image dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Window Panel (1400 x 820)
            GameObject window = new GameObject("BackpackWindow");
            window.transform.SetParent(safeArea.transform, false);
            RectTransform winRt = window.AddComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(1420, 840);
            winRt.anchoredPosition = Vector2.zero;
            Image winImg = window.AddComponent<Image>();
            winImg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);

            // Window Title
            GameObject title = new GameObject("Text_Title");
            title.transform.SetParent(window.transform, false);
            RectTransform titleRt = title.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.anchoredPosition = new Vector2(40f, -40f);
            titleRt.sizeDelta = new Vector2(-160f, 50f);
            Text titleText = title.AddComponent<Text>();
            titleText.text = "TÚI ĐỒ & TRANG BỊ";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.95f, 0.9f, 0.7f);
            titleText.alignment = TextAnchor.MiddleLeft;

            // Close Button (X)
            GameObject closeBtn = new GameObject("Button_Close");
            closeBtn.transform.SetParent(window.transform, false);
            RectTransform closeRt = closeBtn.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-50f, -40f);
            closeRt.sizeDelta = new Vector2(60f, 60f);
            Image closeImg = closeBtn.AddComponent<Image>();
            closeImg.color = new Color(0.75f, 0.2f, 0.25f);
            Button closeButton = closeBtn.AddComponent<Button>();

            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeBtn.transform, false);
            RectTransform ctRt = closeTextObj.AddComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero;
            ctRt.anchorMax = Vector2.one;
            ctRt.offsetMin = Vector2.zero;
            ctRt.offsetMax = Vector2.zero;
            Text ct = closeTextObj.AddComponent<Text>();
            ct.text = "✕";
            ct.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ct.fontSize = 32;
            ct.color = Color.white;
            ct.alignment = TextAnchor.MiddleCenter;

            // Content Left: Equipment (Armor & Boots)
            GameObject equipGroup = new GameObject("Group_Equipment");
            equipGroup.transform.SetParent(window.transform, false);
            RectTransform egRt = equipGroup.AddComponent<RectTransform>();
            egRt.anchorMin = new Vector2(0f, 0f);
            egRt.anchorMax = new Vector2(0f, 1f);
            egRt.anchoredPosition = new Vector2(100f, -40f);
            egRt.sizeDelta = new Vector2(140f, -120f);

            var armorView = CreateSlotButton(equipGroup.transform, "Slot_Armor", new Vector2(0, 160), new Vector2(110, 110), icons, "GIÁP");
            var bootsView = CreateSlotButton(equipGroup.transform, "Slot_Boots", new Vector2(0, 0), new Vector2(110, 110), icons, "GIÀY");

            // Content Center: Hotbar (5) & Backpack (15)
            GameObject centerGroup = new GameObject("Group_InventorySlots");
            centerGroup.transform.SetParent(window.transform, false);
            RectTransform cgRt = centerGroup.AddComponent<RectTransform>();
            cgRt.anchorMin = new Vector2(0.5f, 0.5f);
            cgRt.anchorMax = new Vector2(0.5f, 0.5f);
            cgRt.anchoredPosition = new Vector2(-50f, -10f);
            cgRt.sizeDelta = new Vector2(620f, 680f);

            // Hotbar label & 5 slots
            GameObject hotbarLabel = new GameObject("Label_Hotbar");
            hotbarLabel.transform.SetParent(centerGroup.transform, false);
            RectTransform hlRt = hotbarLabel.AddComponent<RectTransform>();
            hlRt.anchoredPosition = new Vector2(0, 260);
            hlRt.sizeDelta = new Vector2(600, 30);
            Text hlt = hotbarLabel.AddComponent<Text>();
            hlt.text = "THANH NHANH (5 Ô)";
            hlt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hlt.fontSize = 20;
            hlt.color = new Color(0.7f, 0.85f, 1f);

            List<BackpackSlotView> hotbarViewList = new List<BackpackSlotView>();
            for (int i = 0; i < 5; i++)
            {
                float x = -240f + i * 120f;
                var sv = CreateSlotButton(centerGroup.transform, $"Slot_Hotbar_{i}", new Vector2(x, 190), new Vector2(104, 104), icons, $"[{i + 1}]");
                sv.slotKey = $"hotbar/{i}";
                hotbarViewList.Add(sv);
            }

            // Backpack label & 15 slots (3 rows x 5 cols)
            GameObject backpackLabel = new GameObject("Label_Backpack");
            backpackLabel.transform.SetParent(centerGroup.transform, false);
            RectTransform blRt = backpackLabel.AddComponent<RectTransform>();
            blRt.anchoredPosition = new Vector2(0, 100);
            blRt.sizeDelta = new Vector2(600, 30);
            Text blt = backpackLabel.AddComponent<Text>();
            blt.text = "BALO (15 Ô)";
            blt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            blt.fontSize = 20;
            blt.color = new Color(0.7f, 0.85f, 1f);

            List<BackpackSlotView> backpackViewList = new List<BackpackSlotView>();
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    int idx = row * 5 + col;
                    float x = -240f + col * 120f;
                    float y = 30f - row * 120f;
                    var sv = CreateSlotButton(centerGroup.transform, $"Slot_Backpack_{idx}", new Vector2(x, y), new Vector2(104, 104), icons);
                    sv.slotKey = $"backpack/{idx}";
                    backpackViewList.Add(sv);
                }
            }

            // Content Right: Item Details Panel
            GameObject detailPanel = new GameObject("Panel_Detail");
            detailPanel.transform.SetParent(window.transform, false);
            RectTransform dpRt = detailPanel.AddComponent<RectTransform>();
            dpRt.anchorMin = new Vector2(1f, 0.5f);
            dpRt.anchorMax = new Vector2(1f, 0.5f);
            dpRt.anchoredPosition = new Vector2(-220f, -10f);
            dpRt.sizeDelta = new Vector2(360f, 680f);
            Image dpImg = detailPanel.AddComponent<Image>();
            dpImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

            // Icon Image
            GameObject detailIcon = new GameObject("Image_Icon");
            detailIcon.transform.SetParent(detailPanel.transform, false);
            RectTransform diRt = detailIcon.AddComponent<RectTransform>();
            diRt.anchoredPosition = new Vector2(0, 220);
            diRt.sizeDelta = new Vector2(130, 130);
            Image diImg = detailIcon.AddComponent<Image>();
            diImg.preserveAspect = true;

            // Item Name
            GameObject dName = new GameObject("Text_Name");
            dName.transform.SetParent(detailPanel.transform, false);
            RectTransform dnRt = dName.AddComponent<RectTransform>();
            dnRt.anchoredPosition = new Vector2(0, 120);
            dnRt.sizeDelta = new Vector2(320, 40);
            Text dnText = dName.AddComponent<Text>();
            dnText.text = "Tên vật phẩm";
            dnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dnText.fontSize = 24;
            dnText.fontStyle = FontStyle.Bold;
            dnText.alignment = TextAnchor.MiddleCenter;
            dnText.color = Color.white;

            // Category Text
            GameObject dCat = new GameObject("Text_Category");
            dCat.transform.SetParent(detailPanel.transform, false);
            RectTransform dcRt = dCat.AddComponent<RectTransform>();
            dcRt.anchoredPosition = new Vector2(0, 80);
            dcRt.sizeDelta = new Vector2(320, 30);
            Text dcText = dCat.AddComponent<Text>();
            dcText.text = "[LOẠI]";
            dcText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dcText.fontSize = 18;
            dcText.alignment = TextAnchor.MiddleCenter;
            dcText.color = new Color(0.4f, 0.8f, 1f);

            // Stats Text
            GameObject dStats = new GameObject("Text_Stats");
            dStats.transform.SetParent(detailPanel.transform, false);
            RectTransform dsRt = dStats.AddComponent<RectTransform>();
            dsRt.anchoredPosition = new Vector2(0, 10);
            dsRt.sizeDelta = new Vector2(320, 80);
            Text dsText = dStats.AddComponent<Text>();
            dsText.text = "Chỉ số...";
            dsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dsText.fontSize = 18;
            dsText.alignment = TextAnchor.UpperCenter;
            dsText.color = new Color(0.3f, 0.95f, 0.85f);

            // Description Text
            GameObject dDesc = new GameObject("Text_Description");
            dDesc.transform.SetParent(detailPanel.transform, false);
            RectTransform ddRt = dDesc.AddComponent<RectTransform>();
            ddRt.anchoredPosition = new Vector2(0, -125);
            ddRt.sizeDelta = new Vector2(320, 160);
            Text ddText = dDesc.AddComponent<Text>();
            ddText.text = "Mô tả chi tiết vật phẩm...";
            ddText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ddText.fontSize = 17;
            ddText.alignment = TextAnchor.UpperLeft;
            ddText.color = new Color(0.85f, 0.88f, 0.92f);

            // Button Drop Item (Hình chữ nhật để vứt đồ ra vị trí người chơi đang đứng)
            GameObject dropButtonObj = new GameObject("Button_Drop");
            dropButtonObj.transform.SetParent(detailPanel.transform, false);
            RectTransform dropRt = dropButtonObj.AddComponent<RectTransform>();
            dropRt.anchoredPosition = new Vector2(0, -265);
            dropRt.sizeDelta = new Vector2(280, 56);
            Image dropBg = dropButtonObj.AddComponent<Image>();
            dropBg.sprite = icons.slotFrame;
            dropBg.type = Image.Type.Sliced;
            dropBg.color = new Color(0.85f, 0.25f, 0.25f, 1f);

            Button dropBtn = dropButtonObj.AddComponent<Button>();
            ColorBlock cb = dropBtn.colors;
            cb.normalColor = new Color(0.85f, 0.25f, 0.25f, 1f);
            cb.highlightedColor = new Color(1f, 0.35f, 0.35f, 1f);
            cb.pressedColor = new Color(0.65f, 0.15f, 0.15f, 1f);
            cb.selectedColor = new Color(0.85f, 0.25f, 0.25f, 1f);
            dropBtn.colors = cb;

            GameObject dropTextObj = new GameObject("Text");
            dropTextObj.transform.SetParent(dropButtonObj.transform, false);
            RectTransform dtRt = dropTextObj.AddComponent<RectTransform>();
            dtRt.anchorMin = Vector2.zero;
            dtRt.anchorMax = Vector2.one;
            dtRt.offsetMin = Vector2.zero;
            dtRt.offsetMax = Vector2.zero;
            Text dt = dropTextObj.AddComponent<Text>();
            dt.text = "Vứt vật phẩm";
            dt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dt.fontSize = 22;
            dt.fontStyle = FontStyle.Bold;
            dt.alignment = TextAnchor.MiddleCenter;
            dt.color = Color.white;

            // Wire up SerializedObject for BackpackUI
            SerializedObject serBp = new SerializedObject(backpackUI);
            serBp.FindProperty("closeButton").objectReferenceValue = closeButton;
            serBp.FindProperty("detailPanel").objectReferenceValue = detailPanel;
            serBp.FindProperty("itemNameText").objectReferenceValue = dnText;
            serBp.FindProperty("itemCategoryText").objectReferenceValue = dcText;
            serBp.FindProperty("itemDescriptionText").objectReferenceValue = ddText;
            serBp.FindProperty("itemStatsText").objectReferenceValue = dsText;
            serBp.FindProperty("itemIconImage").objectReferenceValue = diImg;
            serBp.FindProperty("dropItemButton").objectReferenceValue = dropBtn;
            serBp.FindProperty("dropItemButtonText").objectReferenceValue = dt;

            SetSlotViewProperty(serBp.FindProperty("armorSlot"), armorView);
            SetSlotViewProperty(serBp.FindProperty("bootsSlot"), bootsView);

            SerializedProperty hotbarListProp = serBp.FindProperty("hotbarSlots");
            hotbarListProp.ClearArray();
            for (int i = 0; i < hotbarViewList.Count; i++)
            {
                hotbarListProp.InsertArrayElementAtIndex(i);
                SetSlotViewProperty(hotbarListProp.GetArrayElementAtIndex(i), hotbarViewList[i]);
            }

            SerializedProperty backpackListProp = serBp.FindProperty("backpackSlots");
            backpackListProp.ClearArray();
            for (int i = 0; i < backpackViewList.Count; i++)
            {
                backpackListProp.InsertArrayElementAtIndex(i);
                SetSlotViewProperty(backpackListProp.GetArrayElementAtIndex(i), backpackViewList[i]);
            }

            // Kéo thả: DragProxy object
            GameObject dragProxy = new GameObject("DragProxy");
            dragProxy.transform.SetParent(safeArea.transform, false);
            RectTransform dpRtProxy = dragProxy.AddComponent<RectTransform>();
            dpRtProxy.sizeDelta = new Vector2(100f, 100f);
            Image dpImgProxy = dragProxy.AddComponent<Image>();
            dpImgProxy.raycastTarget = false;

            GameObject dpCount = new GameObject("Count");
            dpCount.transform.SetParent(dragProxy.transform, false);
            RectTransform dpcRt = dpCount.AddComponent<RectTransform>();
            dpcRt.anchorMin = new Vector2(1f, 0f);
            dpcRt.anchorMax = new Vector2(1f, 0f);
            dpcRt.anchoredPosition = new Vector2(-10f, 10f);
            dpcRt.sizeDelta = new Vector2(40f, 24f);
            Text dpcText = dpCount.AddComponent<Text>();
            dpcText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dpcText.fontSize = 20;
            dpcText.fontStyle = FontStyle.Bold;
            dpcText.alignment = TextAnchor.LowerRight;
            dpcText.color = Color.white;
            dpcText.raycastTarget = false;

            dragProxy.SetActive(false);

            serBp.FindProperty("dragProxyObject").objectReferenceValue = dragProxy;
            serBp.FindProperty("dragProxyImage").objectReferenceValue = dpImgProxy;
            serBp.FindProperty("dragProxyCountText").objectReferenceValue = dpcText;
            serBp.FindProperty("dragScaleMultiplier").floatValue = 1.3f;

            serBp.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static BackpackSlotView CreateSlotButton(Transform parent, string name, Vector2 pos, Vector2 size, ItemIcons icons, string label = null)
        {
            GameObject slotObj = new GameObject(name);
            slotObj.transform.SetParent(parent, false);
            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image bg = slotObj.AddComponent<Image>();
            bg.sprite = icons.slotFrame;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            Button btn = slotObj.AddComponent<Button>();

            // Item Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform icRt = iconObj.AddComponent<RectTransform>();
            icRt.anchorMin = Vector2.zero;
            icRt.anchorMax = Vector2.one;
            icRt.offsetMin = new Vector2(10, 10);
            icRt.offsetMax = new Vector2(-10, -10);
            Image icImg = iconObj.AddComponent<Image>();
            icImg.preserveAspect = true;
            iconObj.SetActive(false);

            // Count Text
            GameObject countObj = new GameObject("Text_Count");
            countObj.transform.SetParent(slotObj.transform, false);
            RectTransform cntRt = countObj.AddComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(1, 0);
            cntRt.anchorMax = new Vector2(1, 0);
            cntRt.anchoredPosition = new Vector2(-12, 12);
            cntRt.sizeDelta = new Vector2(40, 24);
            Text cntText = countObj.AddComponent<Text>();
            cntText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cntText.fontSize = 18;
            cntText.fontStyle = FontStyle.Bold;
            cntText.alignment = TextAnchor.LowerRight;
            cntText.color = Color.white;
            countObj.SetActive(false);

            // Highlight border
            GameObject hlObj = new GameObject("Highlight");
            hlObj.transform.SetParent(slotObj.transform, false);
            RectTransform hlRt = hlObj.AddComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero;
            hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = new Vector2(-4, -4);
            hlRt.offsetMax = new Vector2(4, 4);
            Image hlImg = hlObj.AddComponent<Image>();
            hlImg.sprite = icons.slotHighlight;
            hlImg.type = Image.Type.Sliced;
            hlImg.color = Color.white;
            hlObj.SetActive(false);

            // Label text if any
            Text labelText = null;
            if (!string.IsNullOrEmpty(label))
            {
                GameObject lblObj = new GameObject("Text_Label");
                lblObj.transform.SetParent(slotObj.transform, false);
                RectTransform lblRt = lblObj.AddComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0.5f, 0f);
                lblRt.anchorMax = new Vector2(0.5f, 0f);
                lblRt.anchoredPosition = new Vector2(0, -16);
                lblRt.sizeDelta = new Vector2(size.x, 20);
                labelText = lblObj.AddComponent<Text>();
                labelText.text = label;
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelText.fontSize = 14;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = new Color(0.7f, 0.75f, 0.85f);
            }

            // Gắn InventorySlotUI để hỗ trợ kéo thả (Drag & Drop) và phóng to theo yêu cầu
            InventorySlotUI slotUI = slotObj.AddComponent<InventorySlotUI>();
            SerializedObject serSlotUI = new SerializedObject(slotUI);
            serSlotUI.FindProperty("button").objectReferenceValue = btn;
            serSlotUI.FindProperty("iconImage").objectReferenceValue = icImg;
            serSlotUI.FindProperty("countText").objectReferenceValue = cntText;
            serSlotUI.FindProperty("highlightObject").objectReferenceValue = hlObj;
            serSlotUI.FindProperty("labelText").objectReferenceValue = labelText;
            serSlotUI.ApplyModifiedProperties();

            return new BackpackSlotView
            {
                button = btn,
                iconImage = icImg,
                countText = cntText,
                highlightObject = hlObj,
                labelText = labelText
            };
        }

        private static void SetSlotViewProperty(SerializedProperty prop, BackpackSlotView view)
        {
            if (prop == null || view == null) return;
            if (prop.FindPropertyRelative("slotKey") != null)
                prop.FindPropertyRelative("slotKey").stringValue = view.slotKey ?? "";
            prop.FindPropertyRelative("button").objectReferenceValue = view.button;
            prop.FindPropertyRelative("iconImage").objectReferenceValue = view.iconImage;
            prop.FindPropertyRelative("countText").objectReferenceValue = view.countText;
            prop.FindPropertyRelative("highlightObject").objectReferenceValue = view.highlightObject;
            if (prop.FindPropertyRelative("labelText") != null)
                prop.FindPropertyRelative("labelText").objectReferenceValue = view.labelText;
        }

        #endregion

        #region 5. Cập nhật Player_Character Prefab

        private static GameObject UpdatePlayerPrefab(ItemDatabaseSO db, WeaponDataSO sword, ConsumableDataSO potion)
        {
            string path = $"{PrefabsRoot}/Player_Character.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            PlayerController controller = root.GetComponent<PlayerController>();
            if (controller == null) controller = root.AddComponent<PlayerController>();

            PlayerInventory inventory = root.GetComponent<PlayerInventory>();
            if (inventory == null) inventory = root.AddComponent<PlayerInventory>();

            PlayerInteraction interaction = root.GetComponent<PlayerInteraction>();
            if (interaction == null) interaction = root.AddComponent<PlayerInteraction>();

            // Setup PlayerInventory
            SerializedObject serInv = new SerializedObject(inventory);
            serInv.FindProperty("itemDatabase").objectReferenceValue = db;

            SerializedProperty defaultItemsProp = serInv.FindProperty("defaultStartingItems");
            defaultItemsProp.ClearArray();

            // Default Starting items: Slot 0 = Iron Sword, Slot 1 = Health Potion (3)
            defaultItemsProp.InsertArrayElementAtIndex(0);
            var item0 = defaultItemsProp.GetArrayElementAtIndex(0);
            item0.FindPropertyRelative("SlotKey").stringValue = "hotbar/0";
            item0.FindPropertyRelative("Item").objectReferenceValue = sword;
            item0.FindPropertyRelative("Count").intValue = 1;

            defaultItemsProp.InsertArrayElementAtIndex(1);
            var item1 = defaultItemsProp.GetArrayElementAtIndex(1);
            item1.FindPropertyRelative("SlotKey").stringValue = "hotbar/1";
            item1.FindPropertyRelative("Item").objectReferenceValue = potion;
            item1.FindPropertyRelative("Count").intValue = 3;

            serInv.ApplyModifiedProperties();

            // Setup PlayerInteraction
            SerializedObject serInteract = new SerializedObject(interaction);
            serInteract.FindProperty("interactRadius").floatValue = 2.0f;
            serInteract.FindProperty("interactAction").objectReferenceValue = FindActionReference("Player/Interact");
            serInteract.ApplyModifiedProperties();

            // Setup PlayerCombat actions
            PlayerCombat combat = root.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                SerializedObject serCombat = new SerializedObject(combat);
                serCombat.FindProperty("attackAction").objectReferenceValue = FindActionReference("Player/Attack");
                serCombat.FindProperty("switchWeaponAction").objectReferenceValue = null;
                serCombat.ApplyModifiedProperties();
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return saved;
        }

        #endregion

        #region 6. Cập nhật Combat_TouchControls Prefab

        private static GameObject UpdateTouchControlsPrefab(ItemIcons icons, GameObject backpackPrefab)
        {
            string path = $"{PrefabsRoot}/Combat_TouchControls.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            Transform safeArea = root.transform.Find("SafeArea");
            if (safeArea == null) safeArea = root.transform;

            Transform actionPanel = safeArea.Find("ActionPanel");
            if (actionPanel == null) actionPanel = safeArea;

            // 1. Xóa hoàn toàn nút SwitchWeapon theo yêu cầu
            Transform oldSwitch = actionPanel.Find("Button_SwitchWeapon") ?? safeArea.Find("Button_SwitchWeapon");
            if (oldSwitch != null)
            {
                Object.DestroyImmediate(oldSwitch.gameObject);
            }

            // 2. Di chuyển nút Lướt (Dash) sang bên trái nút Attack để không đè lên nút Interact hay các UI khác
            Transform dashTr = actionPanel.Find("Button_Skill_Dash") ?? safeArea.Find("Button_Skill_Dash");
            if (dashTr != null)
            {
                RectTransform dashRt = dashTr.GetComponent<RectTransform>();
                dashRt.anchorMin = new Vector2(1f, 0f);
                dashRt.anchorMax = new Vector2(1f, 0f);
                dashRt.anchoredPosition = new Vector2(-285f, 135f); // Bên trái Attack (-135, 135), cách xa Interact (-135, 305)
                dashRt.sizeDelta = new Vector2(120f, 120f);
            }

            // 3. Chuẩn hóa vị trí nút Tấn công (Attack) ở góc phải dưới
            Transform attackTr = actionPanel.Find("Button_Attack") ?? safeArea.Find("Button_Attack");
            if (attackTr != null)
            {
                RectTransform atkRt = attackTr.GetComponent<RectTransform>();
                atkRt.anchorMin = new Vector2(1f, 0f);
                atkRt.anchorMax = new Vector2(1f, 0f);
                atkRt.anchoredPosition = new Vector2(-135f, 135f);
                atkRt.sizeDelta = new Vector2(175f, 175f);
            }

            // 4. Tạo cụm HotbarPanel ở giữa cạnh dưới
            Transform hotbarPanelTr = safeArea.Find("HotbarPanel");
            GameObject hotbarPanel;
            if (hotbarPanelTr != null)
            {
                hotbarPanel = hotbarPanelTr.gameObject;
            }
            else
            {
                hotbarPanel = new GameObject("HotbarPanel");
                hotbarPanel.transform.SetParent(safeArea, false);
            }

            RectTransform hpRt = hotbarPanel.GetComponent<RectTransform>();
            if (hpRt == null) hpRt = hotbarPanel.AddComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0.5f, 0f);
            hpRt.anchorMax = new Vector2(0.5f, 0f);
            hpRt.anchoredPosition = new Vector2(0f, 65f); // Cách mép dưới 65px
            hpRt.sizeDelta = new Vector2(680f, 110f); // Chiều cao 110px >= 96px theo quy ước

            HotbarUI hotbarUI = hotbarPanel.GetComponent<HotbarUI>();
            if (hotbarUI == null) hotbarUI = hotbarPanel.AddComponent<HotbarUI>();

            // Xóa sạch children cũ của hotbarPanel nếu có
            for (int i = hotbarPanel.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(hotbarPanel.transform.GetChild(i).gameObject);
            }

            List<HotbarSlotView> hotbarSlotViews = new List<HotbarSlotView>();
            for (int i = 0; i < 5; i++)
            {
                float x = -275f + i * 110f;
                var view = CreateHotbarSlotButton(hotbarPanel.transform, $"Slot_{i}", new Vector2(x, 0), new Vector2(100, 100), icons);
                hotbarSlotViews.Add(view);
            }

            // Nút mở Balo (...)
            GameObject backpackBtnObj = new GameObject("Button_Backpack");
            backpackBtnObj.transform.SetParent(hotbarPanel.transform, false);
            RectTransform bpRt = backpackBtnObj.AddComponent<RectTransform>();
            bpRt.anchoredPosition = new Vector2(275f, 0f);
            bpRt.sizeDelta = new Vector2(100f, 100f);

            Image bpBg = backpackBtnObj.AddComponent<Image>();
            bpBg.sprite = icons.slotFrame;
            bpBg.type = Image.Type.Sliced;
            Button bpBtn = backpackBtnObj.AddComponent<Button>();

            GameObject bpIconObj = new GameObject("Icon");
            bpIconObj.transform.SetParent(backpackBtnObj.transform, false);
            RectTransform bpicRt = bpIconObj.AddComponent<RectTransform>();
            bpicRt.anchorMin = Vector2.zero;
            bpicRt.anchorMax = Vector2.one;
            bpicRt.offsetMin = new Vector2(16, 16);
            bpicRt.offsetMax = new Vector2(-16, -16);
            Image bpicImg = bpIconObj.AddComponent<Image>();
            bpicImg.sprite = icons.backpackIcon;
            bpicImg.preserveAspect = true;

            // Wire up HotbarUI serialized properties
            SerializedObject serHotbar = new SerializedObject(hotbarUI);
            serHotbar.FindProperty("backpackButton").objectReferenceValue = bpBtn;

            SerializedProperty slotViewsProp = serHotbar.FindProperty("slotViews");
            slotViewsProp.ClearArray();
            for (int i = 0; i < hotbarSlotViews.Count; i++)
            {
                slotViewsProp.InsertArrayElementAtIndex(i);
                var elem = slotViewsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("button").objectReferenceValue = hotbarSlotViews[i].button;
                elem.FindPropertyRelative("iconImage").objectReferenceValue = hotbarSlotViews[i].iconImage;
                elem.FindPropertyRelative("countText").objectReferenceValue = hotbarSlotViews[i].countText;
                elem.FindPropertyRelative("highlightObject").objectReferenceValue = hotbarSlotViews[i].highlightObject;
                elem.FindPropertyRelative("backgroundImage").objectReferenceValue = hotbarSlotViews[i].backgroundImage;
            }
            serHotbar.ApplyModifiedProperties();

            // 5. Tạo/cập nhật nút Tương tác (Button_Interact) đặt ngay TRÊN nút Attack
            Transform interactTr = actionPanel.Find("Button_Interact") ?? safeArea.Find("Button_Interact");
            GameObject interactObj;
            if (interactTr != null)
            {
                interactObj = interactTr.gameObject;
            }
            else
            {
                interactObj = new GameObject("Button_Interact");
                interactObj.transform.SetParent(actionPanel, false);
            }
            interactObj.transform.SetParent(actionPanel, false);

            RectTransform intRt = interactObj.GetComponent<RectTransform>();
            if (intRt == null) intRt = interactObj.AddComponent<RectTransform>();
            intRt.anchorMin = new Vector2(1f, 0f);
            intRt.anchorMax = new Vector2(1f, 0f);
            // Vị trí phía trên nút Attack (-135, 135), kích thước 120x120px
            intRt.anchoredPosition = new Vector2(-135f, 305f);
            intRt.sizeDelta = new Vector2(120f, 120f);

            Image intBg = interactObj.GetComponent<Image>();
            if (intBg == null) intBg = interactObj.AddComponent<Image>();
            intBg.sprite = icons.slotFrame;
            intBg.type = Image.Type.Sliced;
            intBg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

            Button intButton = interactObj.GetComponent<Button>();
            if (intButton == null) intButton = interactObj.AddComponent<Button>();

            OnScreenButton onScreenBtn = interactObj.GetComponent<OnScreenButton>();
            if (onScreenBtn == null) onScreenBtn = interactObj.AddComponent<OnScreenButton>();
            onScreenBtn.controlPath = "<Gamepad>/buttonNorth";

            // Icon động của vật phẩm/vũ khí nhặt
            Transform intIconTr = interactObj.transform.Find("Icon");
            GameObject intIconObj;
            if (intIconTr != null) intIconObj = intIconTr.gameObject;
            else
            {
                intIconObj = new GameObject("Icon");
                intIconObj.transform.SetParent(interactObj.transform, false);
            }
            RectTransform iconRt = intIconObj.GetComponent<RectTransform>();
            if (iconRt == null) iconRt = intIconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 8f);
            iconRt.sizeDelta = new Vector2(72f, 72f);
            Image iconImg = intIconObj.GetComponent<Image>();
            if (iconImg == null) iconImg = intIconObj.AddComponent<Image>();
            iconImg.sprite = icons.interactIcon;
            iconImg.preserveAspect = true;

            // Label prompt text
            Transform intLblTr = interactObj.transform.Find("Text_Prompt");
            GameObject intLbl;
            if (intLblTr != null) intLbl = intLblTr.gameObject;
            else
            {
                intLbl = new GameObject("Text_Prompt");
                intLbl.transform.SetParent(interactObj.transform, false);
            }
            RectTransform lblRt = intLbl.GetComponent<RectTransform>();
            if (lblRt == null) lblRt = intLbl.AddComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.anchoredPosition = new Vector2(0f, 16f);
            lblRt.sizeDelta = new Vector2(0f, 22f);
            Text promptTxt = intLbl.GetComponent<Text>();
            if (promptTxt == null) promptTxt = intLbl.AddComponent<Text>();
            promptTxt.text = "NHẶT";
            promptTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptTxt.fontSize = 14;
            promptTxt.fontStyle = FontStyle.Bold;
            promptTxt.alignment = TextAnchor.MiddleCenter;
            promptTxt.color = Color.white;

            InteractButtonUI interactUI = interactObj.GetComponent<InteractButtonUI>();
            if (interactUI == null) interactUI = interactObj.AddComponent<InteractButtonUI>();

            SerializedObject serIntUI = new SerializedObject(interactUI);
            serIntUI.FindProperty("button").objectReferenceValue = intButton;
            serIntUI.FindProperty("iconImage").objectReferenceValue = iconImg;
            serIntUI.FindProperty("promptText").objectReferenceValue = promptTxt;
            serIntUI.FindProperty("defaultInteractIcon").objectReferenceValue = icons.interactIcon;
            serIntUI.ApplyModifiedProperties();

            // Xóa các tham chiếu nút đổi vũ khí trên CombatHUD
            CombatHUD hud = root.GetComponentInChildren<CombatHUD>();
            if (hud != null)
            {
                SerializedObject serHud = new SerializedObject(hud);
                serHud.FindProperty("switchButtonIconImage").objectReferenceValue = null;
                serHud.FindProperty("switchButtonIconText").objectReferenceValue = null;
                serHud.FindProperty("switchButtonLabelText").objectReferenceValue = null;
                serHud.ApplyModifiedProperties();
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return saved;
        }

        private static HotbarSlotView CreateHotbarSlotButton(Transform parent, string name, Vector2 pos, Vector2 size, ItemIcons icons)
        {
            GameObject slotObj = new GameObject(name);
            slotObj.transform.SetParent(parent, false);
            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image bg = slotObj.AddComponent<Image>();
            bg.sprite = icons.slotFrame;
            bg.type = Image.Type.Sliced;
            Button btn = slotObj.AddComponent<Button>();

            // Item Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform icRt = iconObj.AddComponent<RectTransform>();
            icRt.anchorMin = Vector2.zero;
            icRt.anchorMax = Vector2.one;
            icRt.offsetMin = new Vector2(10, 10);
            icRt.offsetMax = new Vector2(-10, -10);
            Image icImg = iconObj.AddComponent<Image>();
            icImg.preserveAspect = true;
            iconObj.SetActive(false);

            // Count Text
            GameObject countObj = new GameObject("Text_Count");
            countObj.transform.SetParent(slotObj.transform, false);
            RectTransform cntRt = countObj.AddComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(1, 0);
            cntRt.anchorMax = new Vector2(1, 0);
            cntRt.anchoredPosition = new Vector2(-10, 10);
            cntRt.sizeDelta = new Vector2(40, 24);
            Text cntText = countObj.AddComponent<Text>();
            cntText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cntText.fontSize = 18;
            cntText.fontStyle = FontStyle.Bold;
            cntText.alignment = TextAnchor.LowerRight;
            cntText.color = Color.white;
            countObj.SetActive(false);

            // Highlight outline
            GameObject hlObj = new GameObject("Highlight");
            hlObj.transform.SetParent(slotObj.transform, false);
            RectTransform hlRt = hlObj.AddComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero;
            hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = new Vector2(-4, -4);
            hlRt.offsetMax = new Vector2(4, 4);
            Image hlImg = hlObj.AddComponent<Image>();
            hlImg.sprite = icons.slotHighlight;
            hlImg.type = Image.Type.Sliced;
            hlImg.color = Color.white;
            hlObj.SetActive(false);

            return new HotbarSlotView
            {
                button = btn,
                iconImage = icImg,
                countText = cntText,
                highlightObject = hlObj,
                backgroundImage = bg
            };
        }

        #endregion

        #region 7. Tạo Scene InventoryTest.unity kèm NavMesh

        private static void BuildInventoryTestScene(GameObject playerPrefab, GameObject touchControlsPrefab,
            GameObject backpackPrefab, GameObject pickupPrefab, ItemAssetCollection itemAssets)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Floor 40x40m (Layer 8: Environment)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Inventory";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.layer = 8;

            Material matFloor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Combat/Materials/Mat_Floor.mat");
            if (matFloor != null) floor.GetComponent<Renderer>().sharedMaterial = matFloor;

            // Bake NavMesh Surface cho agent Humanoid và lưu asset riêng
            NavMeshSurface navSurface = floor.AddComponent<NavMeshSurface>();
            navSurface.BuildNavMesh();
            EnsureFolder(SceneFolder);
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(navSurface.navMeshData, NavMeshAssetPath);

            // Directional Light
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.25f;
            dirLight.color = new Color(1f, 0.98f, 0.94f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera Top-Down Follow
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55;
            camObj.AddComponent<AudioListener>();
            TopDownCameraFollow camFollow = camObj.AddComponent<TopDownCameraFollow>();

            // Event System
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Instantiate Player
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.name = "Player_Character";
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;

            camFollow.SetTarget(player.transform);

            // Instantiate Touch Controls
            GameObject touchControls = PrefabUtility.InstantiatePrefab(touchControlsPrefab) as GameObject;
            touchControls.name = "Combat_TouchControls";

            // Instantiate Backpack Canvas
            GameObject backpack = PrefabUtility.InstantiatePrefab(backpackPrefab) as GameObject;
            backpack.name = "Combat_Backpack";

            // Hookup references between HUD, Hotbar, Backpack & Player
            var hud = touchControls.GetComponentInChildren<CombatHUD>();
            var pController = player.GetComponent<PlayerController>();
            if (hud != null && pController != null) hud.SetPlayer(pController);

            var hotbarUI = touchControls.GetComponentInChildren<HotbarUI>();
            var backpackUI = backpack.GetComponent<BackpackUI>();
            if (hotbarUI != null)
            {
                SerializedObject serHotbar = new SerializedObject(hotbarUI);
                serHotbar.FindProperty("player").objectReferenceValue = pController;
                serHotbar.FindProperty("inventory").objectReferenceValue = pController.Inventory;
                serHotbar.FindProperty("backpackUI").objectReferenceValue = backpackUI;
                serHotbar.ApplyModifiedProperties();
            }

            if (backpackUI != null)
            {
                SerializedObject serBp = new SerializedObject(backpackUI);
                serBp.FindProperty("player").objectReferenceValue = pController;
                serBp.FindProperty("inventory").objectReferenceValue = pController.Inventory;
                serBp.ApplyModifiedProperties();
            }

            var interactUI = touchControls.GetComponentInChildren<InteractButtonUI>(true);
            if (interactUI != null)
            {
                SerializedObject serInt = new SerializedObject(interactUI);
                serInt.FindProperty("player").objectReferenceValue = pController;
                serInt.FindProperty("interaction").objectReferenceValue = pController.Interaction;
                serInt.ApplyModifiedProperties();
            }

            // Scatter Item Pickups across the room for testing pickup & inventory
            (ItemDefinitionSO item, int count, Vector3 pos)[] groundItems = {
                (itemAssets.ironSword, 1, new Vector3(0.8f, 0.4f, 1.2f)), // Gần Player (<1.5m) để nút Interact hiện vũ khí ngay trên màn hình
                (itemAssets.greatsword, 1, new Vector3(4.5f, 0.4f, 1.5f)),
                (itemAssets.spear, 1, new Vector3(-2.5f, 0.4f, 2.5f)),
                (itemAssets.dagger, 1, new Vector3(-4.5f, 0.4f, 1.5f)),
                (itemAssets.shotgun, 1, new Vector3(0f, 0.4f, 3.5f)),
                (itemAssets.leatherVest, 1, new Vector3(-3.0f, 0.4f, -2.0f)),
                (itemAssets.swiftBoots, 1, new Vector3(3.0f, 0.4f, -2.0f)),
                (itemAssets.potion, 3, new Vector3(0f, 0.4f, -3.5f))
            };

            foreach (var g in groundItems)
            {
                GameObject pObj = PrefabUtility.InstantiatePrefab(pickupPrefab) as GameObject;
                pObj.name = $"Pickup_{g.item.ItemId}";
                pObj.transform.position = g.pos;
                var pickupComp = pObj.GetComponent<ItemPickup>();
                if (pickupComp != null)
                {
                    pickupComp.SetItem(g.item, g.count);
                }
            }

            // Place a Practice Dummy for combat & weapon testing
            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsRoot}/Enemy_PracticeDummy.prefab");
            if (dummyPrefab != null)
            {
                GameObject dummy = PrefabUtility.InstantiatePrefab(dummyPrefab) as GameObject;
                dummy.name = "Enemy_PracticeDummy";
                dummy.transform.position = new Vector3(0f, 0f, 7.5f);
            }

            // Scene Reload Tester (GameManager)
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<SceneReloadTester>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        #endregion

        #region 8. Chụp ảnh nghiệm thu

        [MenuItem("LightSouls/Combat/Capture InventoryTest Screenshot")]
        public static void CaptureInventoryTestScreenshot()
        {
            EnsureFolder(ScreenshotsRoot);
            string screenshotPath = $"{ScreenshotsRoot}/InventoryTest_Gameplay.png";

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Kích hoạt nút tương tác hiển thị icon vũ khí gần đó
            var touchControls = GameObject.Find("Combat_TouchControls");
            if (touchControls != null)
            {
                var interactUI = touchControls.GetComponentInChildren<InteractButtonUI>(true);
                var pickup = Object.FindAnyObjectByType<ItemPickup>();
                if (interactUI != null && pickup != null)
                {
                    interactUI.gameObject.SetActive(true);
                    var mi = typeof(InteractButtonUI).GetMethod("HandleInteractableFound", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (mi != null) mi.Invoke(interactUI, new object[] { pickup });
                }
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 10.5f, -7.5f);
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

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

                Debug.Log($"[InventoryPrototypeBuilder] Đã chụp ảnh nghiệm thu: {screenshotPath}");
            }
        }

        #endregion

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string folder = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

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

            Debug.LogError($"[InventoryPrototypeBuilder] Không tìm thấy action {actionPath} trong InputSystem_Actions.");
            return null;
        }
    }
}
