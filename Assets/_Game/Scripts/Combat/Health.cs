using System;
using UnityEngine;

namespace RicochetBerlin
{
    public enum Team { Player, Enemy, Neutral }

    /// <summary>Hit points for anything that can be shot: the tank, drones, explosive barrels.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] Team team = Team.Neutral;
        [SerializeField] float maxHealth = 1f;

        public Team Team => team;
        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => Current / maxHealth;
        public bool IsDead { get; private set; }
        public bool Invulnerable { get; private set; }
        /// <summary>Anti-drone "mangal" cage: soaks up whole hits until its slats are gone.</summary>
        public int MangalHits { get; private set; }
        public bool HasMangal => MangalHits > 0;
        /// <summary>Incoming damage multiplier (the Bear's active armour lowers it).</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>Damage amount and the direction the hit came from.</summary>
        public event Action<float, Vector2> Damaged;
        public event Action Died;
        public event Action<float> Healed;
        /// <summary>A hit that the mangal cage absorbed (direction of the hit).</summary>
        public event Action<Vector2> MangalBlocked;

        void Awake() => Current = maxHealth;

        public void SetInvulnerable(bool value) => Invulnerable = value;

        /// <summary>Changes max HP (tank class picked in the menu) and refills.</summary>
        public void SetMax(float max)
        {
            maxHealth = max;
            Current = max;
        }

        public void AddMangal(int hits) => MangalHits = Mathf.Max(MangalHits, hits);

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Healed?.Invoke(amount);
        }

        public void TakeDamage(float amount, Vector2 direction)
        {
            if (IsDead || Invulnerable || amount <= 0f) return;
            if (HasMangal)
            {
                MangalHits--;
                MangalBlocked?.Invoke(direction);
                return;
            }
            amount *= DamageScale;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, direction);
            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }
    }
}
