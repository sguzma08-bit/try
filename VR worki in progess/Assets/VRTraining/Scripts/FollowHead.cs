using UnityEngine;

namespace VRTraining
{
    /// <summary>Keeps this object (the floating dust emitter) centred on the player's head.</summary>
    public class FollowHead : MonoBehaviour
    {
        public Transform head;
        void LateUpdate()
        {
            if (!head && Camera.main) head = Camera.main.transform;
            if (head) transform.position = head.position;
        }
    }
}
