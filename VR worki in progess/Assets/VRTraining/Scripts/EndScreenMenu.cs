using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRTraining
{
    /// <summary>
    /// Final screen: the world goes dark, the terminal panel moves in front of the player
    /// and shows RESTART / QUIT poke buttons.
    /// </summary>
    public class EndScreenMenu : MonoBehaviour
    {
        public TrainingPromptUI panel;
        public GameObject buttonsRoot;          // RESTART + QUIT (hidden until the end)
        public Transform head;
        [Tooltip("Distance in front of the eyes - must be within arm's reach to poke.")]
        public float distance = 0.5f;
        [Tooltip("Panel is shrunk to this width (m) so it fits in view up close.")]
        public float panelWidth = 0.6f;
        public GameObject[] hideDuringMenu;     // dust, steam, etc.
        public string title = "TRAINING COMPLETE";
        [TextArea(2, 4)] public string message = "--- TO BE CONTINUED ---\n\n<color=#FFB000>> Poke a button below.</color>";

        public bool IsShowing { get; private set; }

        void Start() { if (buttonsRoot) buttonsRoot.SetActive(false); }

        public void Show()
        {
            if (IsShowing) return;
            IsShowing = true;
            if (!head && Camera.main) head = Camera.main.transform;

            // 1) Dark void: every light off, black ambient, thick black fog
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) l.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 1.5f;
            if (hideDuringMenu != null) foreach (var g in hideDuringMenu) if (g) g.SetActive(false);

            // 2) Panel in front of the player, facing them, shrunk to arm's-reach size
            if (panel && head)
            {
                var mover = panel.GetComponent<MoveToSpot>();
                if (mover) mover.StopAllCoroutines();
                var fwd = head.forward; fwd.y = 0;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                var t = panel.transform;
                var rt = t as RectTransform;
                if (rt)
                {
                    float w = rt.rect.width * t.lossyScale.x;
                    if (w > 0.001f) t.localScale *= panelWidth / w;
                }
                t.position = head.position + fwd * distance + Vector3.down * 0.08f;
                t.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                panel.ShowMessage(title, message);
            }
            if (buttonsRoot) buttonsRoot.SetActive(true);
        }

        public void Restart()
        {
            AudioListener.volume = 1f;
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
