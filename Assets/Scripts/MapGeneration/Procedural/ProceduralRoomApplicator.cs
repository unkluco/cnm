using System.Collections.Generic;
using LightSouls.MapGeneration.Hazards;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Component gắn trên RoomModule để áp dụng bố cục địa hình và vật cản chiến thuật được sinh theo quy trình:
    /// Quản lý việc sinh/dọn dẹp các container Hazards, Cover, ArenaPillars và cập nhật SpawnPoints cho RoomModule.
    /// </summary>
    [RequireComponent(typeof(RoomModule))]
    [ExecuteAlways]
    public class ProceduralRoomApplicator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TerrainTheme theme = TerrainTheme.Lava;
        [SerializeField] private int customSeed = 101;
        [SerializeField] private bool autoGenerateOnAwake = false;

        [Header("Hazard Prefabs References")]
        [SerializeField] private GameObject prefabLava;
        [SerializeField] private GameObject prefabWater;
        [SerializeField] private GameObject prefabSpikes;
        [SerializeField] private GameObject prefabBarrel;
        [SerializeField] private Material matWall;
        [SerializeField] private Material matEnemy;
        [SerializeField] private Material matLoot;

        [Header("Runtime State")]
        [SerializeField] private string activePatternName;
        [SerializeField] private float activeQualityScore;

        private RoomModule roomModule;
        private RoomTacticalLayout activeLayout;

        public TerrainTheme Theme
        {
            get => theme;
            set => theme = value;
        }

        public int CustomSeed
        {
            get => customSeed;
            set => customSeed = value;
        }

        public RoomTacticalLayout ActiveLayout => activeLayout;
        public string ActivePatternName => activePatternName;
        public float ActiveQualityScore => activeQualityScore;

        public void ConfigurePrefabs(
            GameObject lava,
            GameObject water,
            GameObject spikes,
            GameObject barrel,
            Material wall,
            Material enemy,
            Material loot)
        {
            prefabLava = lava;
            prefabWater = water;
            prefabSpikes = spikes;
            prefabBarrel = barrel;
            matWall = wall;
            matEnemy = enemy;
            matLoot = loot;
        }

        private void Awake()
        {
            roomModule = GetComponent<RoomModule>();
            if (autoGenerateOnAwake && Application.isPlaying)
            {
                GenerateAndApply(customSeed);
            }
        }

        [ContextMenu("Regenerate With Custom Seed")]
        public void RegenerateFromContextMenu()
        {
            GenerateAndApply(customSeed);
        }

        [ContextMenu("Generate With Random Seed")]
        public void GenerateWithRandomSeed()
        {
            customSeed = UnityEngine.Random.Range(100, 999999);
            GenerateAndApply(customSeed);
        }

        /// <summary>
        /// Sinh và áp dụng layout địa hình có chủ đích lên phòng dựa trên seed.
        /// </summary>
        public RoomTacticalLayout GenerateAndApply(int seed)
        {
            if (roomModule == null)
            {
                roomModule = GetComponent<RoomModule>();
            }

            Vector2 size = new Vector2(roomModule.LocalBounds.size.x, roomModule.LocalBounds.size.z);
            var sockets = new List<DoorDirection>();
            foreach (var s in roomModule.DoorSockets)
            {
                if (s != null) sockets.Add(s.LocalDirection);
            }

            activeLayout = ProceduralLayoutGenerator.Generate(seed, theme, size, sockets);
            activePatternName = activeLayout.PatternName;
            activeQualityScore = activeLayout.Score.TotalScore;

            ApplyLayout(activeLayout);
            return activeLayout;
        }

        /// <summary>
        /// Áp dụng trực tiếp một đối tượng RoomTacticalLayout lên cây phân cấp GameObject của phòng.
        /// </summary>
        public void ApplyLayout(RoomTacticalLayout layout)
        {
            if (layout == null) return;
            if (roomModule == null) roomModule = GetComponent<RoomModule>();

            // 1. Dọn dẹp các container cũ nếu có
            ClearContainer("Hazards");
            ClearContainer("Cover");
            ClearContainer("ArenaPillars");
            ClearContainer("ProceduralSpawns");

            Transform hazardsRoot = GetOrCreateContainer("Hazards");
            Transform coverRoot = GetOrCreateContainer("Cover");
            Transform pillarsRoot = GetOrCreateContainer("ArenaPillars");
            Transform spawnsRoot = GetOrCreateContainer("ProceduralSpawns");

            var lifecycle = roomModule.LifecycleController;

            // 2. Sinh Hazards
            foreach (var h in layout.Hazards)
            {
                GameObject prefab = h.HazardType switch
                {
                    ProceduralHazardType.Lava => prefabLava,
                    ProceduralHazardType.Water => prefabWater,
                    ProceduralHazardType.Spikes => prefabSpikes,
                    ProceduralHazardType.ExplosiveBarrel => prefabBarrel,
                    _ => null
                };

                if (prefab != null)
                {
                    GameObject go = InstantiatePrefabOrNormal(prefab, hazardsRoot);
                    go.name = h.Name;
                    go.transform.localPosition = h.LocalPosition;
                    go.transform.localScale = h.LocalScale;
                    go.transform.localRotation = h.LocalRotation;

                    if (h.HazardType == ProceduralHazardType.Spikes)
                    {
                        var spike = go.GetComponent<SpikeTrapHazard>();
                        if (spike != null)
                        {
                            spike.PhaseOffset = h.PhaseOffset;
                            if (lifecycle != null) spike.RoomLifecycle = lifecycle;
                        }
                    }
                }
            }

            // 3. Sinh Cover và Pillars
            foreach (var c in layout.Covers)
            {
                Transform parent = c.IsThinPillar ? pillarsRoot : coverRoot;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = c.Name;
                cube.transform.SetParent(parent, false);
                cube.transform.localPosition = c.LocalPosition;
                cube.transform.localScale = c.LocalScale;
                cube.transform.localRotation = c.LocalRotation;

                if (matWall != null)
                {
                    cube.GetComponent<Renderer>().sharedMaterial = matWall;
                }
                var stoneMesh = Resources.Load<Mesh>("Art/DarkFantasy/Meshes/BeveledBox");
                if (stoneMesh != null) cube.GetComponent<MeshFilter>().sharedMesh = stoneMesh;

                int envLayer = LayerMask.NameToLayer("Environment");
                if (envLayer >= 0) cube.layer = envLayer;
            }

            // 4. Sinh Enemy & Loot Spawn Markers
            var enemyTransforms = new List<Transform>();
            for (int i = 0; i < layout.EnemySpawns.Count; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"EnemySpawn_{i + 1}";
                marker.transform.SetParent(spawnsRoot, false);
                marker.transform.localPosition = layout.EnemySpawns[i] + new Vector3(0f, 0.06f, 0f);
                marker.transform.localScale = new Vector3(1.1f, 0.06f, 1.1f);
                if (marker.GetComponent<Collider>() != null)
                {
                    DestroyImmediate(marker.GetComponent<Collider>());
                }
                if (matEnemy != null)
                {
                    marker.GetComponent<Renderer>().sharedMaterial = matEnemy;
                }
                if (Application.isPlaying) marker.GetComponent<Renderer>().enabled = false;
                enemyTransforms.Add(marker.transform);
            }

            var lootTransforms = new List<Transform>();
            for (int i = 0; i < layout.LootSpawns.Count; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"LootSpawn_{i + 1}";
                marker.transform.SetParent(spawnsRoot, false);
                marker.transform.localPosition = layout.LootSpawns[i];
                marker.transform.localScale = new Vector3(1.4f, 0.9f, 1.0f);
                if (matLoot != null)
                {
                    marker.GetComponent<Renderer>().sharedMaterial = matLoot;
                }
                lootTransforms.Add(marker.transform);
            }

            roomModule.SetSpawnPoints(enemyTransforms, lootTransforms);
        }

        private GameObject InstantiatePrefabOrNormal(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            }
#endif
            return Instantiate(prefab, parent);
        }

        private Transform GetOrCreateContainer(string name)
        {
            Transform existing = transform.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void ClearContainer(string name)
        {
            Transform container = transform.Find(name);
            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    GameObject child = container.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        // Runtime NavMesh baking happens before deferred destruction completes.
                        // Retired obstacles must be inactive immediately.
                        child.SetActive(false);
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }
        }
    }
}
