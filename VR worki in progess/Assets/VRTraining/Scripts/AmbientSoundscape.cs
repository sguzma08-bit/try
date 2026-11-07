using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Background sound for the facility. Every clip slot is optional - drop in sounds you download.
    ///  - Vent hum: loops quietly the whole time
    ///  - Creaks: a random one plays every so often, somewhere around the player
    ///  - Heartbeat: loops during the heartbeat task and speeds up until it is completed
    /// </summary>
    public class AmbientSoundscape : MonoBehaviour
    {
        [Header("Vent / machine hum (looping)")]
        public AudioClip ventHumLoop;
        [Range(0, 1)] public float humVolume = 0.35f;

        [Header("Metal creaks / distant noises (one-shots)")]
        public AudioClip[] creaks;
        [Range(0, 1)] public float creakVolume = 0.6f;
        public Vector2 secondsBetweenCreaks = new Vector2(8f, 20f);
        public Transform listener;   // the player's head

        [Header("Heartbeat (looping) - plays during this task")]
        public AudioClip heartbeatLoop;
        [Range(0, 1)] public float heartbeatVolume = 0.7f;
        public TrainingTask heartbeatTask;
        public float heartbeatStartPitch = 1f;
        public float heartbeatMaxPitch = 1.4f;
        public float secondsToMaxPitch = 20f;

        AudioSource _hum, _creak, _heart;
        float _nextCreak, _heartStarted = -1;

        void Start()
        {
            _hum = Make("Hum", true, 0f);
            _creak = Make("Creak", false, 1f);
            _heart = Make("Heartbeat", true, 0f);
            if (ventHumLoop) { _hum.clip = ventHumLoop; _hum.volume = humVolume; _hum.Play(); }
            _nextCreak = Time.time + Random.Range(secondsBetweenCreaks.x, secondsBetweenCreaks.y);
            if (heartbeatTask)
            {
                heartbeatTask.onTaskStarted.AddListener(StartHeartbeat);
                heartbeatTask.onTaskCompleted.AddListener(StopHeartbeat);
            }
        }

        AudioSource Make(string n, bool loop, float spatial)
        {
            var go = new GameObject("Ambient_" + n);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.loop = loop; a.playOnAwake = false; a.spatialBlend = spatial;
            return a;
        }

        public void StartHeartbeat()
        {
            if (!heartbeatLoop) return;
            _heart.clip = heartbeatLoop; _heart.volume = heartbeatVolume; _heart.pitch = heartbeatStartPitch;
            _heart.Play();
            _heartStarted = Time.time;
        }

        public void StopHeartbeat() { _heartStarted = -1; }

        void Update()
        {
            if (creaks != null && creaks.Length > 0 && Time.time >= _nextCreak)
            {
                _nextCreak = Time.time + Random.Range(secondsBetweenCreaks.x, secondsBetweenCreaks.y);
                var clip = creaks[Random.Range(0, creaks.Length)];
                if (clip)
                {
                    var around = listener ? listener.position : transform.position;
                    var off = Random.insideUnitSphere * 4f; off.y = Mathf.Abs(off.y) + 1f;
                    _creak.transform.position = around + off;
                    _creak.pitch = Random.Range(0.85f, 1.1f);
                    _creak.PlayOneShot(clip, creakVolume);
                }
            }
            if (_heart.isPlaying)
            {
                if (_heartStarted > 0)
                    _heart.pitch = Mathf.Lerp(heartbeatStartPitch, heartbeatMaxPitch, (Time.time - _heartStarted) / secondsToMaxPitch);
                else
                {
                    _heart.volume = Mathf.MoveTowards(_heart.volume, 0f, Time.deltaTime * 0.5f);
                    _heart.pitch = Mathf.MoveTowards(_heart.pitch, 0.9f, Time.deltaTime * 0.3f);
                    if (_heart.volume <= 0.001f) _heart.Stop();
                }
            }
        }
    }
}
