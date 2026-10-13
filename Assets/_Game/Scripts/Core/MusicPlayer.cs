using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Background music: one track for the main menu, another for combat, one for the helicopter boss
    /// fight and one for the win credits, with crossfades between them.
    /// Drop any tracks you have the rights to into the fields.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip combatMusic;
        [Tooltip("Replaces the combat music from the start while the helicopter boss is alive.")]
        [SerializeField] AudioClip bossMusic;
        [SerializeField, Range(0, 1)] float bossVolume = 0.45f;
        [Tooltip("Plays from the start when the round is won (end credits and results).")]
        [SerializeField] AudioClip creditsMusic;
        [SerializeField, Range(0, 1)] float creditsVolume = 0.6f;
        [SerializeField, Range(0, 1)] float menuVolume = 0.45f;
        [SerializeField, Range(0, 1)] float combatVolume = 0.35f;
        [Tooltip("Combat music volume on the pause / end screens.")]
        [SerializeField, Range(0, 1)] float quietVolume = 0.15f;
        [SerializeField] float fadeSpeed = 0.6f;

        AudioSource menu, combat, boss, credits;

        public static MusicPlayer Instance { get; private set; }

        /// <summary>Playback position of the credits track in seconds, or -1 when it is not playing.</summary>
        public float CreditsTime => credits != null && credits.isPlaying ? credits.time : -1f;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Awake()
        {
            Instance = this;
            menu = MakeSource(menuMusic);
            combat = MakeSource(combatMusic);
            boss = MakeSource(bossMusic, autoPlay: false);
            credits = MakeSource(creditsMusic, autoPlay: false);
        }

        AudioSource MakeSource(AudioClip clip, bool autoPlay = true)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            if (clip != null && autoPlay) src.Play();
            return src;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            GameState state = gm != null ? gm.State : GameState.Menu;
            bool won = state == GameState.Won && credits.clip != null;
            ObjectiveDirector od = ObjectiveDirector.Instance;
            bool bossFight = boss.clip != null && od != null && od.Boss != null && !od.BossDefeated && !won;
            if (bossFight && !boss.isPlaying) boss.Play();

            // in a round: full volume while playing, quiet on the pause / defeat screens
            float roundVolume(float full) => state == GameState.Playing ? full : state == GameState.Menu || won ? 0f : quietVolume;
            float menuTarget = state == GameState.Menu ? menuVolume : 0f;
            float combatTarget = bossFight ? 0f : roundVolume(combatVolume);
            float bossTarget = bossFight ? roundVolume(bossVolume) : 0f;
            if (won && !credits.isPlaying) credits.Play();

            float step = fadeSpeed * Time.unscaledDeltaTime;
            menu.volume = Mathf.MoveTowards(menu.volume, menuTarget, step);
            combat.volume = Mathf.MoveTowards(combat.volume, combatTarget, step);
            boss.volume = Mathf.MoveTowards(boss.volume, bossTarget, step);
            if (!bossFight && boss.isPlaying && boss.volume <= 0f) boss.Stop();
            credits.volume = Mathf.MoveTowards(credits.volume, won ? creditsVolume : 0f, step);
        }
    }
}
