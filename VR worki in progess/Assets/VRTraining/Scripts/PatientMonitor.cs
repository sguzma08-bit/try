using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Heart-monitor beep beside the patient: slow before treatment, fast and irregular
    /// while the patient is critical, steady once stabilized.
    /// </summary>
    public class PatientMonitor : MonoBehaviour
    {
        public TrainingTask treatTask;
        public AudioSource audioSource;
        [Tooltip("A single short ECG beep.")]
        public AudioClip beep;
        public float idleInterval = 1.3f;
        public Vector2 criticalInterval = new Vector2(0.35f, 0.9f);
        public float stableInterval = 0.9f;

        float _next;

        void Update()
        {
            if (!audioSource || !beep || Time.time < _next) return;
            float wait = idleInterval;
            if (treatTask && treatTask.IsComplete) wait = stableInterval;
            else if (treatTask && treatTask.IsActive) wait = Random.Range(criticalInterval.x, criticalInterval.y);
            audioSource.pitch = treatTask && treatTask.IsActive ? Random.Range(0.97f, 1.05f) : 1f;
            audioSource.PlayOneShot(beep);
            _next = Time.time + wait;
        }
    }
}
