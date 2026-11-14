using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Các bảng mở đè lên màn chơi (balo, cửa hàng…) để nút Back của Android (phím Esc) đóng chúng trước,
    /// thay vì mở menu tạm dừng.
    ///
    /// Bảng nào mở thì Push(this, Close), đóng thì Remove(this). Không tự nghe UI/Cancel:
    /// PauseMenuController gọi TryCloseTop() trước, chỉ khi không còn bảng nào mở thì mới tạm dừng.
    /// </summary>
    public static class BackButtonStack
    {
        private struct Entry
        {
            public UnityEngine.Object Owner;
            public Action Close;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        public static bool HasOpenPanel
        {
            get
            {
                RemoveDestroyed();
                return entries.Count > 0;
            }
        }

        /// <summary>Đưa bảng lên trên cùng. Gọi lại với cùng owner thì chỉ đưa nó lên trên cùng.</summary>
        public static void Push(UnityEngine.Object owner, Action close)
        {
            if (owner == null || close == null)
            {
                return;
            }

            Remove(owner);
            entries.Add(new Entry { Owner = owner, Close = close });
        }

        public static void Remove(UnityEngine.Object owner)
        {
            entries.RemoveAll(entry => entry.Owner == owner);
        }

        /// <summary>Đóng bảng trên cùng. Trả về false khi không có bảng nào mở.</summary>
        public static bool TryCloseTop()
        {
            RemoveDestroyed();
            if (entries.Count == 0)
            {
                return false;
            }

            var top = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            top.Close();
            return true;
        }

        private static void RemoveDestroyed()
        {
            // Bảng bị hủy cùng Scene cũ mà chưa kịp Remove.
            entries.RemoveAll(entry => entry.Owner == null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            entries.Clear();
        }
    }
}
