using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Flow field toward the player: a breadth-first search over the map grid, refreshed a few
    /// times per second (only when the tank has moved to another cell). Every drone just walks
    /// "downhill", so 30 drones cost the same as one.
    /// </summary>
    public class NavGrid : MonoBehaviour
    {
        public static NavGrid Instance { get; private set; }

        [SerializeField] LevelBuilder level;
        [SerializeField] Transform target;
        [SerializeField] float refreshInterval = 0.25f;

        public LevelMap Map { get; private set; }

        int[,] distance;
        readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        float timer;
        Vector2Int builtFor = new(int.MinValue, int.MinValue);

        static readonly Vector2Int[] Neighbours =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        void Awake()
        {
            Instance = this;
            Map = new LevelMap(level.MapFile.text);
            distance = new int[Map.Width, Map.Height];
            Rebuild();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            if (Map.WorldToCell(target.position) != builtFor) Rebuild();
        }

        /// <summary>Recomputes the field right away (e.g. after the tank was teleported to its start).</summary>
        public void Refresh() => Rebuild();

        void Rebuild()
        {
            builtFor = Map.WorldToCell(target.position);
            for (int x = 0; x < Map.Width; x++)
                for (int y = 0; y < Map.Height; y++) distance[x, y] = int.MaxValue;

            Vector2Int start = NearestWalkable(Map.WorldToCell(target.position));
            distance[start.x, start.y] = 0;
            frontier.Clear();
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                Vector2Int c = frontier.Dequeue();
                int next = distance[c.x, c.y] + 1;
                foreach (Vector2Int d in Neighbours)
                {
                    Vector2Int n = c + d;
                    if (!CanStep(c, d) || distance[n.x, n.y] <= next) continue;
                    distance[n.x, n.y] = next;
                    frontier.Enqueue(n);
                }
            }
        }

        /// <summary>Diagonal steps are only allowed when both side cells are open (no corner cutting).</summary>
        bool CanStep(Vector2Int from, Vector2Int d)
        {
            Vector2Int to = from + d;
            if (!Map.IsWalkable(to.x, to.y)) return false;
            if (d.x != 0 && d.y != 0)
                return Map.IsWalkable(from.x + d.x, from.y) && Map.IsWalkable(from.x, from.y + d.y);
            return true;
        }

        /// <summary>Steps (cells) to the player, or int.MaxValue if unreachable.</summary>
        public int DistanceAt(Vector2 world)
        {
            Vector2Int c = Map.WorldToCell(world);
            return Map.InBounds(c.x, c.y) ? distance[c.x, c.y] : int.MaxValue;
        }

        /// <summary>Direction to move from <paramref name="world"/> to get closer to the player.</summary>
        public Vector2 FlowDirection(Vector2 world)
        {
            Vector2Int c = NearestWalkable(Map.WorldToCell(world));
            int best = distance[c.x, c.y];
            Vector2Int bestCell = c;
            foreach (Vector2Int d in Neighbours)
            {
                if (!CanStep(c, d)) continue;
                Vector2Int n = c + d;
                if (distance[n.x, n.y] < best)
                {
                    best = distance[n.x, n.y];
                    bestCell = n;
                }
            }
            Vector2 to = Map.CellCenter(bestCell) - world;
            return to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.zero;
        }

        public Vector2Int NearestWalkable(Vector2Int c)
        {
            if (Map.IsWalkable(c.x, c.y)) return c;
            for (int r = 1; r < 4; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Map.IsWalkable(c.x + dx, c.y + dy)) return new Vector2Int(c.x + dx, c.y + dy);
            return new Vector2Int(Mathf.Clamp(c.x, 1, Map.Width - 2), Mathf.Clamp(c.y, 1, Map.Height - 2));
        }
    }
}
