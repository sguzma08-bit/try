using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>Hides the ingredient herbs and reveals the result (the Healing Spray).</summary>
    public class HerbCombiner : MonoBehaviour
    {
        public GameObject[] ingredients;
        public GameObject result;
        public Transform spawnPoint;
        public TrainingTriggerZone zone;
        public ParticleSystem combineEffect;
        public AudioSource audioSource;
        public AudioClip combineSound;

        public UnityEvent onCombined;

        bool _done;

        void Start()
        {
            if (result && !_done) result.SetActive(false);
        }

        public void Combine()
        {
            if (_done) return;
            _done = true;

            foreach (var ing in ingredients) if (ing) ing.SetActive(false);
            if (zone) zone.ResetZone();

            if (result)
            {
                if (spawnPoint) result.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                result.SetActive(true);
                var rb = result.GetComponent<Rigidbody>();
                if (rb && !rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                StartCoroutine(PopIn(result.transform));
            }

            if (combineEffect) combineEffect.Play();
            if (audioSource && combineSound) audioSource.PlayOneShot(combineSound);
            onCombined?.Invoke();
        }

        IEnumerator PopIn(Transform t)
        {
            var target = t.localScale;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.35f)
            {
                t.localScale = target * Mathf.SmoothStep(0.2f, 1f, k);
                yield return null;
            }
            t.localScale = target;
        }
    }
}
