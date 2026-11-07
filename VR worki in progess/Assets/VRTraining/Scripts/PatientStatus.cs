using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>Shows the dummy's condition and reacts when treatment is finished.</summary>
    public class PatientStatus : MonoBehaviour
    {
        public TMP_Text statusLabel;
        public TrainingTriggerZone treatmentZone;
        public Renderer[] tintRenderers;
        public Color healedTint = new Color(0.6f, 1f, 0.65f);

        public UnityEvent onHealed;

        bool _healed;

        void Start() => SetLabel("PATIENT STATUS: <color=#FF5050>CRITICAL</color>");

        void Update()
        {
            if (_healed || !treatmentZone || !statusLabel) return;
            float p = treatmentZone.StayProgress;
            if (p > 0f)
                SetLabel($"TREATING... <color=#FFD54A>{Mathf.RoundToInt(p * 100)}%</color>");
        }

        public void Heal()
        {
            if (_healed) return;
            _healed = true;
            SetLabel("PATIENT STATUS: <color=#7CFF6A>STABILIZED</color>");
            var block = new MaterialPropertyBlock();
            foreach (var r in tintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", healedTint);
                block.SetColor("_Color", healedTint);
                r.SetPropertyBlock(block);
            }
            onHealed?.Invoke();
        }

        void SetLabel(string s) { if (statusLabel) statusLabel.text = s; }
    }
}
