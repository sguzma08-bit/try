using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Makes a glove model copy the Meta hand every frame (works for tracked hands and controller hands).
    /// The glove is placed on the wrist, turned to match the palm, scaled to the hand size, and each
    /// finger bone is aimed at the matching Meta finger joint. The default hand mesh is hidden.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class GloveHand : MonoBehaviour
    {
        public HandVisual source;                 // OVRHandVisualRight / OVRHandVisualLeft
        public Transform gloveModel;              // the imported glove (child of this object)
        public Renderer[] hideRenderers;          // Meta's default hand meshes
        public bool hideDefaultHand = true;

        // Meta joint indices (HandJointId): wrist 1, thumb 2-5, index 6-10, middle 11-15, ring 16-20, little 21-25
        static readonly string[][] Fingers =
        {
            new[] { "thumb01", "thumb02", "thumb03" },
            new[] { "index00", "index01", "index02", "index03" },
            new[] { "middle00", "middle01", "middle02", "middle03" },
            new[] { "ring00", "ring01", "ring02", "ring03" },
            new[] { "pinky00", "pinky01", "pinky02", "pinky03" },
        };
        static readonly int[][] FingerJoints =
        {
            new[] { 2, 3, 4, 5 },
            new[] { 6, 7, 8, 9, 10 },
            new[] { 11, 12, 13, 14, 15 },
            new[] { 16, 17, 18, 19, 20 },
            new[] { 21, 22, 23, 24, 25 },
        };

        Transform _wrist, _middle1, _index1, _pinky1;
        readonly List<Transform[]> _chains = new List<Transform[]>();
        readonly List<Vector3> _tipDirLocal = new List<Vector3>();
        readonly Dictionary<Transform, Quaternion> _rest = new Dictionary<Transform, Quaternion>();
        Renderer[] _gloveRenderers;
        Vector3 _baseScale;
        bool _ok;

        void Start()
        {
            if (!source || !gloveModel) return;
            foreach (var a in gloveModel.GetComponentsInChildren<Animation>(true)) a.enabled = false;
            foreach (var a in gloveModel.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var all = gloveModel.GetComponentsInChildren<Transform>(true);
            Transform Bone(string prefix) { foreach (var t in all) if (t.name.StartsWith(prefix)) return t; return null; }
            _wrist = Bone("hand_");
            _middle1 = Bone("middle01"); _index1 = Bone("index01"); _pinky1 = Bone("pinky01");
            foreach (var f in Fingers)
            {
                var chain = new Transform[f.Length];
                for (int i = 0; i < f.Length; i++) chain[i] = Bone(f[i]);
                _chains.Add(chain);
                var last = chain[chain.Length - 1]; var prev = chain[chain.Length - 2];
                _tipDirLocal.Add(last && prev ? last.InverseTransformDirection(last.position - prev.position).normalized : Vector3.forward);
            }
            foreach (var t in all) _rest[t] = t.localRotation;
            _gloveRenderers = gloveModel.GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;
            _ok = _wrist && _middle1 && _index1 && _pinky1;
            if (!_ok) Debug.LogWarning($"[VR Training] {name}: glove bones not found (need hand_, index01, middle01, pinky01).");
        }

        void LateUpdate()
        {
            if (!_ok) return;
            var J = source.Joints;
            bool visible = source.isActiveAndEnabled && source.IsVisible && J != null && J.Count > 25;
            foreach (var r in _gloveRenderers) r.enabled = visible;
            if (hideRenderers != null) foreach (var r in hideRenderers) if (r) r.forceRenderingOff = hideDefaultHand;
            if (!visible) return;

            // 1) back to the glove's rest pose
            foreach (var kv in _rest) kv.Key.localRotation = kv.Value;

            // 2) turn the whole glove so its palm matches the Meta palm
            Vector3 fwdO = J[12].position - J[1].position, sideO = J[7].position - J[22].position;
            Vector3 fwdG = _middle1.position - _wrist.position, sideG = _index1.position - _pinky1.position;
            var delta = Quaternion.LookRotation(fwdO, Vector3.Cross(fwdO, sideO)) *
                        Quaternion.Inverse(Quaternion.LookRotation(fwdG, Vector3.Cross(fwdG, sideG)));
            transform.rotation = delta * transform.rotation;

            // 3) same size as the tracked hand, wrist on the wrist
            float lenG = (_middle1.position - _wrist.position).magnitude;
            if (lenG > 1e-5f) transform.localScale *= fwdO.magnitude / lenG;
            transform.position += J[1].position - _wrist.position;

            // 4) aim each finger bone at the matching Meta joint
            for (int f = 0; f < _chains.Count; f++)
            {
                var chain = _chains[f]; var ids = FingerJoints[f];
                for (int i = 0; i < chain.Length; i++)
                {
                    var b = chain[i];
                    if (!b) continue;
                    Vector3 cur = i < chain.Length - 1 && chain[i + 1]
                        ? chain[i + 1].position - b.position
                        : b.TransformDirection(_tipDirLocal[f]);
                    Vector3 tgt = J[ids[i + 1]].position - J[ids[i]].position;
                    if (cur.sqrMagnitude < 1e-10f || tgt.sqrMagnitude < 1e-10f) continue;
                    b.rotation = Quaternion.FromToRotation(cur, tgt) * b.rotation;
                }
            }
        }

        void OnDisable()
        {
            if (hideRenderers != null) foreach (var r in hideRenderers) if (r) r.forceRenderingOff = false;
        }
    }
}
