using System;

namespace LightSouls.Common
{
    /// <summary>
    /// Mọi đối tượng có máu (người chơi, quái, boss, vật phá được) cài đặt interface này.
    /// Đòn đánh tìm IDamageable trên collider trúng rồi gọi TakeDamage; không cần biết lớp cụ thể.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True khi máu đã về 0. Đối tượng đã chết phải bỏ qua các lần TakeDamage tiếp theo.</summary>
        bool IsDead { get; }

        void TakeDamage(DamageInfo info);

        /// <summary>Phát đúng một lần, ngay khi máu về 0.</summary>
        event Action<IDamageable> Died;
    }
}
