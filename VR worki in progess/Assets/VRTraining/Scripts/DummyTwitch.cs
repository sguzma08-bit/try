using System.Collections;
using UnityEngine;

namespace VRTraining
{
    /// <summary>Makes the dummy's head (and optionally an arm) jerk violently for a moment.</summary>
    public class DummyTwitch : MonoBehaviour
    {
        public Transform headBone;
        public Transform armBone;
        public float seconds = 0.6f;
        [Tooltip("If set, this bone is used as the arm (found by name). The hanging right arm faces the player.")]
        public string armBoneName = "B-upperArm.R";
        public float armAngle = 35f;
        public float headAngle = 35f;
        public AudioSource audioSource;
        [Tooltip("Optional: a bone crack / sting sound.")]
        public AudioClip twitchSound;

        Quaternion _headRest, _armRest;
        float _until;

        void Start()
        {
            if (!string.IsNullOrEmpty(armBoneName))
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == armBoneName) { armBone = t; break; }
            if (headBone) _headRest = headBone.localRotation;
            if (armBone) _armRest = armBone.localRotation;
        }

        public void Twitch() => Twitch(seconds);

        public void Twitch(float duration)
        {
            _until = Time.time + duration;
            if (audioSource && twitchSound) audioSource.PlayOneShot(twitchSound);
        }

        void LateUpdate()
        {
            bool active = Time.time < _until;
            if (headBone)
                headBone.localRotation = active
                    ? _headRest * Quaternion.Euler(Random.Range(-headAngle, headAngle), Random.Range(-headAngle, headAngle), Random.Range(-headAngle * 0.5f, headAngle * 0.5f))
                    : Quaternion.Slerp(headBone.localRotation, _headRest, Time.deltaTime * 10f);
            if (armBone)
                armBone.localRotation = active
                    ? _armRest * Quaternion.Euler(Random.Range(-armAngle, armAngle), 0, Random.Range(-armAngle, armAngle))
                    : Quaternion.Slerp(armBone.localRotation, _armRest, Time.deltaTime * 10f);
        }
    }
}
