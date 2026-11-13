using System.Collections;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Testing
{
    /// <summary>
    /// Bia tập bắn / quái vật giả lập để kiểm tra sát thương và tương tác hai chiều.
    /// Tag: Enemy, Layer: Enemy (7).
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PracticeDummy : MonoBehaviour
    {
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Color normalColor = new Color(0.9f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.1f;
        [SerializeField] private bool autoRevive = true;
        [SerializeField] private float reviveDelay = 2.5f;

        private HealthComponent _health;
        private Material _instancedMat;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            gameObject.tag = "Enemy";
            gameObject.layer = 7; // Layer Enemy = 7

            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<Renderer>();
            }

            if (meshRenderer != null)
            {
                _instancedMat = meshRenderer.material;
                _instancedMat.color = normalColor;
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.Died -= HandleDied;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashHitRoutine());
        }

        private void HandleDied(IDamageable damageable)
        {
            Debug.Log($"[PracticeDummy] {gameObject.name} đã bị hạ gục!");
            if (autoRevive)
            {
                StartCoroutine(ReviveRoutine());
            }
        }

        private IEnumerator FlashHitRoutine()
        {
            if (_instancedMat != null)
            {
                _instancedMat.color = hitColor;
                transform.localScale = new Vector3(1.15f, 0.9f, 1.15f);
                yield return new WaitForSeconds(hitFlashDuration);
                _instancedMat.color = normalColor;
                transform.localScale = Vector3.one;
            }
        }

        private IEnumerator ReviveRoutine()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            yield return new WaitForSeconds(reviveDelay);
            if (meshRenderer != null) meshRenderer.enabled = true;
            _health.ResetHealth();
        }
    }
}
