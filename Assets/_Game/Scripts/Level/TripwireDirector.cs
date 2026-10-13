using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Each round, strings a few tripwires across narrow passages (breaches in the ruins), away from
    /// the start, the flag and the evac point.
    /// </summary>
    public class TripwireDirector : MonoBehaviour
    {
        [SerializeField] Tripwire tripwirePrefab;
        [SerializeField] int count = 12;
        [SerializeField] float minSpacing = 5f;
        [SerializeField] float keepAwayFromStart = 7f;
        [SerializeField] float keepAwayFromObjectives = 4f;
        [Tooltip("Only passages this narrow (cells) get a wire.")]
        [SerializeField] int maxWidth = 4;

        struct Span { public Vector2 a, b; }

        IEnumerator Start()
        {
            yield return null; // the player and objectives are placed during Start
            LevelMap map = NavGrid.Instance.Map;
            var spans = FindPassages(map);
            Shuffle(spans);

            Vector2 start = GameManager.Instance.Player.transform.position;
            var avoid = new List<Vector2>();
            foreach (FlagPickup f in FindObjectsByType<FlagPickup>(FindObjectsInactive.Include)) avoid.Add(f.transform.position);
            avoid.Add(ObjectiveDirector.Instance.Evac.transform.position);

            var placed = new List<Vector2>();
            foreach (Span s in spans)
            {
                if (placed.Count >= count) break;
                Vector2 mid = (s.a + s.b) * 0.5f;
                if (Vector2.Distance(mid, start) < keepAwayFromStart) continue;
                if (avoid.Exists(p => Vector2.Distance(p, mid) < keepAwayFromObjectives)) continue;
                if (placed.Exists(p => Vector2.Distance(p, mid) < minSpacing)) continue;
                Instantiate(tripwirePrefab, transform).Setup(s.a, s.b);
                placed.Add(mid);
            }
        }

        /// <summary>
        /// Gaps 2..maxWidth cells wide between two building walls, where the walls continue on both
        /// sides - i.e. you are inside a short tunnel, not at an open corner.
        /// </summary>
        List<Span> FindPassages(LevelMap map)
        {
            var result = new List<Span>();
            for (int y = 2; y < map.Height - 2; y++)
                for (int x = 2; x < map.Width - 2; x++)
                {
                    // wire running along x (across a passage that goes up/down)
                    if (map[x - 1, y] == '#' && map.IsWalkable(x, y))
                    {
                        int len = 0;
                        while (len <= maxWidth && map.IsWalkable(x + len, y)) len++;
                        if (len >= 2 && len <= maxWidth && map[x + len, y] == '#' &&
                            map[x - 1, y + 1] == '#' && map[x - 1, y - 1] == '#' &&
                            map[x + len, y + 1] == '#' && map[x + len, y - 1] == '#')
                            result.Add(new Span { a = new Vector2(x, y + 0.5f), b = new Vector2(x + len, y + 0.5f) });
                    }
                    // wire running along y (across a passage that goes left/right)
                    if (map[x, y - 1] == '#' && map.IsWalkable(x, y))
                    {
                        int len = 0;
                        while (len <= maxWidth && map.IsWalkable(x, y + len)) len++;
                        if (len >= 2 && len <= maxWidth && map[x, y + len] == '#' &&
                            map[x + 1, y - 1] == '#' && map[x - 1, y - 1] == '#' &&
                            map[x + 1, y + len] == '#' && map[x - 1, y + len] == '#')
                            result.Add(new Span { a = new Vector2(x + 0.5f, y), b = new Vector2(x + 0.5f, y + len) });
                    }
                }
            return result;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
