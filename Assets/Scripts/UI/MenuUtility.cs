using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Lưu trạng thái con trỏ chuột trước khi mở menu và khôi phục khi đóng,
    /// để menu dùng được chuột kể cả khi nhân vật đang khóa con trỏ.
    /// </summary>
    internal struct CursorState
    {
        private CursorLockMode lockMode;
        private bool visible;

        public static CursorState CaptureAndRelease()
        {
            var state = new CursorState { lockMode = Cursor.lockState, visible = Cursor.visible };
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return state;
        }

        public void Restore()
        {
            Cursor.lockState = lockMode;
            Cursor.visible = visible;
        }
    }

    internal static class MenuUtility
    {
        /// <summary>Chọn sẵn một nút để điều hướng được bằng bàn phím hoặc tay cầm.</summary>
        public static void Select(Selectable target)
        {
            if (target != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
