using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining
{
    /// <summary>
    /// Trigger volume that raises Unity Events for OnTriggerEnter / OnTriggerStay / OnTriggerExit,
    /// filtered to specific objects. Can also fire when ALL required objects are inside,
    /// or after an object has stayed inside for a set time.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TrainingTriggerZone : MonoBehaviour
    {
        [Tooltip("Objects this zone reacts to (the root that holds the Rigidbody).")]
        public List<GameObject> acceptedObjects = new List<GameObject>();
        [Tooltip("Optional: OnTriggerStay only counts while this object is being held.")]
        public TrainingInteractionEvents requireHeld;
        [Tooltip("Seconds an object must stay inside before onStayTimeReached fires (0 = off).")]
        public float requiredStaySeconds = 0f;
        [Tooltip("Optional: only fire events while this task is active.")]
        public TrainingTask onlyDuringTask;

        [Header("Unity Events")]
        public UnityEvent onTriggerEntered;
        public UnityEvent onTriggerExited;
        public UnityEvent onAllRequiredInside;
        public UnityEvent onStayTimeReached;

        readonly Dictionary<GameObject, int> _inside = new Dictionary<GameObject, int>();
        float _stay, _lastStayStep = -1f;
        bool _allFired, _stayFired;

        public float StayProgress => requiredStaySeconds <= 0 ? 0 : Mathf.Clamp01(_stay / requiredStaySeconds);
        public bool IsAnythingInside => _inside.Count > 0;

        public bool AllRequiredInside
        {
            get
            {
                if (acceptedObjects.Count == 0) return false;
                foreach (var o in acceptedObjects)
                    if (o == null || !_inside.ContainsKey(o)) return false;
                return true;
            }
        }

        bool Gate => onlyDuringTask == null || onlyDuringTask.IsActive;

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        GameObject Match(Collider c)
        {
            var t = c.attachedRigidbody ? c.attachedRigidbody.transform : c.transform;
            foreach (var o in acceptedObjects)
                if (o && (t == o.transform || t.IsChildOf(o.transform))) return o;
            return null;
        }

        void OnTriggerEnter(Collider other)
        {
            var o = Match(other);
            if (!o) return;
            _inside.TryGetValue(o, out int n);
            _inside[o] = n + 1;
            if (n > 0) return;
            if (Gate) onTriggerEntered?.Invoke();
            CheckAll();
        }

        void OnTriggerStay(Collider other)
        {
            if (requiredStaySeconds <= 0 || _stayFired) return;
            if (!Match(other)) return;
            if (Mathf.Approximately(Time.fixedTime, _lastStayStep)) return; // count once per physics step
            _lastStayStep = Time.fixedTime;
            if (!Gate) return;
            if (requireHeld && !requireHeld.IsSelected) return;
            _stay += Time.fixedDeltaTime;
            if (_stay >= requiredStaySeconds)
            {
                _stayFired = true;
                onStayTimeReached?.Invoke();
            }
        }

        void OnTriggerExit(Collider other)
        {
            var o = Match(other);
            if (!o || !_inside.TryGetValue(o, out int n)) return;
            if (n > 1) { _inside[o] = n - 1; return; }
            _inside.Remove(o);
            _allFired = false;
            if (Gate) onTriggerExited?.Invoke();
        }

        void Update()
        {
            // Objects that get disabled while inside never send OnTriggerExit.
            if (_inside.Count == 0) return;
            List<GameObject> gone = null;
            foreach (var kv in _inside)
                if (kv.Key == null || !kv.Key.activeInHierarchy) (gone ??= new List<GameObject>()).Add(kv.Key);
            if (gone != null) foreach (var g in gone) _inside.Remove(g);
        }

        void CheckAll()
        {
            if (_allFired || !AllRequiredInside || !Gate) return;
            _allFired = true;
            onAllRequiredInside?.Invoke();
        }

        /// <summary>Re-check after the task becomes active (objects may already be inside).</summary>
        public void Recheck() => CheckAll();

        public void ResetZone()
        {
            _inside.Clear();
            _stay = 0;
            _allFired = _stayFired = false;
        }
    }
}
