using UnityEngine;

namespace LightSouls.Game
{
    /// <summary>
    /// Điểm lắp ráp duy nhất của Scene Game: nối bộ sinh màn, nhân vật/chiến đấu và giao diện với nhau.
    /// Các hệ thống không tham chiếu lẫn nhau; mọi kết nối giữa chúng nằm ở đây (Docs/Conventions.md mục 3 và 4).
    ///
    /// Hiện là khung rỗng. Khi PR sinh màn và PR chiến đấu được merge, thêm "LightSouls.MapGeneration"
    /// và "LightSouls.Combat" vào references của LightSouls.Game.asmdef rồi cài đặt theo thứ tự:
    ///   1. Gọi bộ sinh màn tạo màn và chờ OnDungeonGenerated.
    ///   2. Đặt người chơi tại ctx.PlayerSpawnPoint.
    ///   3. Với mỗi phòng trong ctx.SpawnedRooms: nghe OnRoomEnteredCombat, gọi hệ thống chiến đấu sinh quái
    ///      tại room.EnemySpawnPoints, và gọi room.LifecycleController.ClearRoom() khi hết quái.
    ///   4. Khi người chơi tới ctx.LevelExitPoint thì báo qua màn.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
    }
}
