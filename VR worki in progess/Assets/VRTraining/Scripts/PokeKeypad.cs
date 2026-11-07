using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// Poke keypad. Any code works: as soon as the required number of digits
    /// has been entered, access is granted and onUnlocked fires.
    /// </summary>
    public class PokeKeypad : MonoBehaviour
    {
        public int codeLength = 4;
        public TMP_Text display;
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip grantedSound;
        public AudioClip deniedSound;
        [Tooltip("Keypad only accepts input while this task is active (training order).")]
        public TrainingTask onlyDuringTask;

        [Header("Unity Events")]
        public UnityEvent onDigitEntered;
        public UnityEvent onUnlocked;

        public bool IsUnlocked { get; private set; }
        string _code = "";

        void Start() => Refresh();

        bool Locked => onlyDuringTask && !onlyDuringTask.IsActive;
        float _deniedUntil;

        void Update()
        {
            if (_deniedUntil > 0 && Time.time > _deniedUntil) { _deniedUntil = 0; Refresh(); }
        }

        void Deny()
        {
            if (audioSource && deniedSound) audioSource.PlayOneShot(deniedSound);
            if (display) display.text = "<color=#FF3B3B>ACCESS DENIED</color>\n<size=55%><color=#BBBBBB>COMPLETE ALL PROCEDURES FIRST</color></size>";
            _deniedUntil = Time.time + 2f;
        }

        public void PressDigit(string digit)
        {
            if (IsUnlocked) return;
            if (Locked) { Deny(); return; }
            if (audioSource && clickSound) audioSource.PlayOneShot(clickSound);
            _code += digit;
            onDigitEntered?.Invoke();
            if (_code.Length >= codeLength) Unlock();
            else Refresh();
        }

        public void Clear()
        {
            if (IsUnlocked) return;
            if (Locked) { Deny(); return; }
            if (audioSource && clickSound) audioSource.PlayOneShot(clickSound);
            _code = "";
            Refresh();
        }

        void Unlock()
        {
            IsUnlocked = true;
            if (display) display.text = $"<color=#7CFF6A>ACCESS GRANTED</color>\n<size=70%><color=#8A8A8A>{string.Join(" ", _code.ToCharArray())}</color></size>";
            if (audioSource && grantedSound) audioSource.PlayOneShot(grantedSound);
            onUnlocked?.Invoke();
        }

        void Refresh()
        {
            if (!display) return;
            var s = _code;
            for (int i = _code.Length; i < codeLength; i++) s += "_";
            display.text = $"<size=60%><color=#FF3B3B>SECURITY LOCK</color></size>\n<color=#FFFFFF>{string.Join(" ", s.ToCharArray())}</color>";
        }
    }
}
