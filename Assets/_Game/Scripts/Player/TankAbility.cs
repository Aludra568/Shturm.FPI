using System;
using UnityEngine;

namespace RicochetBerlin
{
    public enum AbilityKind { Nitro, Smoke, ActiveArmor }

    /// <summary>
    /// The tank's special (Shift) with a 30 s cooldown, depending on the class:
    /// Lynx - nitro boost, Wolf - smoke screen that blinds drones, Bear - active armour (damage -75%).
    /// </summary>
    public class TankAbility : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] SmokeScreen smokePrefab;
        [Tooltip("Seconds before the special can be used again (counted from when it ends).")]
        [SerializeField] float cooldown = 30f;
        [Header("Nitro")]
        [SerializeField] float nitroDuration = 5f;
        [Header("Smoke screen")]
        [SerializeField] float smokeDuration = 7f;
        [Header("Active armour")]
        [SerializeField] float armorDuration = 6f;
        [SerializeField, Range(0, 1)] float armorDamageScale = 0.25f;

        public AbilityKind Kind { get; private set; }
        public bool Active => left > 0f;
        public bool Available => !Active && cooldownLeft <= 0f;
        public float Left01 => duration > 0f ? left / duration : 0f;
        public float CooldownLeft => cooldownLeft;
        /// <summary>0 right after use, 1 when ready again.</summary>
        public float Ready01 => Active ? 0f : 1f - cooldownLeft / cooldown;

        public static string NameOf(AbilityKind kind) => kind switch
        {
            AbilityKind.Nitro => "НИТРО",
            AbilityKind.Smoke => "ДЫМ",
            _ => "БРОНЯ"
        };

        public string Name => NameOf(Kind);

        public event Action<AbilityKind> Started;
        public event Action<AbilityKind> Ended;

        PlayerTank tank;
        float left;
        float duration;
        float cooldownLeft;

        void Awake() => tank = GetComponent<PlayerTank>();

        /// <summary>Set by the tank class picked in the menu.</summary>
        public void Configure(AbilityKind kind) => Kind = kind;

        void Update()
        {
            GameManager gm = GameManager.Instance;
            bool playing = gm == null || gm.IsPlaying;
            if (playing && Available && input.AbilityPressed && !tank.Health.IsDead) Activate();
            if (cooldownLeft > 0f && !Active)
            {
                cooldownLeft -= Time.deltaTime;
                if (cooldownLeft <= 0f) Audio.Play(Audio.Bank?.reloaded, transform.position, 0.6f, 1.4f);
            }

            if (left <= 0f) return;
            left -= Time.deltaTime;
            if (left <= 0f) Finish();
        }

        void Activate()
        {
            duration = Kind switch
            {
                AbilityKind.Nitro => nitroDuration,
                AbilityKind.Smoke => smokeDuration,
                _ => armorDuration
            };
            left = duration;

            switch (Kind)
            {
                case AbilityKind.Nitro:
                    tank.Controller.SetBoost(true);
                    break;
                case AbilityKind.Smoke:
                    Instantiate(smokePrefab, transform.position, Quaternion.identity).Init(smokeDuration);
                    break;
                case AbilityKind.ActiveArmor:
                    tank.Health.DamageScale = armorDamageScale;
                    break;
            }
            Started?.Invoke(Kind);
        }

        void Finish()
        {
            left = 0f;
            cooldownLeft = cooldown;
            if (Kind == AbilityKind.Nitro) tank.Controller.SetBoost(false);
            if (Kind == AbilityKind.ActiveArmor) tank.Health.DamageScale = 1f;
            Ended?.Invoke(Kind);
        }
    }
}
