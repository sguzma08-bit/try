using UnityEngine;

namespace VRTraining
{
    /// <summary>Keeps a world-space label turned toward the player's head.</summary>
    public class FaceCamera : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;
            var dir = transform.position - _cam.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
