using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Tiny one-shot audio player with a source pool. Volume falls off with distance to the camera,
    /// which is all the "3D" a top-down game needs.
    /// </summary>
    public class Audio : MonoBehaviour
    {
        public static Audio Instance { get; private set; }
        public static SoundBank Bank => Instance != null ? Instance.bank : null;

        [SerializeField] SoundBank bank;
        [SerializeField] int voices = 24;
        [SerializeField] float hearingRange = 22f;

        AudioSource[] sources;
        int next;

        void Awake()
        {
            Instance = this;
            sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays a random variant. Pass a world position for distance falloff, or null for UI sounds.</summary>
        public static void Play(AudioClip[] clips, Vector2? at = null, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
        {
            if (Instance == null || clips == null || clips.Length == 0) return;
            Instance.PlayInternal(clips[Random.Range(0, clips.Length)], at, volume, pitch, pitchJitter);
        }

        void PlayInternal(AudioClip clip, Vector2? at, float volume, float pitch, float jitter)
        {
            if (clip == null) return;
            if (at.HasValue && Camera.main != null)
            {
                float d = Vector2.Distance(at.Value, Camera.main.transform.position);
                volume *= Mathf.Clamp01(1f - d / hearingRange);
                if (volume < 0.02f) return;
            }

            AudioSource src = sources[next];
            next = (next + 1) % sources.Length;
            src.pitch = pitch + Random.Range(-jitter, jitter);
            src.PlayOneShot(clip, volume);
        }

        /// <summary>Loudness factor (0..1) for looping sources that sit on moving objects.</summary>
        public static float Falloff(Vector2 at)
        {
            if (Instance == null || Camera.main == null) return 0f;
            return Mathf.Clamp01(1f - Vector2.Distance(at, Camera.main.transform.position) / Instance.hearingRange);
        }
    }
}
