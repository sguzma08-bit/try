using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// While the item is held, a small terminal-style card floats above it with the item's
    /// name, a description and a hint. Edit the three text fields in the Inspector.
    /// </summary>
    public class ItemInspectLabel : MonoBehaviour
    {
        public string itemName = "ITEM";
        [TextArea(2, 4)] public string description = "Description.";
        [TextArea(1, 3)] public string hint = "";
        [Tooltip("The item's Grabbable (found automatically if empty).")]
        public MonoBehaviour pointable;
        public float heightAbove = 0.18f;
        public float hideDelay = 0.4f;

        IPointable _p;
        int _held;
        float _hideAt = -1;
        Transform _card;
        Camera _cam;

        void Awake()
        {
            if (!pointable) pointable = GetComponent<Grabbable>();
            _p = pointable as IPointable;
        }

        void OnEnable() { if (_p != null) _p.WhenPointerEventRaised += OnPointer; }
        void OnDisable() { if (_p != null) _p.WhenPointerEventRaised -= OnPointer; _held = 0; if (_card) _card.gameObject.SetActive(false); }

        void OnPointer(PointerEvent e)
        {
            if (e.Type == PointerEventType.Select) { _held++; Show(); }
            else if (e.Type == PointerEventType.Unselect || e.Type == PointerEventType.Cancel)
            {
                _held = Mathf.Max(0, _held - 1);
                if (_held == 0) _hideAt = Time.time + hideDelay;
            }
        }

        void Show()
        {
            _hideAt = -1;
            if (!_card) Build();
            _card.gameObject.SetActive(true);
        }

        void Build()
        {
            _card = new GameObject("Inspect_Card").transform;
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "Card_Background";
            bg.transform.SetParent(_card, false);
            bg.transform.localPosition = new Vector3(0, 0, 0.002f);
            bg.transform.localScale = new Vector3(0.36f, 0.16f, 1f);
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = new Color(0.05f, 0.01f, 0.01f, 0.88f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = m;

            var txtGO = new GameObject("Card_Text");
            txtGO.transform.SetParent(_card, false);
            var tmp = txtGO.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(0.33f, 0.14f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = 0.4f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.text = $"<color=#FF3B3B><b>{itemName}</b></color>\n<size=70%><color=#DDDDDD>{description}</color>" +
                       (string.IsNullOrEmpty(hint) ? "" : $"\n<color=#FFB000>> {hint}</color>") + "</size>";
        }

        void LateUpdate()
        {
            if (_hideAt > 0 && Time.time >= _hideAt) { _hideAt = -1; if (_card) _card.gameObject.SetActive(false); }
            if (!_card || !_card.gameObject.activeSelf) return;
            if (!_cam) _cam = Camera.main;
            _card.position = transform.position + Vector3.up * heightAbove;
            if (_cam)
            {
                var dir = _card.position - _cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f) _card.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
