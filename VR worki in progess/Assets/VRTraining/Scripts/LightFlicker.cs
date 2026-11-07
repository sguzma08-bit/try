using System.Collections;
using UnityEngine;

namespace VRTraining
{
    /// <summary>Makes a light stutter every few seconds, with an optional buzz loop and crackle sounds.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public Vector2 secondsBetweenFlickers = new Vector2(3f, 9f);
        public Vector2 flickerLength = new Vector2(0.15f, 0.7f);
        [Range(0, 1)] public float dimTo = 0.05f;

        [Header("Sound (optional)")]
        public AudioSource audioSource;
        [Tooltip("Constant fluorescent hum / buzz (loops).")]
        public AudioClip buzzLoop;
        [Tooltip("Electrical crackle / spark played when it flickers.")]
        public AudioClip[] crackleClips;

        Light _light;
        float _base;

        void Start()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            if (audioSource && buzzLoop) { audioSource.clip = buzzLoop; audioSource.loop = true; audioSource.Play(); }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(secondsBetweenFlickers.x, secondsBetweenFlickers.y));
                if (!_light.enabled) continue;   // e.g. lights are out during the scare
                if (audioSource && crackleClips != null && crackleClips.Length > 0)
                {
                    var c = crackleClips[Random.Range(0, crackleClips.Length)];
                    if (c) audioSource.PlayOneShot(c);
                }
                float end = Time.time + Random.Range(flickerLength.x, flickerLength.y);
                while (Time.time < end)
                {
                    _light.intensity = Random.value < 0.5f ? _base * dimTo : _base * Random.Range(0.6f, 1.1f);
                    if (audioSource && buzzLoop) audioSource.volume = _light.intensity / Mathf.Max(0.01f, _base);
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.09f));
                }
                _light.intensity = _base;
                if (audioSource) audioSource.volume = 1f;
            }
        }
    }
}
