using UnityEngine;

namespace VRTraining
{
    /// <summary>Plays a footstep each time the player's head travels a stride along the floor.</summary>
    public class Footsteps : MonoBehaviour
    {
        public Transform head;
        public AudioSource audioSource;
        [Tooltip("2-6 footstep sounds on a metal floor.")]
        public AudioClip[] stepClips;
        public float strideLength = 0.75f;
        [Range(0, 1)] public float volume = 0.5f;

        Vector3 _last; float _travel;

        void Start() { if (head) _last = head.position; }

        void Update()
        {
            if (!head || !audioSource || stepClips == null || stepClips.Length == 0) return;
            var p = head.position; var d = p - _last; d.y = 0; _last = p;
            if (d.magnitude > 0.5f) return;   // teleport / snap, not a step
            _travel += d.magnitude;
            if (_travel < strideLength) return;
            _travel = 0;
            var c = stepClips[Random.Range(0, stepClips.Length)];
            if (c) { audioSource.pitch = Random.Range(0.9f, 1.1f); audioSource.PlayOneShot(c, volume); }
        }
    }
}
