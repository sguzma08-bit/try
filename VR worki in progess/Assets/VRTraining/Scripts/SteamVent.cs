using System.Collections;
using UnityEngine;

namespace VRTraining
{
    /// <summary>Puffs of steam every few seconds from the Particle System on this object, with a hiss.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SteamVent : MonoBehaviour
    {
        public Vector2 secondsBetweenPuffs = new Vector2(4f, 8f);
        public Vector2 puffLength = new Vector2(1f, 2.2f);

        [Header("Sound (optional)")]
        public AudioSource audioSource;
        [Tooltip("Steam hiss / pressure release.")]
        public AudioClip[] hissClips;

        ParticleSystem _ps;

        IEnumerator Start()
        {
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            yield return new WaitForSeconds(Random.Range(0.5f, 3f));
            while (true)
            {
                if (audioSource && hissClips != null && hissClips.Length > 0)
                {
                    var c = hissClips[Random.Range(0, hissClips.Length)];
                    if (c) { audioSource.pitch = Random.Range(0.9f, 1.1f); audioSource.PlayOneShot(c); }
                }
                _ps.Play();
                yield return new WaitForSeconds(Random.Range(puffLength.x, puffLength.y));
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                yield return new WaitForSeconds(Random.Range(secondsBetweenPuffs.x, secondsBetweenPuffs.y));
            }
        }
    }
}
