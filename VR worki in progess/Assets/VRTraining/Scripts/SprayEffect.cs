using UnityEngine;

namespace VRTraining
{
    /// <summary>Plays the spray particles while the spray is being used on the patient.</summary>
    public class SprayEffect : MonoBehaviour
    {
        public ParticleSystem particles;
        public AudioSource loopAudio;

        public void StartSpray()
        {
            if (particles && !particles.isPlaying) particles.Play();
            if (loopAudio && !loopAudio.isPlaying) loopAudio.Play();
        }

        public void StopSpray()
        {
            if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (loopAudio) loopAudio.Stop();
        }
    }
}
