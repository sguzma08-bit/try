using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// After the patient is stabilized: a pause, lights cut out while the dummy twitches,
    /// lights come back, then something bangs on the door.
    /// </summary>
    public class ScareSequence : MonoBehaviour
    {
        public float delayBeforeLightsOut = 2f;
        public float lightsOutSeconds = 0.6f;
        public float delayBeforeBanging = 3f;

        public Light[] lights;
        public PlayerViewFX viewFX;
        public DummyTwitch twitch;
        public DoorBanger banger;
        [Tooltip("Closes this door before the banging if it is still open.")]
        public DoorOpener door;
        public AudioSource audioSource;
        [Tooltip("Optional: electrical clunk / power-down sound for the lights going out.")]
        public AudioClip lightsOutSound;

        [Header("Unity Events")]
        public UnityEvent onLightsOut;
        public UnityEvent onLightsBack;

        bool _played;

        public void Play()
        {
            if (_played) return;
            _played = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(delayBeforeLightsOut);

            Debug.Log("[VR Training] Scare: lights out + dummy twitch");
            if (audioSource && lightsOutSound) audioSource.PlayOneShot(lightsOutSound);
            SetLights(false);
            if (viewFX) viewFX.Blackout(lightsOutSeconds);
            if (twitch) twitch.Twitch();
            onLightsOut?.Invoke();
            yield return new WaitForSeconds(lightsOutSeconds);

            SetLights(true);
            Debug.Log("[VR Training] Scare: lights back on");
            if (twitch) twitch.Twitch(1.2f);   // second, longer twitch the player can actually see
            onLightsBack?.Invoke();
            yield return new WaitForSeconds(delayBeforeBanging);

            if (door && door.IsOpen) { door.Close(); yield return new WaitForSeconds(door.openSeconds + 0.3f); }
            Debug.Log("[VR Training] Scare: banging on the door");
            if (banger) banger.StartBanging();
        }

        void SetLights(bool on)
        {
            if (lights == null) return;
            foreach (var l in lights) if (l) l.enabled = on;
        }
    }
}
