using LightSouls.Lobby;
using LightSouls.UI;
using UnityEngine;

namespace LightSouls.Game
{
    /// <summary>
    /// Điểm lắp ráp của Scene Lobby (sảnh), tương tự GameBootstrap của Scene Game.
    /// Sảnh không được tham chiếu UI (Docs/Conventions.md mục 3), nên việc chuyển Scene nằm ở đây:
    /// người chơi chọn ngục ở cổng → LobbyController.DungeonChosen → nạp Scene Game.
    ///
    /// Luồng một lượt chơi:
    ///   MainMenu (Chơi) → Lobby (mua đồ, chọn ngục) → Game (qua màn) → Lobby → ...
    ///   Thua trong Game → lượt mới bắt đầu lại ở Lobby (mất xu và đồ).
    /// </summary>
    public class LobbyBootstrap : MonoBehaviour
    {
        private bool isLoading;

        private void OnEnable()
        {
            LobbyController.DungeonChosen += HandleDungeonChosen;
        }

        private void OnDisable()
        {
            LobbyController.DungeonChosen -= HandleDungeonChosen;
        }

        private void HandleDungeonChosen(string dungeonId)
        {
            // Bấm chọn hai lần liền nhau không được nạp Scene hai lần.
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            SceneLoader.LoadGame();
        }
    }
}
