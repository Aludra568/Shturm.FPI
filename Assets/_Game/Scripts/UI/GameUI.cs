using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RicochetBerlin
{
    /// <summary>
    /// Drives the UI Toolkit document: main menu, HUD, pause and end screens. HUD texts are rebuilt only
    /// when the number behind them changes, so the bar costs no garbage per frame.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] Camera cam;

        GameManager game;
        PlayerTank player;
        ObjectiveDirector objective;

        VisualElement root, hud, menu, pause, end, vignette, extract, evacArrow;
        VisualElement armorFill, nitroFill, extractFill, signalBars;
        VisualElement[] bars;
        Label objectiveLabel, timer, kills, alert, shells, nitroCaption, evacDistance, signalCaption;
        Label modShield, modTwin, modTrack, modAllies, armorValue, shellsCount;
        VisualElement tankCards, swatches, difficultyRow, bossPanel, bossFill;
        Label extractCaption, bossCaption;
        VisualElement evacArrowHead;
        float vignetteAlpha;

        // last values written to the HUD labels
        int shownSecond = -1, shownHp = -1, shownAmmo = -1, shownReload = -1, shownMangal = -1, shownAllies = -1, shownMeters = -1;
        int shownAbility = int.MinValue;

        void Awake()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            hud = root.Q("hud");
            menu = root.Q("menu");
            pause = root.Q("pause");
            end = root.Q("end");
            vignette = root.Q("damage-vignette");
            extract = root.Q("extract");
            evacArrow = root.Q("evac-arrow");
            evacArrowHead = evacArrow.Q(className: "evac-arrow-head");
            armorFill = root.Q("armor-fill");
            nitroFill = root.Q("nitro-fill");
            extractFill = root.Q("extract-fill");
            signalBars = root.Q("signal-bars");
            bars = signalBars.Query(className: "signal-bar").ToList().ToArray();
            objectiveLabel = root.Q<Label>("objective");
            timer = root.Q<Label>("timer");
            kills = root.Q<Label>("kills");
            alert = root.Q<Label>("alert");
            shells = root.Q<Label>("shells");
            nitroCaption = root.Q<Label>("nitro-caption");
            evacDistance = root.Q<Label>("evac-distance");
            signalCaption = root.Q<Label>("signal-caption");
            modShield = root.Q<Label>("mod-shield");
            armorValue = root.Q<Label>("armor-value");
            modAllies = root.Q<Label>("mod-allies");
            shellsCount = root.Q<Label>("shells-count");
            modTwin = root.Q<Label>("mod-twin");
            modTrack = root.Q<Label>("mod-track");
            tankCards = root.Q("tank-cards");
            bossPanel = root.Q("boss");
            bossFill = root.Q("boss-fill");
            extractCaption = root.Q<Label>("extract-caption");
            bossCaption = root.Q<Label>("boss-caption");
            swatches = root.Q("color-swatches");
            difficultyRow = root.Q("difficulty");

            Bind("play", () => game.StartGame());
            Bind("quit", () => game.Quit());
            Bind("resume", () => game.TogglePause());
            Bind("pause-restart", () => game.Restart());
            Bind("pause-menu", () => game.BackToMenu());
            Bind("end-restart", () => game.Restart());
            Bind("end-menu", () => game.BackToMenu());
        }

        void Start()
        {
            game = GameManager.Instance;
            player = game.Player;
            objective = ObjectiveDirector.Instance;

            game.StateChanged += OnStateChanged;
            game.FlagTaken += OnFlagTaken;
            game.EnemyKilled += _ => kills.text = $"СБИТО {game.Kills}";
            game.Parried += pos => FloatingText(pos, "ОТБИЛ!");
            game.RicochetKill += (pos, bounces) => FloatingText(pos, bounces > 1 ? $"РИКОШЕТ ×{bounces}!" : "РИКОШЕТ!");
            player.Hurt += s => vignetteAlpha = Mathf.Max(vignetteAlpha, 0.25f + s * 0.35f);
            objective.BeepPlayed += PulseSignal;
            player.StatusMessage += msg => ShowAlert(msg, small: true);
            if (PowerupDirector.Instance != null)
                PowerupDirector.Instance.Collected += kind => ShowAlert(SupplyCrate.NameOf(kind) + "!", small: true, color: SupplyCrate.ColorOf(kind));
            objective.BossSpawned += () =>
            {
                ShowBossPhase(0);
                ShowAlert("ВЕРТОЛЁТ!");
            };
            objective.BossPhaseChanged += i =>
            {
                ShowBossPhase(i);
                ShowAlert($"ФАЗА {i + 1}", small: true,
                          color: new Color(1f, 0.6f, 0.25f));
            };
            objective.BossKilled += () =>
            {
                objectiveLabel.text = "ЗАЙМИ ТОЧКУ";
                ShowAlert("СБИТ!", color: new Color(0.67f, 0.78f, 0.4f));
            };
            BuildTankPicker();

            float best = game.BestTime;
            root.Q<Label>("best").text = best > 0f ? $"Рекорд: {FormatTime(best)}" : "";
            OnStateChanged(game.State);
        }

        void Bind(string name, System.Action action)
        {
            root.Q<Button>(name).clicked += () =>
            {
                Audio.Play(Audio.Bank?.uiClick, null, 0.6f);
                action();
            };
        }

        // ------------------------------------------------------------------ state

        void OnStateChanged(GameState state)
        {
            // aiming uses relative mouse movement: lock the cursor in combat, free it for menus
            bool combat = state == GameState.Playing;
            UnityEngine.Cursor.lockState = combat ? CursorLockMode.Locked : CursorLockMode.None;
            UnityEngine.Cursor.visible = !combat;

            Show(menu, state == GameState.Menu);
            Show(hud, state != GameState.Menu);
            Show(pause, state == GameState.Paused);
            Show(end, state == GameState.Lost);

            if (state == GameState.Menu) root.Q<Button>("play").Focus();
            if (state == GameState.Lost) FillEndScreen(false);
            if (state == GameState.Won)
            {
                // a win rolls the end credits first, then the results card
                if (Credits.Instance != null) Credits.Instance.Play(ShowWinScreen);
                else ShowWinScreen();
            }
        }

        void ShowWinScreen()
        {
            if (game.State != GameState.Won) return;
            Show(end, true);
            FillEndScreen(true);
        }

        void FillEndScreen(bool won)
        {
            var title = root.Q<Label>("end-title");
            title.text = won ? "ПОБЕДА" : "ПОДБИТ";
            title.EnableInClassList("lost", !won);
            root.Q<Label>("end-subtitle").text = won ? (game.NewRecord ? "Новый рекорд" : "Флаг доставлен") : "Танк потерян";
            root.Q<Label>("end-subtitle").text += $"  -  Сложность: {Difficulty.Name.ToLower()}";
            root.Q<Label>("stat-time").text = FormatTime(game.ElapsedTime);
            root.Q<Label>("stat-kills").text = game.Kills.ToString();
            root.Q<Label>("stat-ricochet").text = game.RicochetKills.ToString();
            root.Q<Label>("stat-best").text = game.BestTime > 0f ? FormatTime(game.BestTime) : "-";
            root.Q<Button>("end-restart").Focus();
            if (!won) Audio.Play(Audio.Bank?.lose, null, 0.8f, 1f, 0f);
        }

        void OnFlagTaken()
        {
            objectiveLabel.text = "К ЭВАКУАЦИИ";
            signalCaption.text = "ТРЕВОГА";
            ShowAlert("ФЛАГ!");
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (game == null) return;
            HandleKeys();
            if (game.State == GameState.Menu) return;

            int second = (int)game.ElapsedTime;
            if (second != shownSecond)
            {
                shownSecond = second;
                timer.text = FormatTime(second);
            }

            float hp = player.Health.Normalized;
            armorFill.style.width = Length.Percent(hp * 100f);
            int hpValue = Mathf.CeilToInt(player.Health.Current);
            if (hpValue != shownHp)
            {
                shownHp = hpValue;
                armorValue.text = hpValue.ToString();
            }
            armorValue.EnableInClassList("low", hp < 0.35f);
            armorFill.EnableInClassList("low", hp < 0.35f);

            TankAbility ability = player.Ability;
            float charge = ability.Active ? ability.Left01 : ability.Ready01;
            nitroFill.style.width = Length.Percent(charge * 100f);
            nitroFill.EnableInClassList("used", !ability.Available && !ability.Active);
            // -2 running, -1 ready, otherwise whole seconds of cooldown left
            int abilityState = ability.Active ? -2 : ability.Available ? -1 : Mathf.RoundToInt(ability.CooldownLeft);
            if (abilityState != shownAbility)
            {
                shownAbility = abilityState;
                nitroCaption.text = ability.Name + (abilityState == -2 ? "  ▶" : abilityState == -1 ? "  [SHIFT]" : $"  {abilityState}с");
            }

            TankTurret gun = player.Turret;
            int max = gun.MagazineSize;
            int free = gun.Reloading ? 0 : gun.Ammo;
            int reload = gun.Reloading ? Mathf.RoundToInt(gun.ReloadLeft * 10f) : -1;
            if (free != shownAmmo || reload != shownReload)
            {
                shownAmmo = free;
                shownReload = reload;
                shells.text = gun.Reloading ? $"ПЕРЕЗАРЯДКА {reload / 10f:0.0}" : new string('●', free) + new string('○', max - free);
                shellsCount.text = $"{free} / {max}";
                shellsCount.EnableInClassList("low", gun.Reloading);
            }

            UpdateModifiers();

            UpdateSignal();
            UpdateEvac();

            vignetteAlpha = Mathf.Max(0f, vignetteAlpha - Time.unscaledDeltaTime * 1.5f);
            float lowHpPulse = hp < 0.3f && !player.Health.IsDead ? 0.12f + Mathf.Sin(Time.time * 5f) * 0.05f : 0f;
            vignette.style.opacity = Mathf.Max(vignetteAlpha, lowHpPulse);
        }

        void HandleKeys()
        {
            switch (game.State)
            {
                case GameState.Playing:
                case GameState.Paused:
                    if (input.PausePressed) game.TogglePause();
                    if (game.State == GameState.Paused && input.RestartPressed) game.Restart();
                    break;
                case GameState.Won:
                case GameState.Lost:
                    if (input.RestartPressed && !(Credits.Instance != null && Credits.Instance.Rolling)) game.Restart();
                    break;
                case GameState.Menu:
                    if (!Onboarding.Blocking && UnityEngine.InputSystem.Keyboard.current?.enterKey.wasPressedThisFrame == true) game.StartGame();
                    break;
            }
        }

        void UpdateModifiers()
        {
            Health hp = player.Health;
            Show(modShield, hp.HasMangal);
            if (hp.HasMangal && hp.MangalHits != shownMangal)
            {
                shownMangal = hp.MangalHits;
                modShield.text = $"МАНГАЛ ×{shownMangal}";
            }
            Show(modTwin, player.Turret.TwinActive);
            Show(modAllies, AllyDrone.Count > 0);
            if (AllyDrone.Count > 0 && AllyDrone.Count != shownAllies)
            {
                shownAllies = AllyDrone.Count;
                modAllies.text = $"ДРОНЫ ×{shownAllies}";
            }
            Show(modTrack, player.TrackLost && !hp.IsDead);
        }

        // ------------------------------------------------------------------ tank picker (main menu)

        void BuildTankPicker()
        {
            tankCards.Clear();
            TankClass[] classes = player.Classes;
            for (int i = 0; i < classes.Length; i++)
            {
                int index = i;
                TankClass c = classes[i];
                var card = new VisualElement();
                card.AddToClassList("tank-card");

                // the picture is drawn at the same scale for every tank, so sizes compare at a glance
                var box = new VisualElement();
                box.AddToClassList("tank-img-box");
                var img = new VisualElement { name = "img" };
                img.AddToClassList("tank-img");
                if (c.preview != null)
                {
                    img.style.backgroundImage = c.preview;
                    img.style.width = c.preview.width * 1.4f;
                    img.style.height = c.preview.height * 1.4f;
                }
                box.Add(img);
                card.Add(box);

                card.Add(MakeLabel(c.displayName, "tank-name"));
                card.Add(MakeLabel(c.description, "tank-desc"));
                card.Add(StatRow("СКОРОСТЬ", c.speedRating));
                card.Add(StatRow("БРОНЯ", c.armorRating));
                card.Add(StatRow("УРОН", c.fireRating));
                card.Add(MakeLabel("SHIFT - " + TankAbility.NameOf(c.ability), "tank-ability"));
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    TankSelection.PickClass(index, c);
                    OnPickerChanged();
                });
                tankCards.Add(card);
            }

            swatches.Clear();
            for (int i = 0; i < TankSelection.Colors.Length; i++)
            {
                int index = i;
                var swatch = new VisualElement { tooltip = TankSelection.ColorNames[i] };
                swatch.AddToClassList("swatch");
                swatch.style.backgroundColor = TankSelection.Colors[i];
                swatch.RegisterCallback<ClickEvent>(_ =>
                {
                    TankSelection.ColorIndex = index;
                    OnPickerChanged();
                });
                swatches.Add(swatch);
            }

            difficultyRow.Clear();
            for (int i = 0; i < Difficulty.Names.Length; i++)
            {
                int index = i;
                var btn = new Label(Difficulty.Names[i]);
                btn.AddToClassList("diff-btn");
                btn.RegisterCallback<ClickEvent>(_ =>
                {
                    Difficulty.Current = (DifficultyLevel)index;
                    OnPickerChanged();
                });
                difficultyRow.Add(btn);
            }
            RefreshPicker();
        }

        void OnPickerChanged()
        {
            Audio.Play(Audio.Bank?.uiClick, null, 0.6f);
            player.ApplySelection();
            RefreshPicker();
        }

        void RefreshPicker()
        {
            int cls = TankSelection.ClassIndex;
            TankClass[] classes = player.Classes;
            for (int i = 0; i < tankCards.childCount; i++)
            {
                tankCards[i].EnableInClassList("selected", i == cls);
                // the chosen tank shows the picked colour, the others their own default
                Color tint = i == cls ? TankSelection.Color : TankSelection.Colors[classes[i].defaultColorIndex];
                tankCards[i].Q("img").style.unityBackgroundImageTintColor = tint;
            }
            int col = TankSelection.ColorIndex;
            for (int i = 0; i < swatches.childCount; i++) swatches[i].EnableInClassList("selected", i == col);
            int diff = (int)Difficulty.Current;
            for (int i = 0; i < difficultyRow.childCount; i++) difficultyRow[i].EnableInClassList("selected", i == diff);
            float best = game.BestTime;
            root.Q<Label>("best").text = best > 0f ? $"Рекорд ({Difficulty.Name.ToLower()}): {FormatTime(best)}" : "";
        }

        static VisualElement StatRow(string name, int rating)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");
            row.Add(MakeLabel(name, "stat-name"));
            for (int i = 0; i < 3; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("pip");
                pip.EnableInClassList("on", i < rating);
                row.Add(pip);
            }
            return row;
        }

        static Label MakeLabel(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        // ------------------------------------------------------------------ objective HUD

        void UpdateSignal()
        {
            float s = game.HasFlag ? 0f : objective.SignalStrength;
            int lit = Mathf.CeilToInt(s * bars.Length - 0.15f);
            for (int i = 0; i < bars.Length; i++)
            {
                bool on = game.HasFlag ? Mathf.Repeat(Time.time * 2f, 1f) < 0.5f : i < lit;
                bars[i].EnableInClassList("on", on);
            }
            if (!game.HasFlag && objective.FlagSpotted && objectiveLabel.text.StartsWith("НАЙДИ"))
                objectiveLabel.text = "ФЛАГ РЯДОМ";
        }

        void PulseSignal()
        {
            signalBars.AddToClassList("pulse");
            signalBars.schedule.Execute(() => signalBars.RemoveFromClassList("pulse")).StartingIn(90);
        }

        void UpdateEvac()
        {
            EvacZone evac = objective.Evac;
            bool active = game.HasFlag && evac.gameObject.activeInHierarchy;
            Show(extract, active && (evac.PlayerInside || evac.Progress > 0f));
            if (!active)
            {
                Show(evacArrow, false);
                return;
            }
            extractFill.style.width = Length.Percent(evac.Progress * 100f);
            extractCaption.text = evac.Locked ? "СНАЧАЛА ВЕРТОЛЁТ" : "ЭВАКУАЦИЯ";

            Show(bossPanel, objective.BossAlive);
            if (objective.BossAlive) bossFill.style.width = Length.Percent(objective.Boss.Health.Normalized * 100f);

            // off-screen pointer toward the extraction point
            Vector3 vp = cam.WorldToViewportPoint(evac.transform.position);
            bool onScreen = vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.08f && vp.y < 0.92f;
            Show(evacArrow, !onScreen);
            if (onScreen) return;

            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 dir = ((Vector2)vp - center);
            if (vp.z < 0) dir = -dir;
            float k = Mathf.Min(0.42f / Mathf.Max(Mathf.Abs(dir.x), 0.001f), 0.4f / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
            Vector2 edge = center + dir * k;

            float w = root.layout.width, h = root.layout.height;
            evacArrow.style.left = edge.x * w - 32f;
            evacArrow.style.top = (1f - edge.y) * h - 32f;
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            evacArrowHead.style.rotate = new Rotate(angle);
            int meters = Mathf.RoundToInt(Vector2.Distance(player.transform.position, evac.transform.position) * 4f);
            if (meters != shownMeters)
            {
                shownMeters = meters;
                evacDistance.text = $"{meters} м";
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Text that pops at a world position, rises and fades (celebrates bank shots).</summary>
        void FloatingText(Vector2 world, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("float-text");
            hud.Add(label);
            StartCoroutine(FloatRoutine(label, world));
        }

        IEnumerator FloatRoutine(Label label, Vector2 world)
        {
            const float duration = 1.1f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Vector3 vp = cam.WorldToViewportPoint(world + Vector2.up * t * 1.2f);
                label.style.left = vp.x * root.layout.width - 120f;
                label.style.top = (1f - vp.y) * root.layout.height - 20f;
                label.style.opacity = 1f - Mathf.Clamp01((t - 0.6f) / 0.5f);
                label.style.scale = new Scale(Vector3.one * (1f + Mathf.Max(0f, 0.25f - t) * 2f));
                yield return null;
            }
            label.RemoveFromHierarchy();
        }

        void ShowBossPhase(int phase)
        {
            HelicopterBoss armor = objective.BossArmor;
            if (armor == null) return;
            objectiveLabel.text = $"СБЕЙ ВЕРТОЛЁТ - БЕЙ В {armor.Current.weakSpotName}";
            bossCaption.text = $"ВЕРТОЛЁТ - ФАЗА {phase + 1}/{armor.PhaseCount}";
        }

        void ShowAlert(string text, bool small = false, Color? color = null)
        {
            alert.text = text;
            alert.EnableInClassList("small", small);
            // the accent stripe on the plate takes the same colour as the text
            StyleColor c = color.HasValue ? new StyleColor(color.Value) : new StyleColor(StyleKeyword.Null);
            alert.style.color = c;
            alert.style.borderLeftColor = c;
            alert.AddToClassList("show");
            alertTimer?.Pause();
            alertTimer = alert.schedule.Execute(() => alert.RemoveFromClassList("show")).StartingIn(2000);
        }

        IVisualElementScheduledItem alertTimer;

        static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        static string FormatTime(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
    }
}
