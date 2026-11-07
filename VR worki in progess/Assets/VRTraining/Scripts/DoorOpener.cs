using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>Slides a door panel open (default: straight up) and plays its sound.</summary>
    public class DoorOpener : MonoBehaviour
    {
        public Transform doorPanel;
        [Tooltip("World-space direction the panel moves when opening.")]
        public Vector3 openDirection = Vector3.up;
        public float openDistance = 2.2f;
        public float openSeconds = 1.5f;
        public AudioSource doorSound;
        [Tooltip("Animators that would fight the slide (disabled when opening).")]
        public Animator[] disableAnimators;

        [Tooltip("Also open when this task completes (defaults to a TrainingTask on this object).")]
        public TrainingTask openWhenTaskCompletes;

        public UnityEvent onOpened;

        void Awake()
        {
            if (!openWhenTaskCompletes) openWhenTaskCompletes = GetComponent<TrainingTask>();
            if (openWhenTaskCompletes) openWhenTaskCompletes.onTaskCompleted.AddListener(Open);
        }

        public bool IsOpen { get; private set; }
        /// <summary>True once the door has been opened at least once (stays true after it closes again).</summary>
        public bool WasOpened { get; private set; }
        public UnityEvent onClosed;
        Vector3 _closedPos;
        bool _hasClosedPos;

        public void Open()
        {
            if (IsOpen || !doorPanel) return;
            IsOpen = true;
            WasOpened = true;
            if (!_hasClosedPos) { _closedPos = doorPanel.position; _hasClosedPos = true; }
            if (disableAnimators != null) foreach (var a in disableAnimators) if (a) a.enabled = false;
            if (doorSound) doorSound.Play();
            StopAllCoroutines();
            StartCoroutine(Slide(_closedPos + openDirection.normalized * openDistance, true));
        }

        /// <summary>Slides the door back down to where it started.</summary>
        public void Close()
        {
            if (!IsOpen || !doorPanel || !_hasClosedPos) return;
            IsOpen = false;
            if (doorSound) doorSound.Play();
            StopAllCoroutines();
            StartCoroutine(Slide(_closedPos, false));
        }

        IEnumerator Slide(Vector3 end, bool opening)
        {
            Vector3 start = doorPanel.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, openSeconds))
            {
                doorPanel.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            doorPanel.position = end;
            if (opening) onOpened?.Invoke(); else onClosed?.Invoke();
        }
    }
}
