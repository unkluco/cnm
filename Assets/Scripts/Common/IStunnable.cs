namespace LightSouls.Common
{
    /// <summary>Đối tượng có thể bị choáng (đứng yên, không tấn công) trong một khoảng thời gian.</summary>
    public interface IStunnable
    {
        /// <summary>Choáng thêm duration giây. Gọi khi đang choáng thì lấy mốc kết thúc muộn hơn.</summary>
        void Stun(float duration);

        bool IsStunned { get; }
    }
}
