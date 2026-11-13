using System;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// ScriptableObject Runtime Anchor (inspired by Unity Open Project #1 architecture).
    /// Exposes a single runtime scene Transform (such as PlayerSpawnPoint or LevelExitPoint)
    /// to any prefab or system without FindObjectOfType or Singleton coupling.
    /// </summary>
    [CreateAssetMenu(fileName = "TransformAnchor", menuName = "LightSouls/Map Generation/Anchors/Transform Anchor")]
    public class TransformAnchorSO : ScriptableObject
    {
        public event Action<Transform> OnAnchorProvided;

        public Transform Value { get; private set; }
        public bool IsSet => Value != null;

        private void OnDisable()
        {
            Value = null;
        }

        public void Provide(Transform target)
        {
            Value = target;
            OnAnchorProvided?.Invoke(target);
        }

        public void Clear()
        {
            Value = null;
        }
    }
}
