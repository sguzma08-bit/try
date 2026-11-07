using System.Collections;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// UI / story sounds tied to the training steps. All slots optional.
    /// Hooks itself onto every task in the TrainingManager when the game starts.
    /// </summary>
    public class TrainingSoundHooks : MonoBehaviour
    {
        public TrainingManager manager;
        public AudioSource audioSource;

        [Tooltip("PA / intercom announcement at the start (e.g. 'Welcome, field medic').")]
        public AudioClip introAnnouncement;
        public float introDelay = 1.5f;
        [Tooltip("Terminal blip when a new step appears on the panel.")]
        public AudioClip stepStartSound;
        [Tooltip("Confirm chime when a step is verified.")]
        public AudioClip stepCompleteSound;
        [Tooltip("Liquid / squish when the herbs are combined.")]
        public AudioClip mixSound;
        public TrainingTask mixTask;
        [Tooltip("Relief / success sting when the patient is stabilized.")]
        public AudioClip patientStabilizedSound;
        public TrainingTask patientTask;

        IEnumerator Start()
        {
            if (manager)
                foreach (var t in manager.tasks)
                {
                    if (!t) continue;
                    t.onTaskStarted.AddListener(() => Play(stepStartSound));
                    t.onTaskCompleted.AddListener(() => Play(stepCompleteSound));
                }
            if (mixTask) mixTask.onTaskCompleted.AddListener(() => Play(mixSound));
            if (patientTask) patientTask.onTaskCompleted.AddListener(() => Play(patientStabilizedSound));
            yield return new WaitForSeconds(introDelay);
            Play(introAnnouncement);
        }

        public void Play(AudioClip c) { if (audioSource && c) audioSource.PlayOneShot(c); }
    }
}
