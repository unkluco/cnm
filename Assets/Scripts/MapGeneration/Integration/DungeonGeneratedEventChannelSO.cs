using System;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// ScriptableObject Event Channel (inspired by Unity Open Project #1 architecture).
    /// Allows Map Generation and Combat/GameManager systems to communicate without direct scene dependencies.
    /// </summary>
    [CreateAssetMenu(fileName = "OnDungeonGenerated", menuName = "LightSouls/Map Generation/Events/Dungeon Generated Channel")]
    public class DungeonGeneratedEventChannelSO : ScriptableObject
    {
        public event Action<DungeonRuntimeContext> OnEventRaised;

        /// <summary>
        /// Cached latest context so late-joining listeners (e.g. async spawners) can inspect current level state.
        /// </summary>
        public DungeonRuntimeContext LastContext { get; private set; }

        private void OnDisable()
        {
            LastContext = null;
        }

        public void RaiseEvent(DungeonRuntimeContext context)
        {
            LastContext = context;
            OnEventRaised?.Invoke(context);
        }
    }
}
