namespace LightSouls.Common
{
    /// <summary>
    /// Nhân vật có tốc độ di chuyển bị ảnh hưởng từ bên ngoài (nước làm chậm, bùn, băng…).
    /// Hệ thống chiến đấu cài đặt cho người chơi và quái; địa hình đặc biệt của sinh màn gọi vào.
    ///
    /// Mỗi nguồn ghi một hệ số riêng, khóa bằng chính đối tượng nguồn (thường là component của vùng địa hình).
    /// Tốc độ thật = tốc độ gốc × tích mọi hệ số đang có. Nguồn rời đi thì gọi RemoveSpeedMultiplier;
    /// gọi Remove cho nguồn chưa đăng ký thì không làm gì.
    /// </summary>
    public interface ISpeedModifiable
    {
        /// <summary>Đặt (hoặc thay) hệ số của nguồn này. Ví dụ 0.5 là chậm một nửa. Giá trị âm được coi là 0.</summary>
        void SetSpeedMultiplier(object source, float multiplier);

        void RemoveSpeedMultiplier(object source);
    }
}
