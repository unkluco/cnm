using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using LightSouls.Common;
using LightSouls.Combat.Effects;

namespace LightSouls.Combat
{
    /// <summary>
    /// Điều khiển di chuyển nhân vật góc nhìn Top-Down.
    /// Hỗ trợ cả bàn phím (WASD), cần Analog ảo, kỹ năng Lướt (Dash),
    /// và diễn hoạt mượt mà các chi (chân, tay, thân người nghiêng nhấp nhô về trước-sau khi chạy).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Thông số di chuyển")]
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = 20f;

        [Header("Kỹ năng Lướt (Dash Skill)")]
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float dashDuration = 0.22f;
        [SerializeField] private float dashCooldown = 1.0f;
        [SerializeField] private DashTrailEffect dashTrailEffect;

        [Header("Input System")]
        [Tooltip("Action Move từ Input Asset (Map Player).")]
        [SerializeField] private InputActionReference moveAction;
        [Tooltip("Action Dash từ Input Asset (Map Player).")]
        [SerializeField] private InputActionReference dashAction;

        [Header("Khớp chi mô hình người (Procedural Animation)")]
        [SerializeField] private Transform torso;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        private CharacterController _controller;
        private Vector2 _inputVector;
        private Vector3 _velocity;
        private bool _isInputActive = true;

        private bool _isDashing = false;
        private float _lastDashTime = -999f;
        private Vector3 _dashDirection;

        // Góc gốc của cánh tay phải khi giương vũ khí (Combat Stance)
        private Quaternion _rightArmDefaultRotation = Quaternion.Euler(-35f, -8f, 12f);
        private Quaternion _torsoDefaultRotation = Quaternion.identity;

        public Vector2 InputVector => _inputVector;
        public bool IsMoving => _inputVector.sqrMagnitude > 0.01f;
        public bool IsDashing => _isDashing;
        public Vector3 FacingDirection => transform.forward;
        public float DashCooldownRemaining => Mathf.Max(0f, (_lastDashTime + dashCooldown) - Time.time);
        public float DashCooldownRatio => Mathf.Clamp01(DashCooldownRemaining / dashCooldown);

        public event Action OnDashExecuted;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // Cấu hình kích thước CharacterController đúng chuẩn Humanoid
            _controller.height = 1.8f;
            _controller.radius = 0.45f;
            _controller.center = new Vector3(0f, 0.9f, 0f);

            if (rightArm != null)
            {
                _rightArmDefaultRotation = rightArm.localRotation;
            }
            if (torso != null)
            {
                _torsoDefaultRotation = torso.localRotation;
            }

            if (dashTrailEffect == null)
            {
                dashTrailEffect = GetComponent<DashTrailEffect>();
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }

