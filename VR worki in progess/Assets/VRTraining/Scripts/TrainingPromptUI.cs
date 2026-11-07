using TMPro;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Writes the onboarding / step prompts onto the training panel in a
    /// corporate bio-lab terminal style, with a typewriter reveal and blinking cursor.
    /// </summary>
    public class TrainingPromptUI : MonoBehaviour
    {
        public TMP_Text promptText;
        public AudioSource audioSource;
        public AudioClip stepChime;

        [Header("Terminal style")]
        public string terminalHeader = "BIOHAZARD RESPONSE DIVISION  //  MEDICAL TRAINING TERMINAL";
        public string terminalSubHeader = "SECURE LINK ESTABLISHED  -  CLEARANCE: FIELD MEDIC  -  NODE R-4";
        public Color headerColor = new Color(0.86f, 0.08f, 0.12f);
        public Color bodyColor = new Color(0.9f, 0.9f, 0.88f);

        [Header("Typewriter")]
        public float charactersPerSecond = 120f;
        public float cursorBlinkSeconds = 0.5f;

        string _header = "", _title = "", _body = "";
        string _full = "";
        float _visible;
        int _total;

        public void ShowTask(int step, int total, string title, string body)
        {
            _header = $"PROCEDURE {step:00} / {total:00}";
            _title = title.ToUpperInvariant();
            _body = body;
            Render(null, null, restart: true);
            if (audioSource && stepChime) audioSource.PlayOneShot(stepChime);
        }

        /// <summary>Adds a short hint line under the current step (used by Hover events).</summary>
        public void ShowHint(string hint) => Render("> " + hint, "#FFB000", restart: false);

        /// <summary>Shows a success line (used when a task completes).</summary>
        public void ShowSuccess(string message) => Render("[ VERIFIED ] " + message, "#7CFF6A", restart: false);

        public void ShowMessage(string title, string body)
        {
            _header = "SESSION CLOSED";
            _title = title.ToUpperInvariant();
            _body = body;
            Render(null, null, restart: true);
        }

        void Render(string extra, string color, bool restart)
        {
            if (!promptText) return;
            string hc = ColorUtility.ToHtmlStringRGB(headerColor);
            string bc = ColorUtility.ToHtmlStringRGB(bodyColor);
            var s = $"<color=#{hc}><b>{terminalHeader}</b></color>\n";
            s += $"<size=55%><color=#8A8A8A>{terminalSubHeader}</color></size>\n";
            s += $"<color=#{hc}>________________________________________</color>\n\n";
            if (!string.IsNullOrEmpty(_header)) s += $"<size=70%><color=#FF4A4A>{_header}</color></size>\n";
            s += $"<color=#FFFFFF><b>{_title}</b></color>\n\n<size=80%><color=#{bc}>{_body}</color></size>";
            if (!string.IsNullOrEmpty(extra)) s += $"\n\n<size=80%><color={color}>{extra}</color></size>";

            _full = s;
            promptText.text = _full + " _";
            promptText.ForceMeshUpdate();
            int newTotal = promptText.textInfo.characterCount;
            if (restart) _visible = 0;
            else _visible = Mathf.Min(_visible, _total);   // keep what was already on screen
            _total = newTotal;
            promptText.maxVisibleCharacters = Mathf.FloorToInt(_visible);
        }

        void Update()
        {
            if (!promptText || string.IsNullOrEmpty(_full)) return;
            if (_visible < _total)
            {
                _visible += charactersPerSecond * Time.deltaTime;
                promptText.maxVisibleCharacters = Mathf.FloorToInt(_visible);
            }
            else
            {
                // Blinking cursor once the text is fully typed.
                bool on = Mathf.Repeat(Time.time, cursorBlinkSeconds * 2f) < cursorBlinkSeconds;
                promptText.maxVisibleCharacters = on ? _total : _total - 1;
            }
        }
    }
}
