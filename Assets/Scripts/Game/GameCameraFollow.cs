using UnityEngine;

namespace LightSouls.Game
{
    /// <summary>
    /// Camera của Scene Game: nhìn chéo từ trên cao xuống và bám theo người chơi.
    /// Góc quay cố định (không xoay theo người chơi) để cần điều khiển "lên" luôn là hướng lên màn hình.
    /// </summary>
    public class GameCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Tooltip("Góc nghiêng xuống, độ. 90 là nhìn thẳng từ trên xuống.")]
        [SerializeField, Range(30f, 90f)] private float pitch = 58f;

        [Tooltip("Khoảng cách từ camera tới điểm nhìn trên người chơi.")]
        [SerializeField] private float distance = 17f;

        [Tooltip("Điểm nhìn cao hơn chân người chơi bao nhiêu mét.")]
        [SerializeField] private float lookAtHeight = 1f;

        [Tooltip("Thời gian bám theo, giây. Càng nhỏ càng bám sát.")]
        [SerializeField] private float smoothTime = 0.12f;

        private Vector3 velocity;

        private void Awake()
        {
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Đổi đối tượng bám theo. snap = true thì nhảy ngay tới vị trí mới, không trượt.</summary>
        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap && target != null)
            {
                velocity = Vector3.zero;
                transform.position = DesiredPosition();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
        }

        private Vector3 DesiredPosition()
        {
            var lookAt = target.position + Vector3.up * lookAtHeight;
            return lookAt - Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward * distance;
        }
    }
}
