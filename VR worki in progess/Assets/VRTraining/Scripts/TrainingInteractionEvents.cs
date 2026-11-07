using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// Turns Meta Interaction SDK pointer events into Inspector Unity Events.
    /// Point it at a Grabbable (GRAB events) or a PokeInteractable (POKE events).
    ///   Hover    -> hand/controller comes within range
    ///   Select   -> object grabbed / button poked
    ///   Deselect -> object released / finger lifted
    ///   Exit     -> hand/controller leaves range (unhover)
    /// </summary>
    public class TrainingInteractionEvents : MonoBehaviour
    {
        public enum InteractionType { Grab, Poke }

        public InteractionType interactionType = InteractionType.Grab;
        [Tooltip("A Grabbable, GrabInteractable, HandGrabInteractable or PokeInteractable.")]
        public MonoBehaviour pointable;
        [Tooltip("Optional: only fire these events while this task is active.")]
        public TrainingTask onlyDuringTask;

        [Header("Unity Events")]
        public UnityEvent onHover;
        public UnityEvent onSelect;
        public UnityEvent onDeselect;
        public UnityEvent onExit;

        public bool IsHovered => _hover > 0;
        public bool IsSelected => _select > 0;

        IPointable _p;
        int _hover, _select;

        void Awake()
        {
            _p = pointable as IPointable;
            if (_p == null)
                Debug.LogWarning($"[{name}] 'pointable' is not set to a Meta Interaction SDK interactable/grabbable.", this);
        }

        void OnEnable() { if (_p != null) _p.WhenPointerEventRaised += Handle; }

        void OnDisable()
        {
            if (_p != null) _p.WhenPointerEventRaised -= Handle;
            _hover = _select = 0;
        }

        bool Gate => onlyDuringTask == null || onlyDuringTask.IsActive;

        void Handle(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    _hover++;
                    if (Gate) onHover?.Invoke();
                    break;
                case PointerEventType.Unhover:
                    _hover = Mathf.Max(0, _hover - 1);
                    if (Gate) onExit?.Invoke();
                    break;
                case PointerEventType.Select:
                    _select++;
                    if (Gate) onSelect?.Invoke();
                    break;
                case PointerEventType.Unselect:
                    _select = Mathf.Max(0, _select - 1);
                    if (Gate) onDeselect?.Invoke();
                    break;
                case PointerEventType.Cancel:
                    _select = Mathf.Max(0, _select - 1);
                    _hover = Mathf.Max(0, _hover - 1);
                    break;
            }
        }
    }
}
