using Oculus.Interaction;
using UnityEngine;

namespace VRTraining
{
    /// <summary>Plays a sound when this item is picked up / let go.</summary>
    public class GrabSound : MonoBehaviour
    {
        public MonoBehaviour pointable;   // the item's Grabbable
        public AudioSource audioSource;
        [Tooltip("e.g. leaves rustling for herbs, plastic/metal clink for the spray.")]
        public AudioClip pickupSound;
        public AudioClip dropSound;

        IPointable _p;
        void Awake() { if (!pointable) pointable = GetComponent<Grabbable>(); _p = pointable as IPointable; }
        void OnEnable() { if (_p != null) _p.WhenPointerEventRaised += On; }
        void OnDisable() { if (_p != null) _p.WhenPointerEventRaised -= On; }

        void On(PointerEvent e)
        {
            if (!audioSource) return;
            if (e.Type == PointerEventType.Select && pickupSound) audioSource.PlayOneShot(pickupSound);
            if (e.Type == PointerEventType.Unselect && dropSound) audioSource.PlayOneShot(dropSound);
        }
    }
}
