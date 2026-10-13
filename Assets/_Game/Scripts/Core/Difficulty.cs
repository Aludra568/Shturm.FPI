using UnityEngine;

namespace RicochetBerlin
{
    public enum DifficultyLevel { Easy, Normal, Hard }

    /// <summary>
    /// Difficulty picked in the menu (remembered). Easy is the base balance; harder levels make
    /// drones hit harder, come in larger numbers, more often and a little faster, and toughen the boss.
    /// </summary>
    public static class Difficulty
    {
        const string Key = "RicochetBerlin.Difficulty";

        public static readonly string[] Names = { "ЛЁГКАЯ", "СРЕДНЯЯ", "ТЯЖЁЛАЯ" };

        public static DifficultyLevel Current
        {
            get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, 2);
            set { PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.Save(); }
        }

        public static string Name => Names[(int)Current];

        /// <summary>Multiplier on all damage dealt by drones and the boss.</summary>
        public static float EnemyDamage => Pick(1f, 1.35f, 1.7f);
        /// <summary>Multiplier on drone movement speed.</summary>
        public static float EnemySpeed => Pick(1f, 1.1f, 1.2f);
        /// <summary>Extra drones kept on the map at once.</summary>
        public static int ExtraDrones => (int)Pick(0, 2, 4);
        /// <summary>Multiplier on the pause between drone arrivals.</summary>
        public static float SpawnInterval => Pick(1f, 0.8f, 0.65f);
        /// <summary>Multiplier on the evac guardian's health.</summary>
        public static float BossHealth => Pick(1f, 1.25f, 1.5f);

        static float Pick(float easy, float normal, float hard) => Current switch
        {
            DifficultyLevel.Normal => normal,
            DifficultyLevel.Hard => hard,
            _ => easy
        };
    }
}
