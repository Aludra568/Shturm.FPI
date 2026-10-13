using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>The tank class and colour picked in the menu, remembered between launches.</summary>
    public static class TankSelection
    {
        const string ClassKey = "RicochetBerlin.TankClass";
        const string ColorKey = "RicochetBerlin.TankColor";

        public static readonly Color[] Colors =
        {
            new(0.45f, 0.72f, 0.38f), // olive green
            new(0.86f, 0.76f, 0.52f), // desert sand
            new(0.55f, 0.6f, 0.68f),  // panzer grey
            new(0.4f, 0.6f, 0.9f),    // steel blue
            new(0.88f, 0.38f, 0.32f), // red
            new(0.92f, 0.92f, 0.9f),  // winter white
        };

        public static readonly string[] ColorNames = { "Олива", "Пустыня", "Сталь", "Лёд", "Кармин", "Зима" };

        public static int ClassIndex
        {
            get => PlayerPrefs.GetInt(ClassKey, 1);
            set { PlayerPrefs.SetInt(ClassKey, value); PlayerPrefs.Save(); }
        }

        public static int ColorIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ColorKey, 0), 0, Colors.Length - 1);
            set { PlayerPrefs.SetInt(ColorKey, value); PlayerPrefs.Save(); }
        }

        public static Color Color => Colors[ColorIndex];

        /// <summary>Picks a tank class and gives it that tank's default colour.</summary>
        public static void PickClass(int index, TankClass tank)
        {
            ClassIndex = index;
            ColorIndex = tank.defaultColorIndex;
        }
    }
}
