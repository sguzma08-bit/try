using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Stops the player from walking, sliding or teleporting through the locked door.
    /// While the door is closed, if the player's head enters the doorway or ends up on
    /// the room side, the rig is moved back to the last allowed position.
    /// </summary>
    public class DoorBlocker : MonoBehaviour
    {
        public DoorOpener door;
        public Transform rig;     // camera rig root (what locomotion moves)
        public Transform head;    // center eye camera
        [Tooltip("Horizontal direction pointing INTO the locked room.")]
        public Vector3 roomDirection = Vector3.forward;
        [Tooltip("Point on the outside face of the door.")]
        public Vector3 doorPlanePoint;
        [Tooltip("How far either side of the door the room extends (m).")]
        public float roomHalfWidth = 6f;
        public float roomDepth = 10f;
        [Tooltip("Keep the head this far outside the door face.")]
        public float buffer = 0.25f;

        Vector3 _lastSafeRigPos;
        bool _hasSafe;

        void LateUpdate()
        {
            if (!rig || !head || (door && (door.IsOpen || door.WasOpened))) return;   // once the door has opened, the player is free (door may close behind them)
            if (Blocked(head.position))
            {
                if (_hasSafe)
                {
                    var p = rig.position;
                    rig.position = new Vector3(_lastSafeRigPos.x, p.y, _lastSafeRigPos.z);
                }
                else
                {
                    // push straight back out
                    float d = Vector3.Dot(head.position - doorPlanePoint, roomDirection) + buffer;
                    rig.position -= roomDirection * d;
                }
            }
            else
            {
                _lastSafeRigPos = rig.position;
                _hasSafe = true;
            }
        }

        bool Blocked(Vector3 h)
        {
            var dir = roomDirection; dir.y = 0; dir.Normalize();
            var rel = h - doorPlanePoint; rel.y = 0;
            float depth = Vector3.Dot(rel, dir);
            float side = Vector3.Dot(rel, Vector3.Cross(Vector3.up, dir));
            return depth > -buffer && depth < roomDepth && Mathf.Abs(side) < roomHalfWidth;
        }
    }
}
