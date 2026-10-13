using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RicochetBerlin
{
    /// <summary>
    /// End credits after a win, timed to the credits track: fade to black, the studio logo comes in with
    /// the flute, its full name follows, and the team rolls up the screen from the first sung words,
    /// ending on "thanks for playing". Esc / Enter / Space or the button skips.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class Credits : MonoBehaviour
    {
        public static Credits Instance { get; private set; }

        [SerializeField] float fadeIn = 1.2f;
        [Header("Cues in the credits track, seconds")]
        [Tooltip("The flute comes in: the logo appears.")]
        [SerializeField] float fluteAt = 4.4f;
        [Tooltip("Next flute phrase: the studio's full name.")]
        [SerializeField] float nameAt = 11.9f;
        [Tooltip("Next phrase: \"представляет\".")]
        [SerializeField] float presentsAt = 19.4f;
        [Tooltip("The first sung words: the roll starts.")]
        [SerializeField] float vocalsAt = 33.1f;
        [Header("Roll")]
        [Tooltip("Roll speed, panel pixels per second.")]
        [SerializeField] float scrollSpeed = 75f;
        [SerializeField] float finalHold = 3.5f;

        /// <summary>Role, name. Edit freely.</summary>
        static readonly (string role, string name)[] Team =
        {
            ("РУКОВОДИТЕЛЬ ПРОЕКТА - ГЕЙМДИЗАЙН", "Юрчик"),
            ("ПРОГРАММИРОВАНИЕ", "Сайдамчик"),
            ("ГРАФИКА И ИНТЕРФЕЙС", "Никитка"),
            ("ЗВУК И ЭФФЕКТЫ", "Семёнчик"),
            ("ДИЗАЙН УРОВНЯ - ТЕСТИРОВАНИЕ", "Даник"),
        };

        enum Phase { Idle, Intro, Roll, Hold }

        public bool Rolling => phase != Phase.Idle;

        VisualElement overlay, logo, mark;
        Label studio, presents, thanks;
        VisualElement roll;
        Phase phase;
        float t, ownClock;
        Action onDone;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Build(GetComponent<UIDocument>().rootVisualElement);
            overlay.style.display = DisplayStyle.None;
        }

        void Build(VisualElement root)
        {
            overlay = new VisualElement();
            overlay.AddToClassList("credits");
            root.Add(overlay);

            logo = new VisualElement();
            logo.AddToClassList("cr-intro");
            mark = new VisualElement();
            mark.AddToClassList("cr-logo");
            logo.Add(mark);
            studio = Text("Фуфелшмерц Пакость Инкорпорейтед", "cr-studio");
            logo.Add(studio);
            presents = Text("представляет", "cr-presents");
            logo.Add(presents);
            overlay.Add(logo);

            roll = new VisualElement();
            roll.AddToClassList("cr-roll");
            roll.Add(Text("ШТУРМ", "cr-title"));
            roll.Add(Text("КОМАНДА", "cr-section"));
            foreach (var (role, name) in Team) AddEntry(role, name);
            overlay.Add(roll);

            thanks = Text("Спасибо за игру!", "cr-thanks");
            overlay.Add(thanks);

            var skip = new Button(Finish) { text = "ПРОПУСТИТЬ" };
            skip.AddToClassList("btn");
            skip.AddToClassList("cr-skip");
            overlay.Add(skip);
        }

        void AddEntry(string role, string name)
        {
            var entry = new VisualElement();
            entry.AddToClassList("cr-entry");
            entry.Add(Text(role, "cr-role"));
            entry.Add(Text(name, "cr-name"));
            roll.Add(entry);
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        /// <summary>Starts the credits; <paramref name="done"/> runs when they end or are skipped.</summary>
        public void Play(Action done)
        {
            onDone = done;
            phase = Phase.Intro;
            t = ownClock = 0f;
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.opacity = 0f;
            foreach (VisualElement e in new[] { logo, mark, studio, presents, roll, thanks }) e.style.opacity = 0f;
            logo.style.opacity = 1f;
        }

        void Finish()
        {
            if (phase == Phase.Idle) return;
            phase = Phase.Idle;
            overlay.style.display = DisplayStyle.None;
            Action done = onDone;
            onDone = null;
            done?.Invoke();
        }

        /// <summary>0 before <paramref name="start"/>, rising to 1 over <paramref name="length"/> seconds.</summary>
        static float Ramp(float time, float start, float length) => Mathf.Clamp01((time - start) / length);

        void Update()
        {
            if (phase == Phase.Idle) return;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            ownClock += dt;

            Keyboard k = Keyboard.current;
            if (k != null && ownClock > 0.5f &&
                (k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
            {
                Finish();
                return;
            }

            // follow the track itself so the cues land on the music; without it, our own clock
            float song = MusicPlayer.Instance != null ? MusicPlayer.Instance.CreditsTime : -1f;
            if (song < 0f) song = ownClock;

            overlay.style.opacity = Ramp(ownClock, 0f, fadeIn);
            float screenH = overlay.resolvedStyle.height;

            switch (phase)
            {
                case Phase.Intro:
                    // the logo comes in with the flute, then the name and "presents"; all of it leaves in the
                    // break right before the vocals
                    float outro = 1f - Ramp(song, vocalsAt - 1.2f, 0.6f);
                    mark.style.opacity = Ramp(song, fluteAt - 0.15f, 0.6f) * outro;
                    mark.style.scale = new Scale(Vector3.one * (0.9f + 0.1f * Ramp(song, fluteAt, vocalsAt - fluteAt)));
                    studio.style.opacity = Ramp(song, nameAt - 0.15f, 0.8f) * outro;
                    presents.style.opacity = Ramp(song, presentsAt - 0.15f, 0.8f) * outro;
                    if (song >= vocalsAt)
                    {
                        roll.style.opacity = 1f;
                        Next(Phase.Roll);
                    }
                    break;

                case Phase.Roll:
                    float top = screenH - (song - vocalsAt) * scrollSpeed;
                    roll.style.top = top;
                    float bottom = top + roll.resolvedStyle.height;
                    // the last line is leaving: "thanks for playing" fades in at the centre
                    if (bottom < screenH * 0.42f)
                        thanks.style.opacity = Mathf.Clamp01(thanks.resolvedStyle.opacity + dt / 1.2f);
                    if (bottom < 0f) Next(Phase.Hold);
                    break;

                case Phase.Hold:
                    thanks.style.opacity = 1f;
                    if (t >= finalHold) Finish();
                    break;
            }
        }

        void Next(Phase p)
        {
            phase = p;
            t = 0f;
        }
    }
}
