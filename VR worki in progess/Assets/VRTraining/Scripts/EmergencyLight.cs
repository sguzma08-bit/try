using UnityEngine;

namespace VRTraining
{
    /// <summary>Rotating red warning beacon. Off until Activate() is called (by the scare sequence).</summary>
    public class EmergencyLight : MonoBehaviour
    {
        public Transform spinner;        // rotates
        public Light spot;
        public Renderer beacon;          // red housing that glows when on
        public float degreesPerSecond = 220f;
        public bool startOn = false;

        [Header("Sound (optional)")]
        public AudioSource audioSource;
        [Tooltip("Alarm / klaxon loop that plays while the beacon is on.")]
        public AudioClip alarmLoop;
        [Range(0, 1)] public float alarmVolume = 0.5f;

        public bool IsOn { get; private set; }
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Start() { Set(startOn); }

        public void Activate() => Set(true);
        public void Deactivate() => Set(false);

        void Set(bool on)
        {
            IsOn = on;
            if (spot) spot.enabled = on;
            if (beacon) beacon.material.SetColor(Emission, on ? new Color(4f, 0.2f, 0.1f) : Color.black);
            if (audioSource && alarmLoop)
            {
                if (on) { audioSource.clip = alarmLoop; audioSource.loop = true; audioSource.volume = alarmVolume; audioSource.Play(); }
                else audioSource.Stop();
            }
        }

        void Update()
        {
            if (IsOn && spinner) spinner.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
