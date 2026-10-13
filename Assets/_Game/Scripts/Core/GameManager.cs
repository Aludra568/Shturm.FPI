using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RicochetBerlin
{
    public enum GameState { Menu, Playing, Paused, Won, Lost }

    /// <summary>
    /// Round flow: menu -> playing -> won/lost, pause, restart, stats.
    /// Lives in the scene (no DontDestroyOnLoad), so a scene reload is a full, clean restart.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Restart skips the main menu; "back to menu" shows it again. Survives scene reloads.</summary>
        public static bool SkipMenuOnLoad;

        // play mode starts without a domain reload: a restart interrupted by "stop" must not skip the next menu
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SkipMenuOnLoad = false;

        const string BestTimeKey = "RicochetBerlin.BestTime";

        [SerializeField] PlayerTank player;
        [SerializeField] float endScreenDelay = 1.4f;

        public GameState State { get; private set; } = GameState.Menu;
        public PlayerTank Player => player;
        public float ElapsedTime { get; private set; }
        public int Kills { get; private set; }
        public int RicochetKills { get; private set; }
        public bool HasFlag { get; private set; }
        /// <summary>Best extraction time on the current difficulty.</summary>
        public float BestTime => PlayerPrefs.GetFloat(BestTimeKey + Difficulty.Current, 0f);
        public bool NewRecord { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action FlagTaken;
        public event Action<Vector2> EnemyKilled;
        /// <summary>Kill made by a shell after bouncing: position and number of bounces.</summary>
        public event Action<Vector2, int> RicochetKill;
        /// <summary>An enemy shell knocked back by a player shell.</summary>
        public event Action<Vector2> Parried;

        Coroutine hitStopRoutine;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            player.Health.Died += OnPlayerDied;
            SetState(SkipMenuOnLoad ? GameState.Playing : GameState.Menu);
            SkipMenuOnLoad = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (State == GameState.Playing) ElapsedTime += Time.deltaTime;
        }

        public bool IsPlaying => State == GameState.Playing;

        public void StartGame()
        {
            if (State == GameState.Menu) SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            SkipMenuOnLoad = true;
            ReloadScene();
        }

        public void BackToMenu()
        {
            SkipMenuOnLoad = false;
            ReloadScene();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void TakeFlag()
        {
            if (HasFlag || !IsPlaying) return;
            HasFlag = true;
            FlagTaken?.Invoke();
        }

        public void RegisterKill(Vector2 position)
        {
            Kills++;
            EnemyKilled?.Invoke(position);
        }

        public void RegisterParry(Vector2 position) => Parried?.Invoke(position);

        public void RegisterRicochetKill(Vector2 position, int bounces)
        {
            RicochetKills++;
            RicochetKill?.Invoke(position, bounces);
        }

        public void Win()
        {
            if (!IsPlaying) return;
            float best = BestTime;
            NewRecord = best <= 0f || ElapsedTime < best;
            if (NewRecord)
            {
                PlayerPrefs.SetFloat(BestTimeKey + Difficulty.Current, ElapsedTime);
                PlayerPrefs.Save();
            }
            player.Health.SetInvulnerable(true);
            StartCoroutine(EndAfterDelay(GameState.Won));
        }

        void OnPlayerDied()
        {
            if (!IsPlaying) return;
            StartCoroutine(EndAfterDelay(GameState.Lost));
        }

        IEnumerator EndAfterDelay(GameState end)
        {
            // keep the world running for a moment so the explosion / extraction reads well
            State = end;
            yield return new WaitForSecondsRealtime(endScreenDelay);
            SetState(end);
        }

        /// <summary>Freezes time for a few frames on impactful hits ("hit stop").</summary>
        public void HitStop(float duration)
        {
            if (!IsPlaying) return;
            if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            if (State != GameState.Paused) Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        void SetState(GameState next)
        {
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(next);
        }

        static void ReloadScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
