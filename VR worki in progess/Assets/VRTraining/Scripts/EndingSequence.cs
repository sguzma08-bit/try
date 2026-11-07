using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VRTraining
{
    /// <summary>
    /// The ending: after the banging stops, the door jerks open a crack with red light
    /// spilling under it, then the view fades to black and the end title appears.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [Header("Door crack")]
        public Transform doorPanel;
        [Tooltip("Direction the door slides to open (up).")]
        public Vector3 openDirection = Vector3.up;
        [Tooltip("Direction pointing INTO the room (used to put the red light outside).")]
        public Vector3 roomDirection = Vector3.forward;
        public float crackDistance = 0.35f;
        public float crackSeconds = 2.5f;
        public float delayBeforeCrack = 1.5f;

        [Header("Fade to black")]
        public Transform head;
        public float delayBeforeFade = 2f;
        public float fadeSeconds = 1.5f;
        public string title = "TRAINING COMPLETE";
        public string subtitle = "--- TO BE CONTINUED ---";

        [Header("Sound (optional)")]
        public AudioSource audioSource;
        [Tooltip("Door straining / metal scraping as it is forced open.")]
        public AudioClip doorForcedSound;
        [Tooltip("Growl or groan from behind the door.")]
        public AudioClip growlSound;
        [Tooltip("Final hit when the screen cuts to black.")]
        public AudioClip endStinger;
        [Tooltip("Turn the rest of the game's sound down when the title shows.")]
        public bool fadeOutOtherAudio = true;

        [Header("End menu")]
        [Tooltip("Shows RESTART / QUIT after the title. Leave empty to stay on the title.")]
        public EndScreenMenu menu;
        public float titleHoldSeconds = 3f;

        [Header("Unity Events")]
        public UnityEvent onDoorCracked;
        public UnityEvent onEndScreen;

        bool _played;
        Light _redLight;

        public void Play()
        {
            if (_played) return;
            _played = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(delayBeforeCrack);
            Debug.Log("[VR Training] Ending: door cracks open");

            // Red light from the other side, shining in under the door.
            if (doorPanel)
            {
                var go = new GameObject("Ending_RedLight");
                go.transform.position = doorPanel.position - roomDirection.normalized * 0.8f + Vector3.down * 0.6f;
                _redLight = go.AddComponent<Light>();
                _redLight.type = LightType.Point;
                _redLight.color = new Color(1f, 0.1f, 0.05f);
                _redLight.range = 5f;
                _redLight.intensity = 0f;
            }
            if (audioSource && doorForcedSound) audioSource.PlayOneShot(doorForcedSound);

            // Jerky forced opening: 3 shoves, each with a pause.
            if (doorPanel)
            {
                Vector3 start = doorPanel.position, dir = openDirection.normalized;
                for (int i = 1; i <= 3; i++)
                {
                    Vector3 a = doorPanel.position, b = start + dir * crackDistance * i / 3f;
                    float seg = crackSeconds / 6f;
                    for (float t = 0; t < 1f; t += Time.deltaTime / seg)
                    {
                        doorPanel.position = Vector3.Lerp(a, b, t) + Random.insideUnitSphere * 0.004f;
                        if (_redLight) _redLight.intensity = Mathf.Lerp(_redLight.intensity, 3f * i / 3f, t);
                        yield return null;
                    }
                    doorPanel.position = b;
                    yield return new WaitForSeconds(seg);
                }
            }
            onDoorCracked?.Invoke();
            if (audioSource && growlSound) audioSource.PlayOneShot(growlSound);

            yield return new WaitForSeconds(delayBeforeFade);
            Debug.Log("[VR Training] Ending: fade to black + title");
            yield return EndScreen();
        }

        IEnumerator EndScreen()
        {
            if (!head && Camera.main) head = Camera.main.transform;
            if (!head) yield break;

            // A canvas stuck just in front of the eyes: black backdrop + title.
            var cgo = new GameObject("Ending_Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 999;
            var rt = (RectTransform)cgo.transform;
            rt.SetParent(head, false);
            rt.localPosition = new Vector3(0, 0, 0.3f);
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(1000, 1000);
            rt.localScale = Vector3.one * 0.0015f;      // 1.5 m wide at 0.3 m = fills the view
            var group = cgo.GetComponent<CanvasGroup>();
            group.alpha = 0;

            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(rt, false);
            var bgrt = (RectTransform)bg.transform;
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = Color.black;

            var tgo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.transform.SetParent(rt, false);
            var trt = (RectTransform)tgo.transform;
            trt.sizeDelta = new Vector2(220, 80);
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 14;
            tmp.color = new Color(1f, 0.85f, 0.85f, 0f);
            tmp.text = $"<color=#FF3B3B><b>{title}</b></color>\n<size=60%><color=#BBBBBB>{subtitle}</color></size>";

            float startVolume = AudioListener.volume;
            for (float t = 0; t < 1f; t += Time.deltaTime / fadeSeconds)
            {
                group.alpha = t;
                if (fadeOutOtherAudio) AudioListener.volume = Mathf.Lerp(startVolume, 0.25f, t);
                yield return null;
            }
            group.alpha = 1;
            if (_redLight) _redLight.enabled = false;
            if (fadeOutOtherAudio) AudioListener.volume = startVolume;   // let the stinger play at full volume
            if (fadeOutOtherAudio) foreach (var a in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude)) if (a != audioSource) a.Stop();
            if (audioSource && endStinger) audioSource.PlayOneShot(endStinger);
            onEndScreen?.Invoke();

            yield return new WaitForSeconds(0.8f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.5f)
            {
                tmp.color = new Color(1f, 0.85f, 0.85f, t);
                yield return null;
            }
            tmp.color = new Color(1f, 0.85f, 0.85f, 1f);

            // Hand over to the end menu: switch the world to dark, then lift the black curtain.
            if (!menu) yield break;
            yield return new WaitForSeconds(titleHoldSeconds);
            menu.Show();
            for (float t = 0; t < 1f; t += Time.deltaTime / 1f)
            {
                group.alpha = 1f - t;
                yield return null;
            }
            Destroy(cgo);
        }
    }
}
