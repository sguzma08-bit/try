using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// One step of the training scenario. Interaction events (grab, poke, trigger)
    /// call CompleteTask(); the TrainingManager then starts the next task.
    /// </summary>
    public class TrainingTask : MonoBehaviour
    {
        [Header("Prompt shown on the training panel")]
        public string taskTitle = "New Task";
        [TextArea(3, 8)] public string instructions;
        [TextArea(1, 3)] public string completionMessage = "Task complete!";

        [Header("Objects")]
        [Tooltip("Turned ON when this task begins.")]
        public GameObject[] activateOnStart;
        [Tooltip("Turned OFF when this task is completed.")]
        public GameObject[] deactivateOnComplete;

        [Header("Auto-complete if the goal is already met when the task begins")]
        public TrainingInteractionEvents completeIfAlreadySelected;
        public TrainingTriggerZone completeIfZoneSatisfied;

        [Header("Unity Events: signal the task START and END")]
        public UnityEvent onTaskStarted;
        public UnityEvent onTaskCompleted;

        public bool IsActive { get; private set; }
        public bool IsComplete { get; private set; }

        TrainingManager _manager;

        public void Init(TrainingManager manager)
        {
            _manager = manager;
            IsActive = false;
            IsComplete = false;
        }

        public void BeginTask()
        {
            IsActive = true;
            IsComplete = false;
            if (activateOnStart != null)
                foreach (var go in activateOnStart) if (go) go.SetActive(true);
            onTaskStarted?.Invoke();

            if (completeIfAlreadySelected && completeIfAlreadySelected.IsSelected) CompleteTask();
            else if (completeIfZoneSatisfied && completeIfZoneSatisfied.AllRequiredInside) CompleteTask();
        }

        /// <summary>Called by interaction Unity Events. Ignored unless this task is the active one.</summary>
        public void CompleteTask()
        {
            if (!IsActive || IsComplete) return;
            IsActive = false;
            IsComplete = true;
            if (deactivateOnComplete != null)
                foreach (var go in deactivateOnComplete) if (go) go.SetActive(false);
            onTaskCompleted?.Invoke();
            if (_manager) _manager.NotifyTaskCompleted(this);
        }
    }
}
