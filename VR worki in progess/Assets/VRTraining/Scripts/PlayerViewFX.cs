using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRTraining
{
    /// <summary>
    /// VR-safe survival-horror view feedback using a whole-view color tint
    /// (vignette/chromatic aberration look wrong in headsets and the XR Simulator):
    ///  - PulseSuccess(): short green tint when a step is verified
    ///  - DangerOn()/DangerOff(): red heartbeat tint while the patient is critical
    /// </summary>
    public class PlayerViewFX : MonoBehaviour
    {
        public Volume volume;
        public Color baseTint = new Color(0.9f, 1f, 0.93f);
        public Color successTint = new Color(0.75f, 1f, 0.78f);
        public Color dangerTint = new Color(1f, 0.62f, 0.62f);
        public float heartbeatBpm = 70f;

        ColorAdjustments _ca;
        bool _danger;
        float _flash;

        void Start()
        {
            if (volume && volume.profile) volume.profile.TryGet(out _ca);
        }

        public void PulseSuccess() => _flash = 1f;

        float _blackoutUntil;
        /// <summary>Drops the view to near-black for a moment (lights-out scare).</summary>
        public void Blackout(float seconds) => _blackoutUntil = Time.time + seconds;
        public void DangerOn() => _danger = true;
        public void DangerOff() => _danger = false;

        void Update()
        {
            if (!_ca) return;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 1.2f);
            Color c = baseTint;
            if (_danger)
            {
                float t = Mathf.Repeat(Time.time * heartbeatBpm / 60f, 1f);
                float beat = Mathf.Exp(-40f * t * t) + 0.6f * Mathf.Exp(-40f * (t - 0.25f) * (t - 0.25f));
                c = Color.Lerp(baseTint, dangerTint, 0.35f + 0.5f * beat);
            }
            if (_flash > 0f) c = Color.Lerp(c, successTint, _flash);
            _ca.colorFilter.Override(c);
            _ca.postExposure.Override(Time.time < _blackoutUntil ? -6f : 0f);
        }
    }
}
