using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RicochetBerlin
{
    /// <summary>
    /// Onboarding cards shown when the game starts: one card per mechanic, each with pictures drawn
    /// from the game's own sprites. Arrows (or A/D, ←/→) flip, "ПРОПУСТИТЬ" (or Esc) closes.
    /// Shown once per launch; the menu's "ОБУЧЕНИЕ" button opens it again.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class Onboarding : MonoBehaviour
    {
        [Tooltip("Card illustrations (Assets/_Game/UI/Cards), looked up by file name.")]
        [SerializeField] Texture2D[] pictures;

        /// <summary>Set once the cards have been seen in this launch (restarts do not show them again).</summary>
        static bool shownThisLaunch;

        // the project enters play mode without a domain reload, so statics survive between runs in the editor
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            shownThisLaunch = false;
            instance = null;
        }

        struct Picture
        {
            public string image, caption;
            public Picture(string image, string caption) { this.image = image; this.caption = caption; }
        }

        struct Card
        {
            public string title, text;
            public Picture[] pictures;
        }

        const string Gap = "\u00A0\u00A0\u00A0\u00A0\u00A0\u00A0";

        static readonly Card[] Cards =
        {
            new Card
            {
                title = "ЦЕЛЬ",
                text = "Флаг спрятан где-то в руинах. Карты нет - слушай радиомаяк: чем ближе флаг, тем чаще он пищит. " +
                       "Подбери флаг и довези его до точки эвакуации.",
                pictures = new[] { new Picture("c1_flag", "Флаг"), new Picture("c1_flag_tank", "Флаг на танке"),
                                   new Picture("c1_route", "Путь до эвакуации") }
            },
            new Card
            {
                title = "ДРОНЫ",
                text = "",
                pictures = new[] { new Picture("c15_kamikaze", "Камикадзе: догоняет и взрывается"),
                                   new Picture("c15_gunner", "Стрелок: стреляет издалека") }
            },
            new Card
            {
                title = "БОСС",
                text = "У точки эвакуации ждёт вертолёт - пока он в воздухе, не улететь. Броня гасит урон, " +
                       "полный урон - по светящейся точке. Она всегда отвёрнута от тебя: бей рикошетом.",
                pictures = new[] { new Picture("c2_heli", "Вертолёт"), new Picture("c2_phase1", "Фаза 1: хвост"),
                                   new Picture("c2_phase2", "Фаза 2: правый борт"), new Picture("c2_phase3", "Фаза 3: левый борт") }
            },
            new Card
            {
                title = "ОГОНЬ И ПРИЦЕЛ",
                text = "Башня поворачивается за мышью, корпус - за башней. В магазине 5 снарядов, потом перезарядка 3 с.",
                pictures = new[] { new Picture("c3_fire", "ЛКМ - выстрел"), new Picture("c3_aim", "ПКМ - прицел") }
            },
            new Card
            {
                title = "РИКОШЕТ",
                text = "Снаряд летит бесконечно и отскакивает от стен до 3 раз. Свой рикошет тоже ранит.",
                pictures = new[] { new Picture("c35_ricochet", "Отскок от стен"),
                                   new Picture("c35_parry", "Сбей вражеский снаряд - он полетит обратно") }
            },
            new Card
            {
                title = "ОПАСАЙСЯ",
                text = "",
                pictures = new[] { new Picture("c4_behind", "Красная стрелка - дрон сзади"),
                                   new Picture("c4_tripwire", "Растяжка: -50% брони. Стреляй во взрыватель"),
                                   new Picture("c4_barrel", "Бочка взрывается от выстрела") }
            },
            new Card
            {
                title = "ТЕБЕ ПОМОГУТ",
                text = "Ящики лежат на улицах - подъезжай и забирай.",
                pictures = new[] { new Picture("c5_mangal", "МАНГАЛ\nдержит 3 удара"), new Picture("c5_twin", "СПАРКА\nдва ствола до конца боя"),
                                   new Picture("c5_allies", "ДРОНЫ\n3 помощника"), new Picture("c5_repair", "РЕМОНТ\n+35 брони") }
            },
            new Card
            {
                title = "УПРАВЛЕНИЕ",
                // UI Toolkit collapses runs of spaces; non-breaking ones keep the gaps between the items
                text = "W S - газ" + Gap + "A D - поворот корпуса" + Gap + "МЫШЬ - башня\n" +
                       "Q - разворот на 180°" + Gap + "SHIFT - умение танка (раз в 30 с)" + Gap + "ESC - пауза",
                pictures = new[] { new Picture("c6_controls", "") }
            },
        };

        const float RowWidth = 1180f, CellMargin = 28f;

        VisualElement overlay, card, pictureRow, dots;
        Label title, text;
        Button prev, next, skip;
        readonly Dictionary<string, Texture2D> byName = new();
        int page;

        public bool IsOpen => overlay != null && overlay.style.display == DisplayStyle.Flex;

        static Onboarding instance;
        int closedFrame = -1;

        /// <summary>True while the cards are open (or were closed this frame), so menu keys don't fire through them.</summary>
        public static bool Blocking => instance != null && (instance.IsOpen || instance.closedFrame == Time.frameCount);

        void Awake() => instance = this;

        void Start()
        {
            foreach (Texture2D t in pictures) if (t != null) byName[t.name] = t;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            Build(root);
            root.Q<Button>("open-tutorial")?.RegisterCallback<ClickEvent>(_ => Open());

            overlay.style.display = DisplayStyle.None;
            autoOpen = !shownThisLaunch;
        }

        bool autoOpen;

        void Build(VisualElement root)
        {
            overlay = new VisualElement();
            overlay.AddToClassList("onboarding");
            root.Add(overlay);

            prev = new Button(() => Flip(-1)) { text = "‹" };
            prev.AddToClassList("ob-arrow");

            card = new VisualElement();
            card.AddToClassList("ob-card");
            title = new Label();
            title.AddToClassList("ob-title");
            text = new Label();
            text.AddToClassList("ob-text");
            pictureRow = new VisualElement();
            pictureRow.AddToClassList("ob-pictures");
            var tape = new VisualElement { pickingMode = PickingMode.Ignore };
            tape.AddToClassList("hazard");
            card.Add(tape);
            card.Add(title);
            card.Add(text);
            card.Add(pictureRow);

            next = new Button(() => Flip(1)) { text = "›" };
            next.AddToClassList("ob-arrow");

            var middle = new VisualElement();
            middle.AddToClassList("ob-middle");
            middle.Add(prev);
            middle.Add(card);
            middle.Add(next);
            overlay.Add(middle);

            dots = new VisualElement();
            dots.AddToClassList("ob-dots");
            for (int i = 0; i < Cards.Length; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("ob-dot");
                dots.Add(dot);
            }
            overlay.Add(dots);

            skip = new Button(Close);
            skip.AddToClassList("btn");
            skip.AddToClassList("ob-skip");
            overlay.Add(skip);
        }

        public void Open()
        {
            shownThisLaunch = true;
            overlay.style.display = DisplayStyle.Flex;
            page = 0;
            ShowPage();
        }

        void Close()
        {
            shownThisLaunch = true;
            if (IsOpen) closedFrame = Time.frameCount;
            overlay.style.display = DisplayStyle.None;
        }

        void Flip(int dir)
        {
            int target = page + dir;
            if (target >= Cards.Length) { Close(); return; }
            if (target < 0) return;
            page = target;
            Audio.Play(Audio.Bank?.uiClick, null, 0.5f);
            ShowPage();
        }

        void ShowPage()
        {
            Card c = Cards[page];
            title.text = c.title;
            text.text = c.text;
            text.style.display = string.IsNullOrEmpty(c.text) ? DisplayStyle.None : DisplayStyle.Flex;

            pictureRow.Clear();
            // enlarge the pictures to fill the card width (but not past 1.4x, they are pixel-ish art)
            float total = 0f;
            foreach (Picture p in c.pictures)
                total += (byName.TryGetValue(p.image, out Texture2D t) ? t.width : 200) + CellMargin;
            float scale = Mathf.Clamp(RowWidth / total, 1f, 1.4f);

            foreach (Picture p in c.pictures)
            {
                var cell = new VisualElement();
                cell.AddToClassList("ob-cell");
                var img = new VisualElement();
                img.AddToClassList("ob-img");
                if (byName.TryGetValue(p.image, out Texture2D tex))
                {
                    img.style.backgroundImage = tex;
                    img.style.width = tex.width * scale;
                    img.style.height = tex.height * scale;
                }
                cell.Add(img);
                if (!string.IsNullOrEmpty(p.caption))
                {
                    var cap = new Label(p.caption);
                    cap.AddToClassList("ob-caption");
                    cap.style.maxWidth = byName.TryGetValue(p.image, out Texture2D t2) ? t2.width * scale + 20 : 240;
                    cell.Add(cap);
                }
                pictureRow.Add(cell);
            }

            for (int i = 0; i < dots.childCount; i++) dots[i].EnableInClassList("on", i == page);
            prev.SetEnabled(page > 0);
            skip.text = page == Cards.Length - 1 ? "ПОНЯТНО" : "ПРОПУСТИТЬ";
            skip.EnableInClassList("last", page == Cards.Length - 1);

            // slide-in: start offset and transparent, the USS transition animates to rest
            card.AddToClassList("ob-enter");
            card.schedule.Execute(() => card.RemoveFromClassList("ob-enter")).StartingIn(16);
        }

        void Update()
        {
            // first frame after everything started: show the cards over the menu (not when a restart skips it)
            if (autoOpen)
            {
                autoOpen = false;
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Menu) Open();
                else shownThisLaunch = true;
                return;
            }
            if (!IsOpen || Keyboard.current == null) return;
            Keyboard k = Keyboard.current;
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) Flip(1);
            else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Flip(-1);
            else if (k.escapeKey.wasPressedThisFrame) Close();
        }
    }
}
