using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>One shadowless, softly flickering light; the scene budget decides whether it contributes.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkFantasyTorchLight : MonoBehaviour
    {
        private static readonly List<DarkFantasyTorchLight> Registered = new List<DarkFantasyTorchLight>();
        public static IReadOnlyList<DarkFantasyTorchLight> ActiveLights => Registered;

        [SerializeField] private Light source;
        [SerializeField] private Transform flameVisual;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private float baseIntensity = 3.1f;
        [SerializeField, Range(0f, 0.25f)] private float flickerAmount = 0.1f;
        private float phase;
        private Vector3 flameScale;

        public Light Source => source;

        public void Configure(Light light, float intensity, float flicker = 0.1f, Transform flame = null, ParticleSystem sparks = null)
        {
            source = light;
            flameVisual = flame;
            embers = sparks;
            baseIntensity = intensity;
            flickerAmount = flicker;
            if (source != null)
            {
                source.intensity = intensity;
                source.shadows = LightShadows.None;
            }
        }

        private void OnEnable()
        {
            if (source == null) source = GetComponent<Light>();
            if (source == null) return;
            phase = Mathf.Abs(transform.position.x * 0.73f + transform.position.z * 1.29f);
            if (flameVisual != null) flameScale = flameVisual.localScale;
            if (!Registered.Contains(this)) Registered.Add(this);
            // Scene lighting budget enables only the nearest sources during runtime.
            if (Application.isPlaying) source.enabled = false;
            if (embers != null && Application.isPlaying) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
            if (Application.isPlaying && source != null) source.enabled = false;
            if (flameVisual != null) flameVisual.localScale = flameScale;
            if (embers != null && Application.isPlaying) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetContributing(bool contributes)
        {
            if (source == null) return;
            if (source.enabled != contributes) source.enabled = contributes;
            if (!contributes) source.intensity = baseIntensity;
            if (!contributes && flameVisual != null) flameVisual.localScale = flameScale;
            if (embers != null && Application.isPlaying)
            {
                if (contributes && !embers.isPlaying) embers.Play();
                else if (!contributes && embers.isPlaying) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Update()
        {
            if (source == null || !source.enabled) return;
            float t = Time.time * 3.5f + phase;
            float flicker = 0.65f * Mathf.Sin(t) + 0.35f * Mathf.Sin(t * 2.17f);
            source.intensity = baseIntensity * (1f + flickerAmount * flicker);
            if (flameVisual != null) flameVisual.localScale = Vector3.Scale(flameScale, new Vector3(1f + flicker * .04f, 1f + flicker * .12f, 1f + flicker * .04f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Registered.Clear();
    }
}
