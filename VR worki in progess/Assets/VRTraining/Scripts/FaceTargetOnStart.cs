using System.Collections;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// When the scene starts, turns the player rig (around the head) so the user is
    /// looking at the start screen, whatever direction the headset was facing.
    /// </summary>
    public class FaceTargetOnStart : MonoBehaviour
    {
        public Transform rig;
        public Transform head;
        public Transform target;
        [Tooltip("Wait for headset tracking to start before turning.")]
        public float delay = 0.5f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(delay);
            if (!rig || !head || !target) yield break;
            var want = target.position - head.position; want.y = 0;
            var have = head.forward; have.y = 0;
            if (want.sqrMagnitude < 0.001f || have.sqrMagnitude < 0.001f) yield break;
            float delta = Vector3.SignedAngle(have, want, Vector3.up);
            rig.RotateAround(head.position, Vector3.up, delta);
        }
    }
}
