using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// Closes the door behind the player once they have walked far enough into the room.
    /// </summary>
    public class DoorAutoClose : MonoBehaviour
    {
        public DoorOpener door;
        public Transform head;
        [Tooltip("Horizontal direction pointing INTO the room.")]
        public Vector3 roomDirection = Vector3.forward;
        [Tooltip("A point on the door's face.")]
        public Vector3 doorPlanePoint;
        [Tooltip("How far past the door (m) the player must be before it closes.")]
        public float closeWhenInside = 1.5f;
        public float delay = 0.5f;

        [Header("Unity Events")]
        public UnityEvent onClosedBehindPlayer;

        bool _done;
        float _insideSince = -1f;

        void Update()
        {
            if (_done || !door || !head || !door.IsOpen) return;
            var dir = roomDirection; dir.y = 0; dir.Normalize();
            var rel = head.position - doorPlanePoint; rel.y = 0;
            if (Vector3.Dot(rel, dir) > closeWhenInside)
            {
                if (_insideSince < 0) _insideSince = Time.time;
                if (Time.time - _insideSince >= delay)
                {
                    _done = true;
                    door.Close();
                    onClosedBehindPlayer?.Invoke();
                }
            }
            else _insideSince = -1f;
        }
    }
}
