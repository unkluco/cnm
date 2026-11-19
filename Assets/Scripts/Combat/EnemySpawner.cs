using System;
using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat
{
    /// <summary>
    /// Thành phần sinh quái vật trong phòng chiến đấu (Enemy Spawner).
    /// Nhận danh sách điểm sinh từ RoomModule / GameBootstrap, tự động sinh quái
    /// và phát sự kiện AllEnemiesDefeated khi toàn bộ quái trong đợt đã chết.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Kho Prefab Quái vật")]
        [Tooltip("Danh sách prefab quái có thể sinh (Zombie, Goblin, Cultist...)")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [Tooltip("Các quái có chiêu riêng, được luân phiên để mỗi phòng có thử thách khác nhau.")]
        [SerializeField] private GameObject[] specialEnemyPrefabs;
        [SerializeField, Range(0f, 1f)] private float extraSpecialChance = 0.35f;

        [Header("Tùy chọn thử nghiệm")]
        [SerializeField] private bool autoSpawnOnStart = false;
        [SerializeField] private Transform[] testSpawnPoints;

        private readonly List<IDamageable> _aliveEnemies = new List<IDamageable>();
        private readonly List<GameObject> _spawnedEnemyObjects = new List<GameObject>();
        private readonly List<GameObject> _specialBag = new List<GameObject>();
        private GameObject _lastSpecial;

        public IReadOnlyList<GameObject> SpecialEnemyPrefabs => specialEnemyPrefabs ?? Array.Empty<GameObject>();

        public void SetSpecialEnemyPrefabs(GameObject[] prefabs)
        {
            specialEnemyPrefabs = prefabs;
            _specialBag.Clear();
            _lastSpecial = null;
        }

        /// <summary>
        /// Phát sự kiện khi toàn bộ quái vật sinh ra trong đợt đã chết.
        /// GameBootstrap lắng nghe sự kiện này để gọi ClearRoom() mở cửa phòng.
        /// </summary>
        public event Action AllEnemiesDefeated;

        /// <summary>
        /// Số lượng quái vật hiện còn sống trong phòng.
        /// </summary>
        public int AliveEnemyCount => _aliveEnemies.Count;

        /// <summary>
        /// True nếu đang có quái vật chiến đấu trong phòng.
        /// </summary>
        public bool HasActiveWave => _aliveEnemies.Count > 0;

        private void Start()
        {
            if (autoSpawnOnStart && testSpawnPoints != null && testSpawnPoints.Length > 0)
            {
                SpawnWave(testSpawnPoints);
            }
        }

        /// <summary>
        /// Cấu hình danh sách prefab quái vật từ code (nếu cần).
        /// </summary>
        public void SetEnemyPrefabs(GameObject[] prefabs)
        {
            enemyPrefabs = prefabs;
        }

        /// <summary>
        /// Hàm sinh quái vật theo danh sách điểm sinh được cung cấp bởi RoomModule.
        /// </summary>
        /// <param name="spawnPoints">Danh sách Transform các điểm sinh trong phòng</param>
        public void SpawnWave(IReadOnlyList<Transform> spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("[EnemySpawner] Danh sách điểm sinh rỗng, kích hoạt hoàn thành ngay.");
                AllEnemiesDefeated?.Invoke();
                return;
            }

            bool hasSpecials = specialEnemyPrefabs != null && Array.Exists(specialEnemyPrefabs, prefab => prefab != null);
            bool hasNormal = enemyPrefabs != null && Array.Exists(enemyPrefabs, prefab => prefab != null);
            if (!hasNormal && !hasSpecials)
            {
                Debug.LogError("[EnemySpawner] Chưa cấu hình enemyPrefabs! Không thể sinh quái.");
                AllEnemiesDefeated?.Invoke();
                return;
            }

            // Dọn dẹp quái cũ nếu còn sót lại
            ClearEnemies();

            Debug.Log($"[EnemySpawner] Bắt đầu sinh đợt quái tại {spawnPoints.Count} điểm sinh.");

            bool spawnedSpecial = false;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Transform sp = spawnPoints[i];
                if (sp == null) continue;

                // Mỗi đợt có ít nhất một quái đặc biệt nếu đã cấu hình; các ô còn lại trộn cả quái thường.
                bool useSpecial = hasSpecials && (!spawnedSpecial || !hasNormal || UnityEngine.Random.value < extraSpecialChance);
                GameObject prefab = useSpecial ? TakeSpecialPrefab() : PickNormalPrefab(i);
                if (prefab == null) continue;

                spawnedSpecial |= useSpecial;

                GameObject enemyObj = Instantiate(prefab, sp.position, sp.rotation);
                _spawnedEnemyObjects.Add(enemyObj);

                IDamageable damageable = enemyObj.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = enemyObj.GetComponentInChildren<IDamageable>();
                }

                if (damageable != null)
                {
                    _aliveEnemies.Add(damageable);
                    damageable.Died += HandleEnemyDied;
                }
                else
                {
                    Debug.LogWarning($"[EnemySpawner] Quái {enemyObj.name} không có component cài đặt IDamageable!");
                }
            }

            if (_aliveEnemies.Count == 0)
            {
                Debug.LogWarning("[EnemySpawner] Không sinh được quái hợp lệ, kích hoạt hoàn thành.");
                AllEnemiesDefeated?.Invoke();
            }
        }

        private GameObject PickNormalPrefab(int index)
        {
            if (enemyPrefabs == null) return null;
            for (int offset = 0; offset < enemyPrefabs.Length; offset++)
            {
                var prefab = enemyPrefabs[(index + offset) % enemyPrefabs.Length];
                if (prefab != null) return prefab;
            }
            return null;
        }

        private GameObject TakeSpecialPrefab()
        {
            if (_specialBag.Count == 0)
            {
                foreach (var prefab in specialEnemyPrefabs)
                    if (prefab != null && !_specialBag.Contains(prefab)) _specialBag.Add(prefab);
                for (int i = _specialBag.Count - 1; i > 0; i--)
                {
                    int other = UnityEngine.Random.Range(0, i + 1);
                    (_specialBag[i], _specialBag[other]) = (_specialBag[other], _specialBag[i]);
                }
                // Không lặp ngay khi chuyển sang một chu kỳ quái mới.
                int last = _specialBag.Count - 1;
                if (last > 0 && _specialBag[last] == _lastSpecial)
                    (_specialBag[0], _specialBag[last]) = (_specialBag[last], _specialBag[0]);
            }

            if (_specialBag.Count == 0) return null;
            int index = _specialBag.Count - 1;
            var result = _specialBag[index];
            _specialBag.RemoveAt(index);
            _lastSpecial = result;
            return result;
        }

        private void HandleEnemyDied(IDamageable deadEnemy)
        {
            if (deadEnemy != null)
            {
                deadEnemy.Died -= HandleEnemyDied;
                _aliveEnemies.Remove(deadEnemy);
            }

            Debug.Log($"[EnemySpawner] Quái bị tiêu diệt! Còn lại: {_aliveEnemies.Count}");

            if (_aliveEnemies.Count == 0)
            {
                Debug.Log("[EnemySpawner] TOÀN BỘ QUÁI ĐÃ BỊ TIÊU DIỆT! Phát sự kiện AllEnemiesDefeated.");
                AllEnemiesDefeated?.Invoke();
            }
        }

        /// <summary>
        /// Dọn dẹp toàn bộ quái vật đang tồn tại (khi reset scene hoặc rời phòng).
        /// </summary>
        public void ClearEnemies()
        {
            foreach (var d in _aliveEnemies)
            {
                if (d != null)
                {
                    d.Died -= HandleEnemyDied;
                }
            }
            _aliveEnemies.Clear();

            foreach (var obj in _spawnedEnemyObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            _spawnedEnemyObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearEnemies();
        }
    }
}
