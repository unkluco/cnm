using UnityEngine;

namespace LightSouls.Combat.Testing
{
    /// <summary>
    /// Camera góc nhìn Top-down isometric mượt mà bám theo mục tiêu (Player).
    /// </summary>
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -7f);
        [SerializeField] private float smoothSpeed = 8f;

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
