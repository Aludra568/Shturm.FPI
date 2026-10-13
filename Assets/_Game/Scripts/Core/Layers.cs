using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Physics layer indices and masks shared by gameplay code (set up by the editor builder).</summary>
    public static class Layers
    {
        public const int Walls = 8;   // ruined buildings: block movement, bullets and sight
        public const int Cover = 9;   // rubble, crates, barrels: block movement and bullets, NOT sight
        public const int Player = 10;
        public const int Enemy = 11;

        public static readonly int SightBlockers = 1 << Walls;
        public static readonly int Solid = (1 << Walls) | (1 << Cover);
        public static readonly int Projectile = (1 << Walls) | (1 << Cover) | (1 << Player) | (1 << Enemy);
    }

    /// <summary>Sorting orders on the single Default sorting layer (keeps every Light2D affecting everything).</summary>
    public static class SortOrder
    {
        public const int Ground = -100;
        public const int Courtyard = -99;
        public const int Decals = -80;
        public const int Tracks = -70;
        public const int Props = -20;
        public const int Units = 0;
        public const int Turret = 5;
        public const int Projectiles = 20;
        public const int Walls = 30;
        public const int Fx = 50;
        public const int Markers = 80;
    }
}
