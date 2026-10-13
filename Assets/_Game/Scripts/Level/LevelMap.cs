using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Text map parsed into a grid. One character = one 1x1 world unit cell.
    /// Row 0 of the text is the top of the map; world (0,0) is the bottom-left corner.
    /// <code>
    /// #  building (blocks move/bullets/sight)   r  rubble (blocks move/bullets, see over it)
    /// /  \ fallen slab, diagonal (ricochet!)     .  street     ,  courtyard
    /// c  crate   x  tank trap   b  explosive barrel   o  crater   w  wire   t  dead tree   f  fire
    /// M  manhole (drones climb out)   S  player start   F  possible flag spot   E  possible evac spot
    /// </code>
    /// </summary>
    public class LevelMap
    {
        public readonly int Width;
        public readonly int Height;
        readonly char[,] cells; // [x, y] with y = 0 at the bottom

        public LevelMap(string text)
        {
            string[] lines = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            Height = lines.Length;
            foreach (string line in lines) Width = Mathf.Max(Width, line.Length);
            cells = new char[Width, Height];
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                    cells[x, y] = x < lines[row].Length ? lines[row][x] : '#';
            }
        }

        public char this[int x, int y] => InBounds(x, y) ? cells[x, y] : '#';

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public static bool IsBlocking(char c) => c == '#' || c == 'r' || c == '/' || c == '\\' || c == 'c' || c == 'x' || c == 'b';

        public bool IsWalkable(int x, int y) => InBounds(x, y) && !IsBlocking(cells[x, y]);

        public Vector2 CellCenter(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);
        public Vector2 CellCenter(Vector2Int c) => CellCenter(c.x, c.y);

        public Vector2Int WorldToCell(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        public List<Vector2Int> FindAll(char c)
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (cells[x, y] == c) result.Add(new Vector2Int(x, y));
            return result;
        }

        /// <summary>
        /// Greedy-merges cells matching the predicate into as few rectangles as possible
        /// (fewer colliders = fewer seams for bullets to catch on).
        /// </summary>
        public List<RectInt> MergeRects(System.Func<char, bool> match)
        {
            var used = new bool[Width, Height];
            var rects = new List<RectInt>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (used[x, y] || !match(cells[x, y])) continue;
                    int w = 1;
                    while (x + w < Width && !used[x + w, y] && match(cells[x + w, y])) w++;
                    int h = 1;
                    while (y + h < Height)
                    {
                        bool rowOk = true;
                        for (int i = 0; i < w; i++)
                            if (used[x + i, y + h] || !match(cells[x + i, y + h])) { rowOk = false; break; }
                        if (!rowOk) break;
                        h++;
                    }
                    for (int j = 0; j < h; j++)
                        for (int i = 0; i < w; i++) used[x + i, y + j] = true;
                    rects.Add(new RectInt(x, y, w, h));
                }
            }
            return rects;
        }
    }
}
