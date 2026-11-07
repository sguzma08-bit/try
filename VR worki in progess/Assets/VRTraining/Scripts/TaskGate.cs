using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Keeps interactables switched off until their task begins, so the user
    /// cannot get ahead of the training order (e.g. grab the Red Herb during Step 2).
    /// </summary>
    public class TaskGate : MonoBehaviour
    {
        public TrainingTask unlockWhenTaskStarts;
        [Tooltip("Grab/poke interactable components to disable until the task starts.")]
        public Behaviour[] lockedBehaviours;

        void Start()
        {
            if (!unlockWhenTaskStarts) return;
            if (unlockWhenTaskStarts.IsActive || unlockWhenTaskStarts.IsComplete) { Unlock(); return; }
            Set(false);
            unlockWhenTaskStarts.onTaskStarted.AddListener(Unlock);
        }

        public void Unlock() => Set(true);

        void Set(bool on)
        {
            if (lockedBehaviours == null) return;
            foreach (var b in lockedBehaviours) if (b) b.enabled = on;
        }
    }
}
