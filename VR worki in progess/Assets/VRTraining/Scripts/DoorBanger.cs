using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>Something pounds on the closed door: bang sounds + the door panel jolts.</summary>
    public class DoorBanger : MonoBehaviour
    {
        public Transform doorPanel;
        public AudioSource audioSource;
        [Tooltip("Drop one or more door-bang / heavy-knock sounds here. Empty = silent jolts.")]
        public AudioClip[] bangClips;
        public int bangs = 4;
        public Vector2 secondsBetween = new Vector2(0.5f, 1.1f);
        [Tooltip("How far the door jumps toward the player on each hit (m).")]
        public float joltDistance = 0.03f;
        [Tooltip("Direction pointing INTO the room (the door is hit from outside).")]
        public Vector3 roomDirection = Vector3.forward;

        public UnityEvent onBang;
        public UnityEvent onFinished;

        public void StartBanging()
        {
            StopAllCoroutines();
            StartCoroutine(Bang());
        }

        IEnumerator Bang()
        {
            for (int i = 0; i < bangs; i++)
            {
                if (audioSource && bangClips != null && bangClips.Length > 0)
                {
                    var clip = bangClips[Random.Range(0, bangClips.Length)];
                    if (clip) { audioSource.pitch = Random.Range(0.9f, 1.05f); audioSource.PlayOneShot(clip); }
                }
                onBang?.Invoke();
                if (doorPanel) yield return Jolt();
                yield return new WaitForSeconds(Random.Range(secondsBetween.x, secondsBetween.y));
            }
            onFinished?.Invoke();
        }

        IEnumerator Jolt()
        {
            Vector3 home = doorPanel.position;
            Vector3 hit = home + roomDirection.normalized * joltDistance;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.05f) { doorPanel.position = Vector3.Lerp(home, hit, t); yield return null; }
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f) { doorPanel.position = Vector3.Lerp(hit, home, t); yield return null; }
            doorPanel.position = home;
        }
    }
}
