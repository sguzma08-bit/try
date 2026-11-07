using UnityEngine;
using UnityEngine.UI;

namespace VRTraining
{
    /// <summary>
    /// Old-monitor look for a world-space canvas: scanlines, a rolling refresh bar,
    /// a dark vignette and a subtle flicker. Overlays are created at runtime and
    /// never block poke/ray input.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class TerminalCRTEffect : MonoBehaviour
    {
        [Header("Look")]
        public Color scanlineColor = new Color(0f, 0f, 0f, 0.35f);
        [Tooltip("Scanlines across the panel height.")]
        public float scanlineCount = 140f;
        public float scanlineScrollSpeed = 0.15f;
        public Color refreshBarColor = new Color(1f, 0.25f, 0.25f, 0.07f);
        public float refreshBarSeconds = 4f;
        public float vignetteStrength = 0.65f;
        [Range(0f, 0.2f)] public float flickerAmount = 0.05f;

        [Header("Optional background tint (Images whose name contains 'bg' or 'Background')")]
        public bool tintBackgrounds = true;
        public Color backgroundTint = new Color(0.06f, 0.015f, 0.02f, 0.94f);

        RawImage _scan, _vignette;
        RectTransform _bar;
        CanvasGroup _group;
        float _barT;

        void Start()
        {
            var root = (RectTransform)transform;
            if (tintBackgrounds)
                foreach (var img in GetComponentsInChildren<Image>(true))
                {
                    var n = img.name.ToLowerInvariant();
                    if (n == "bg" || n.Contains("background")) img.color = backgroundTint;
                }

            _group = GetComponent<CanvasGroup>();
            if (!_group) _group = gameObject.AddComponent<CanvasGroup>();

            // Scanlines: tiny 1x4 texture tiled vertically.
            var scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            scanTex.SetPixels(new[] { scanlineColor, scanlineColor, Color.clear, Color.clear });
            scanTex.Apply();
            _scan = Overlay("CRT_Scanlines", root).gameObject.AddComponent<RawImage>();
            _scan.texture = scanTex;
            _scan.uvRect = new Rect(0, 0, 1, scanlineCount / 2f);
            _scan.raycastTarget = false;

            // Rolling refresh bar.
            _bar = Overlay("CRT_RefreshBar", root);
            _bar.anchorMin = new Vector2(0, 0); _bar.anchorMax = new Vector2(1, 0.12f);
            var barImg = _bar.gameObject.AddComponent<Image>();
            barImg.color = refreshBarColor;
            barImg.raycastTarget = false;

            // Vignette: radial alpha texture.
            const int S = 64;
            var vTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) - 0.6f) / 0.4f);
                    vTex.SetPixel(x, y, new Color(0, 0, 0, d * d * vignetteStrength));
                }
            vTex.Apply();
            _vignette = Overlay("CRT_Vignette", root).gameObject.AddComponent<RawImage>();
            _vignette.texture = vTex;
            _vignette.raycastTarget = false;
        }

        RectTransform Overlay(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.anchoredPosition3D = new Vector3(0, 0, -2f);   // in front of text, behind nothing
            rt.SetAsLastSibling();
            return rt;
        }

        void Update()
        {
            if (_scan)
            {
                var r = _scan.uvRect;
                r.y = (r.y - Time.deltaTime * scanlineScrollSpeed) % 1f;
                _scan.uvRect = r;
            }
            if (_bar)
            {
                _barT = (_barT + Time.deltaTime / Mathf.Max(0.1f, refreshBarSeconds)) % 1f;
                float y = Mathf.Lerp(1.1f, -0.12f, _barT);
                _bar.anchorMin = new Vector2(0, y);
                _bar.anchorMax = new Vector2(1, y + 0.12f);
            }
            if (_group && flickerAmount > 0)
                _group.alpha = 1f - flickerAmount * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
        }
    }
}
