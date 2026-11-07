using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// Runs the training tasks in order. When a task fires onTaskCompleted the
    /// manager waits briefly, then begins the next task and updates the prompt UI.
    /// </summary>
    public class TrainingManager : MonoBehaviour
    {
        [TextArea(2, 5)]
        public string learningObjective =
            "Prepare a healing spray by combining a Green Herb and a Red Herb, then use it to treat an injured patient.";

        public TrainingPromptUI promptUI;
        [Tooltip("Tasks in the order they must be completed.")]
        public List<TrainingTask> tasks = new List<TrainingTask>();

        public bool startOnPlay = true;
        public float delayBetweenTasks = 1.5f;

        [TextArea(2, 4)]
        public string completionTitle = "TRAINING COMPLETE";
        [TextArea(2, 4)]
        public string completionMessage =
            "You combined the herbs into a healing spray and stabilized the patient.\nWell done, medic.";

        [Header("Unity Events")]
        public UnityEvent onTrainingStarted;
        public UnityEvent onTrainingCompleted;

        [Header("Testing without a headset")]
        [Tooltip("Press N in Play mode to skip the current task.")]
        public bool enableSkipKey = true;

        public int CurrentIndex { get; private set; } = -1;
        public TrainingTask CurrentTask =>
            CurrentIndex >= 0 && CurrentIndex < tasks.Count ? tasks[CurrentIndex] : null;

        void Start()
        {
            foreach (var t in tasks) if (t) t.Init(this);
            if (startOnPlay) StartTraining();
        }

        public void StartTraining()
        {
            StopAllCoroutines();
            foreach (var t in tasks) if (t) t.Init(this);
            CurrentIndex = -1;
            onTrainingStarted?.Invoke();
            Advance();
        }

        void Advance()
        {
            CurrentIndex++;
            if (CurrentIndex >= tasks.Count)
            {
                if (promptUI) promptUI.ShowMessage(completionTitle, completionMessage);
                onTrainingCompleted?.Invoke();
                return;
            }
            var task = tasks[CurrentIndex];
            if (promptUI) promptUI.ShowTask(CurrentIndex + 1, tasks.Count, task.taskTitle, task.instructions);
            task.BeginTask();
        }

        public void NotifyTaskCompleted(TrainingTask task)
        {
            if (task != CurrentTask) return;
            if (promptUI) promptUI.ShowSuccess(task.completionMessage);
            StartCoroutine(NextAfterDelay());
        }

        IEnumerator NextAfterDelay()
        {
            yield return new WaitForSeconds(delayBetweenTasks);
            Advance();
        }

        public void SkipCurrentTask()
        {
            if (CurrentTask) CurrentTask.CompleteTask();
        }

        void Update()
        {
            if (!enableSkipKey) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.nKey.wasPressedThisFrame) SkipCurrentTask();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.N)) SkipCurrentTask();
#endif
        }
    }
}