            if (dashAction != null && dashAction.action != null)
            {
                dashAction.action.Enable();
                dashAction.action.performed += OnDashInput;
            }
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }

            if (dashAction != null && dashAction.action != null)
            {
                dashAction.action.performed -= OnDashInput;
                dashAction.action.Disable();
            }
        }

        private void Update()
        {
            ReadMovementInput();

            if (_isDashing)
            {
                // Khi đang dash: di chuyển tốc độ cao theo hướng dash
                _controller.Move(_dashDirection * (dashSpeed * Time.deltaTime));
            }
            else
            {
                ApplyMovement();
                ApplyRotation();
            }

            AnimateLimbsAndTorso();
        }

        private void OnDashInput(InputAction.CallbackContext context)
        {
            if (!_isInputActive) return;
            PerformDash();
        }

        /// <summary>
        /// Kích hoạt kỹ năng Lướt (Dash).
        /// Lướt nhân vật về hướng đang di chuyển (hoặc hướng đang nhìn nếu đứng yên),
        /// tạo vệt trắng phía sau.
        /// </summary>
        public void PerformDash()
        {
            if (_isDashing || !_isInputActive) return;
            if (Time.time < _lastDashTime + dashCooldown) return;

            _lastDashTime = Time.time;

            // Xác định hướng lướt: ưu tiên hướng đang di chuyển
            if (IsMoving)
            {
                _dashDirection = new Vector3(_inputVector.x, 0f, _inputVector.y).normalized;
            }
            else
            {
                _dashDirection = transform.forward;
            }

            // Xoay tức thì theo hướng lướt
            transform.rotation = Quaternion.LookRotation(_dashDirection, Vector3.up);

            StartCoroutine(DashRoutine());

            OnDashExecuted?.Invoke();

            // Rung phản hồi nếu thiết bị hỗ trợ
            if (GameSettings.VibrationEnabled)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
                #endif
            }
        }

        private IEnumerator DashRoutine()
        {
            _isDashing = true;

            if (dashTrailEffect != null)
            {
                dashTrailEffect.StartDashTrail(dashDuration);
            }

            yield return new WaitForSeconds(dashDuration);

            _isDashing = false;

            if (dashTrailEffect != null)
            {
                dashTrailEffect.StopDashTrail();
            }
        }

        private void AnimateLimbsAndTorso()
        {
            if (_isDashing)
            {
                // Khi Dash: người chúi mạnh về phía trước, chân co lại tạo thế lao đi
                if (torso != null)
                {
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(24f, 0f, 0f), 20f * Time.deltaTime);
                }
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(35f, 0f, 0f);
                return;
            }

            if (IsMoving)
            {
                float cycle = Mathf.Sin(Time.time * 12f);
                float bobCycle = Mathf.Sin(Time.time * 24f);

                // 1. Chân bước nhịp nhàng luân phiên
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(cycle * 32f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-cycle * 32f, 0f, 0f);

                // 2. Tay trái vung ngược pha với chân trái
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-cycle * 28f, 0f, 0f);

                // 3. Tay phải (cầm vũ khí đưa lên 1 góc): giữ tư thế cầm vũ khí và nhấp nhô nhẹ đồng bộ bước chạy
                if (rightArm != null)
                {
                    Quaternion bob = Quaternion.Euler(bobCycle * 4f, 0f, 0f);
                    rightArm.localRotation = _rightArmDefaultRotation * bob;
                }

                // 4. Thân người (Torso): nghiêng về phía trước 10-12 độ và nhấp nhô trước-sau theo bước chạy
                if (torso != null)
                {
                    float forwardLean = 11f;
                    float sway = bobCycle * 3f;
                    torso.localRotation = Quaternion.Euler(forwardLean + sway, 0f, 0f);
                }
            }
            else
            {
                // Khi đứng yên: trở về tư thế idle tự nhiên, thân người thở nhẹ
                float idleBreathing = Mathf.Sin(Time.time * 2.5f) * 1.5f;

                if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, _rightArmDefaultRotation, 10f * Time.deltaTime);
                if (torso != null)
                {
                    Quaternion targetIdle = _torsoDefaultRotation * Quaternion.Euler(idleBreathing, 0f, 0f);
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, targetIdle, 10f * Time.deltaTime);
                }
            }
        }

        private void ReadMovementInput()
        {
            if (!_isInputActive || _isDashing)
            {
                _inputVector = Vector2.zero;
                return;
            }

            // Đọc input qua action Player/Move (bàn phím, tay cầm và cần analog trên màn hình đều đi qua action này).
            _inputVector = moveAction != null && moveAction.action != null
                ? moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;

            // Giới hạn độ dài vector input tối đa là 1 (tránh di chuyển chéo bị nhanh hơn)
            if (_inputVector.sqrMagnitude > 1f)
            {
                _inputVector.Normalize();
            }
        }

        private void ApplyMovement()
        {
            // Chuyển đổi input 2D sang không gian 3D thế giới (X và Z)
            Vector3 moveDirection = new Vector3(_inputVector.x, 0f, _inputVector.y);
            Vector3 motion = moveDirection * (moveSpeed * Time.deltaTime);

            // Xử lý trọng lực để chân luôn chạm sàn Y=0
            if (_controller.isGrounded)
            {
                _velocity.y = -0.5f;
            }
            else
            {
                _velocity.y -= gravity * Time.deltaTime;
            }

            motion.y = _velocity.y * Time.deltaTime;
            _controller.Move(motion);
        }

        private void ApplyRotation()
        {
            if (_inputVector.sqrMagnitude > 0.04f)
            {
                Vector3 targetDir = new Vector3(_inputVector.x, 0f, _inputVector.y);
                Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        public void SetInputActive(bool active)
        {
            _isInputActive = active;
            if (!active)
            {
                _inputVector = Vector2.zero;
            }
        }

        public void RotateTowards(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public void SetArmAndTorsoReferences(Transform tTorso, Transform tLeftArm, Transform tRightArm, Transform tLeftLeg, Transform tRightLeg)
        {
            torso = tTorso;
            leftArm = tLeftArm;
            rightArm = tRightArm;
            leftLeg = tLeftLeg;
            rightLeg = tRightLeg;

            if (rightArm != null) _rightArmDefaultRotation = rightArm.localRotation;
            if (torso != null) _torsoDefaultRotation = torso.localRotation;
        }
    }
}
