using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Vật người chơi bấm nút Tương tác để dùng: vật phẩm rơi, rương, quầy hàng, cổng vào ngục…
    /// Hệ thống chiến đấu (Khải) làm phía người chơi: tìm vật gần nhất, hiện nút và chữ Prompt, gọi Interact khi bấm.
    /// Ai làm vật đó thì cài đặt interface này trên một collider trigger đặt layer Interactable.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Chữ hiện trên nút tương tác, ngắn gọn. Ví dụ: "Nhặt", "Mua", "Vào ngục".</summary>
        string Prompt { get; }

        /// <summary>False thì không hiện nút (ví dụ rương đã mở, không đủ xu thì vẫn hiện để báo lỗi khi bấm).</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>interactor là đối tượng gốc của người chơi.</summary>
        void Interact(GameObject interactor);
    }
}
