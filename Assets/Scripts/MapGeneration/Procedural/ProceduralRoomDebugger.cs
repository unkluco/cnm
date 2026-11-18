using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Công cụ Debug trực quan trong Scene View và Inspector cho việc sinh địa hình chiến thuật:
    /// Cung cấp Gizmos hiển thị ranh giới phòng, vùng an toàn cửa 2.5m, đường an toàn liên tục giữa các cửa,
    /// chân đế hazard và các vị trí spawn quái vật.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RoomModule))]
    public class ProceduralRoomDebugger : MonoBehaviour
    {
        [Header("Seed & Pattern Info")]
        [SerializeField] private int activeSeed = 101;
        [SerializeField] private TerrainTheme theme = TerrainTheme.Lava;
        [SerializeField] private string selectedPattern = "Not Generated";
        [SerializeField] private float qualityScore = 0f;

        [Header("Candidate Generation Diagnostics")]
        [SerializeField] private int candidatesGenerated = 10;
        [SerializeField] private int validCandidatesCount = 0;
        [SerializeField] private string lastRejectionReason = "None";

        [Header("Gizmos Visualization Options")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showRoomBounds = true;
        [SerializeField] private bool showSafeZones = true;
        [SerializeField] private bool showSafeRoutes = true;
        [SerializeField] private bool showHazardFootprints = true;
        [SerializeField] private bool showEnemySpawns = true;

        [Header("Gizmos Colors")]
        [SerializeField] private Color safeZoneColor = new Color(0.2f, 0.9f, 0.3f, 0.7f);
        [SerializeField] private Color safeRouteColor = new Color(0.1f, 0.85f, 1.0f, 0.9f);
        [SerializeField] private Color hazardColor = new Color(1.0f, 0.25f, 0.15f, 0.75f);
        [SerializeField] private Color enemySpawnColor = new Color(0.95f, 0.2f, 0.85f, 0.85f);

        private RoomModule roomModule;
        private ProceduralRoomApplicator applicator;

        public int ActiveSeed => activeSeed;
        public TerrainTheme Theme => theme;
        public string SelectedPattern => selectedPattern;

        private void OnEnable()
        {
            roomModule = GetComponent<RoomModule>();
            applicator = GetComponent<ProceduralRoomApplicator>();
            UpdateStatusFromApplicator();
        }

        public void SetDiagnostics(int seed, TerrainTheme t, string pattern, float score, int generated, int valid, string rejectionReason)
        {
            activeSeed = seed;
            theme = t;
            selectedPattern = pattern;
            qualityScore = score;
            candidatesGenerated = generated;
            validCandidatesCount = valid;
            lastRejectionReason = string.IsNullOrEmpty(rejectionReason) ? "None" : rejectionReason;
        }

        public void UpdateStatusFromApplicator()
        {
            if (applicator != null)
            {
                theme = applicator.Theme;
                activeSeed = applicator.CustomSeed;
                selectedPattern = applicator.ActivePatternName;
                qualityScore = applicator.ActiveQualityScore;
            }
        }

        [ContextMenu("Regenerate Layout (New Random Seed)")]
        public void RegenerateNewSeed()
        {
            if (applicator != null)
            {
                applicator.GenerateWithRandomSeed();
                UpdateStatusFromApplicator();
            }
        }

        [ContextMenu("Reapply Active Seed")]
        public void ReapplyActiveSeed()
        {
            if (applicator != null)
            {
                applicator.GenerateAndApply(activeSeed);
                UpdateStatusFromApplicator();
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (roomModule == null) roomModule = GetComponent<RoomModule>();

            Vector3 worldCenter = transform.position;

            // 1. Ranh giới phòng (Room Bounds)
            if (showRoomBounds)
            {
                Gizmos.color = new Color(0.6f, 0.6f, 0.65f, 0.4f);
                Gizmos.DrawWireCube(worldCenter + roomModule.LocalBounds.center, roomModule.LocalBounds.size);
            }

            // 2. Vùng an toàn cửa (Doorway Safe Zones 2.5m)
            if (showSafeZones)
            {
                Gizmos.color = safeZoneColor;
                foreach (DoorSocket socket in roomModule.DoorSockets)
                {
                    if (socket == null) continue;
                    Vector3 sPos = socket.transform.position;
                    Gizmos.DrawWireSphere(sPos, ProceduralLayoutGenerator.DoorwayClearanceRadius);
                }
            }

            // 3. Đường an toàn đã xác minh (Verified Safe Routes)
            if (showSafeRoutes && applicator != null && applicator.ActiveLayout != null)
            {
                Gizmos.color = safeRouteColor;
                foreach (var route in applicator.ActiveLayout.VerifiedSafeRoutes)
                {
                    if (route == null || route.Count < 2) continue;
                    for (int i = 0; i < route.Count - 1; i++)
                    {
                        Vector3 p1 = transform.TransformPoint(route[i]);
                        Vector3 p2 = transform.TransformPoint(route[i + 1]);
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }

            // 4. Chân đế các Hazard (Hazard Footprints)
            if (showHazardFootprints && applicator != null && applicator.ActiveLayout != null)
            {
                Gizmos.color = hazardColor;
                foreach (var h in applicator.ActiveLayout.Hazards)
                {
                    Bounds localB = h.GetLocalBounds();
                    Gizmos.DrawWireCube(transform.TransformPoint(localB.center), localB.size);
                }
            }

            // 5. Điểm sinh quái (Enemy Spawns)
            if (showEnemySpawns && applicator != null && applicator.ActiveLayout != null)
            {
                Gizmos.color = enemySpawnColor;
                foreach (var pt in applicator.ActiveLayout.EnemySpawns)
                {
                    Vector3 worldPt = transform.TransformPoint(pt);
                    Gizmos.DrawSphere(worldPt + Vector3.up * 0.5f, 0.4f);
                }
            }
        }
    }
}
