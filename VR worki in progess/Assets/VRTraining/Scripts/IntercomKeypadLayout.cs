using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRTraining
{
    /// <summary>
    /// Lines the invisible poke buttons up with the physical keys on the intercom model,
    /// so the player pokes the real wall keypad. Edit the fractions and use the
    /// component menu "Apply Layout" (or Tools > VR Training) to re-align.
    /// Fractions are of the intercom face as seen by the player: x 0=left 1=right, y 0=bottom 1=top.
    /// </summary>
    public class IntercomKeypadLayout : MonoBehaviour
    {
        public Transform intercom;
        public PokeKeypad keypad;
        [Tooltip("Direction pointing INTO the wall (away from the player).")]
        public Vector3 intoWall = Vector3.forward;

        [Header("Key grid on the intercom face (fractions)")]
        public Rect keyGrid = new Rect(0.173f, 0.101f, 0.410f, 0.292f);   // x, y, width, height
        [Range(0.3f, 1f)] public float keyFill = 0.82f;
        [Header("Code readout on the intercom screen (fractions)")]
        public Rect displayArea = new Rect(0.16f, 0.68f, 0.68f, 0.20f);

        [Header("Debug")]
        public bool showKeyOutlines = false;

        // rows top -> bottom
        static readonly string[,] Keys = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "CLR", "0", "" } };

#if UNITY_EDITOR
        [ContextMenu("Apply Layout")]
        public void Apply()
        {
            if (!intercom || !keypad) return;
            var canvasRT = (RectTransform)keypad.transform;
            UnityEditor.Undo.RecordObject(canvasRT, "Keypad layout");

            // Intercom face in world space
            var rs = intercom.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var fwd = intoWall; fwd.y = 0; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);          // player's right when facing the wall
            float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), 0, Mathf.Abs(fwd.z))));
            float faceW = 2f * Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z))));
            float faceH = b.size.y;
            Vector3 faceCenter = b.center - fwd * (depth + 0.008f);
            Vector3 faceBL = faceCenter - right * faceW * 0.5f - Vector3.up * faceH * 0.5f;

            // Canvas covers the whole intercom face
            float w = canvasRT.rect.width, h = canvasRT.rect.height;
            float s = Mathf.Max(faceW / w, faceH / h) * 1.02f;
            canvasRT.SetParent(intercom, true);
            canvasRT.position = faceCenter;
            canvasRT.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            canvasRT.localScale = Vector3.one;
            var L = canvasRT.lossyScale;   // includes the intercom's rotation/scale
            canvasRT.localScale = new Vector3(s / Mathf.Abs(L.x), s / Mathf.Abs(L.y), s / Mathf.Abs(L.z));
            Debug.Log($"[VR Training] intercom lossy {intercom.lossyScale}, rot {intercom.rotation.eulerAngles}; canvas L(one) {L}, s {s}, final lossy {canvasRT.lossyScale}, local {canvasRT.localScale}");

            // Hide the old overlay pad visuals
            foreach (var img in canvasRT.GetComponentsInChildren<Image>(true))
                if (!img.GetComponent<Button>() && img.name != "Display_Backing") { UnityEditor.Undo.RecordObject(img, "hide"); img.enabled = false; }
            var crt = canvasRT.GetComponent<TerminalCRTEffect>();
            if (crt) UnityEditor.Undo.DestroyObjectImmediate(crt);

            Vector2 Local(float fx, float fy)
            {
                var wp = faceBL + right * (fx * faceW) + Vector3.up * (fy * faceH);
                var lp = canvasRT.InverseTransformPoint(wp);
                return new Vector2(lp.x, lp.y);
            }

            float cellW = keyGrid.width / 3f, cellH = keyGrid.height / 4f;
            Vector2 keySize = new Vector2(cellW * faceW * keyFill / s, cellH * faceH * keyFill / s);
            foreach (var btn in canvasRT.GetComponentsInChildren<Button>(true))
            {
                string label = btn.name.Replace("Key_", "");
                if (label == "Clear") label = "CLR";
                int row = -1, col = -1;
                for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++) if (Keys[r, c] == label) { row = r; col = c; }
                if (row < 0) { btn.gameObject.SetActive(false); continue; }
                var rt = (RectTransform)btn.transform;
                UnityEditor.Undo.RecordObject(rt, "key");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float fx = keyGrid.x + (col + 0.5f) * cellW;
                float fy = keyGrid.y + keyGrid.height - (row + 0.5f) * cellH;
                rt.anchoredPosition = Local(fx, fy);
                rt.sizeDelta = keySize;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                var p = rt.localPosition; rt.localPosition = new Vector3(p.x, p.y, -2f);

                var img = btn.GetComponent<Image>();
                UnityEditor.Undo.RecordObject(img, "key");
                img.enabled = true;
                img.color = Color.white;
                img.raycastTarget = true;
                var cb = btn.colors;
                cb.normalColor = showKeyOutlines ? new Color(1f, 0f, 0f, 0.45f) : new Color(1f, 1f, 1f, 0f);
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
                cb.selectedColor = cb.normalColor;
                cb.pressedColor = new Color(0.3f, 1f, 0.4f, 0.55f);
                UnityEditor.Undo.RecordObject(btn, "key");
                btn.colors = cb;
                foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true)) t.enabled = showKeyOutlines;
            }

            // Code readout on the intercom screen
            if (keypad.display)
            {
                var drt = keypad.display.rectTransform;
                UnityEditor.Undo.RecordObject(drt, "display");
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = Local(displayArea.center.x, displayArea.center.y);
                drt.sizeDelta = new Vector2(displayArea.width * faceW / s, displayArea.height * faceH / s);
                var p = drt.localPosition; drt.localPosition = new Vector3(p.x, p.y, -2f);
                keypad.display.alignment = TextAlignmentOptions.Center;
                keypad.display.enableAutoSizing = true;
                keypad.display.fontSizeMin = 1; keypad.display.fontSizeMax = 300;
                keypad.display.enabled = true;
                keypad.display.textWrappingMode = TextWrappingModes.NoWrap;
                keypad.display.overflowMode = TextOverflowModes.Overflow;
                keypad.display.richText = true;
                keypad.display.lineSpacing = -10f;

                // Dark screen backing behind the readout (lab terminal look, easier to read)
                var backing = canvasRT.Find("Display_Backing") as RectTransform;
                if (!backing)
                {
                    var go = new GameObject("Display_Backing", typeof(RectTransform), typeof(Image));
                    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "backing");
                    backing = (RectTransform)go.transform;
                    backing.SetParent(canvasRT, false);
                }
                var bimg = backing.GetComponent<Image>();
                bimg.enabled = true;
                bimg.color = new Color(0.03f, 0.01f, 0.01f, 0.92f);
                bimg.raycastTarget = false;
                backing.anchorMin = backing.anchorMax = new Vector2(0.5f, 0.5f);
                backing.pivot = new Vector2(0.5f, 0.5f);
                backing.anchoredPosition = drt.anchoredPosition;
                backing.sizeDelta = drt.sizeDelta * 1.08f;
                backing.localRotation = Quaternion.identity;
                backing.localScale = Vector3.one;
                var bp = backing.localPosition; backing.localPosition = new Vector3(bp.x, bp.y, -1f);
                backing.SetSiblingIndex(drt.GetSiblingIndex());
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[VR Training] Intercom keypad layout applied (face {faceW:0.00} x {faceH:0.00} m, outlines {(showKeyOutlines ? "ON" : "off")}).");
        }
#endif
    }
}
