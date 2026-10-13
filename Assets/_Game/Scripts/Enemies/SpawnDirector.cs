using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Brings drones in from two "layers" of the city: out of manholes (underground) and off rooftops.
    /// Always out of sight but within earshot. Pressure stays roughly steady: a small fixed number of
    /// drones on the map, and they arrive one at a time so each can be dealt with.
    /// The round opens with a small ambush: a few drones already waiting just around the corners.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        [System.Serializable]
        public class DroneType
        {
            public Drone prefab;
            [Tooltip("Seconds into the round before this type can appear.")]
            public float unlockTime;
            public float weight = 1f;
            public int maxAlive = 99;
        }

        [SerializeField] DroneType[] types;
        [SerializeField] Transform markersRoot;
        [Header("Pacing")]
        [SerializeField] float firstSpawnDelay = 5f;
        [Tooltip("Pause between two arrivals - drones never come in packs.")]
        [SerializeField] float interval = 5f;
        [SerializeField] int startAlive = 5;
        [Tooltip("Gentle ramp: one extra drone by the end of a long round.")]
        [SerializeField] int endAlive = 6;
        [SerializeField] float rampDuration = 240f;
        [SerializeField] int flagBonusAlive = 0;
        [Header("Placement")]
        [SerializeField] float minDistance = 8f;
        [SerializeField] float maxDistance = 19f;
        [SerializeField] float hatchWarning = 0.9f;
        [Header("Opening ambush")]
        [Tooltip("Drones already waiting near the start when the round begins (inclusive range).")]
        [SerializeField] int ambushMin = 3;
        [SerializeField] int ambushMax = 5;
        [Tooltip("Path length to the player, in cells (1 m): near, but not on top of the tank.")]
        [SerializeField] int ambushMinPath = 7;
        [SerializeField] int ambushMaxPath = 16;
        [SerializeField] float ambushMinStraight = 5f;
        [SerializeField] float ambushSpacing = 3.5f;

        readonly List<Vector2> hatches = new();
        readonly List<Vector2> rooftops = new();
        readonly List<Vector2> candidates = new();
        float timer;
        bool spawning;
        bool ambushDone;
        Transform player;

        void Start()
        {
            player = GameManager.Instance.Player.transform;
            foreach (MapMarker m in markersRoot.GetComponentsInChildren<MapMarker>())
                if (m.kind == MarkerKind.Hatch) hatches.Add(m.transform.position);
            CollectRooftopEdges();
            timer = firstSpawnDelay;
            GameManager.Instance.FlagTaken += OnFlagTaken;
        }

        /// <summary>Street cells next to a building: a drone "jumping off the roof" lands there.</summary>
        void CollectRooftopEdges()
        {
            LevelMap map = NavGrid.Instance.Map;
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                {
                    if (!map.IsWalkable(x, y)) continue;
                    bool nearRoof = map[x + 1, y] == '#' || map[x - 1, y] == '#' || map[x, y + 1] == '#' || map[x, y - 1] == '#';
                    bool roomy = map.IsWalkable(x + 1, y) || map.IsWalkable(x - 1, y);
                    if (nearRoof && roomy && (x + y) % 3 == 0) rooftops.Add(map.CellCenter(x, y));
                }
        }

        int TargetAlive
        {
            get
            {
                GameManager gm = GameManager.Instance;
                float ramp = Mathf.Clamp01(gm.ElapsedTime / rampDuration);
                return Mathf.RoundToInt(Mathf.Lerp(startAlive, endAlive, ramp)) + (gm.HasFlag ? flagBonusAlive : 0) +
                       Difficulty.ExtraDrones;
            }
        }

        void Update()
        {
            if (!GameManager.Instance.IsPlaying || spawning) return;
            if (!ambushDone)
            {
                ambushDone = true;
                SpawnAmbush();
            }
            // the boss fight is a duel: no reinforcements while the evac guardian is up
            if (ObjectiveDirector.Instance != null && ObjectiveDirector.Instance.BossAlive) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (Drone.RegularAlive >= TargetAlive)
            {
                timer = 1f; // full house: check again shortly, the next one still waits a full interval
                return;
            }
            // a failed placement retries soon instead of leaving the streets empty for a whole interval
            timer = TrySpawn(GameManager.Instance.ElapsedTime) ? interval * Difficulty.SpawnInterval * Random.Range(0.85f, 1.15f) : 1f;
        }

        /// <summary>
        /// Places the opening drones on street cells a short walk from the tank but hidden behind walls,
        /// spread apart so they come from different corners. They are simply there - no hatch, no drop.
        /// </summary>
        void SpawnAmbush()
        {
            // after a restart the round starts on the first frame - make sure distances are from the real start
            NavGrid.Instance.Refresh();
            LevelMap map = NavGrid.Instance.Map;
            Vector2 p = player.position;
            candidates.Clear();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (!map.IsWalkable(x, y)) continue;
                    Vector2 c = map.CellCenter(x, y);
                    int path = NavGrid.Instance.DistanceAt(c);
                    if (path < ambushMinPath || path > ambushMaxPath) continue;
                    if (Vector2.Distance(c, p) < ambushMinStraight) continue;
                    // around a corner: walls hide the whole body, not just its centre
                    if (!HiddenBehindWalls(p, c)) continue;
                    if (VisionSystem.Instance.CanSee(c, 0.5f)) continue;
                    candidates.Add(c);
                }

            int want = Random.Range(ambushMin, ambushMax + 1);
            var picked = new List<Vector2>();
            while (picked.Count < want && candidates.Count > 0)
            {
                int i = Random.Range(0, candidates.Count);
                Vector2 c = candidates[i];
                candidates.RemoveAt(i);
                bool crowded = false;
                foreach (Vector2 q in picked)
                    if (Vector2.Distance(q, c) < ambushSpacing) { crowded = true; break; }
                if (crowded) continue;
                picked.Add(c);

                DroneType type = PickType(0f);
                if (type == null) break;
                Drone drone = Instantiate(type.prefab, c, Quaternion.identity);
                drone.name = type.prefab.name;
            }
            // the regular trickle picks up after a normal pause
            timer = interval * Difficulty.SpawnInterval;
        }

        static bool HiddenBehindWalls(Vector2 from, Vector2 point)
        {
            Vector2 side = Vector2.Perpendicular((point - from).normalized) * 0.8f;
            return Physics2D.Linecast(from, point, Layers.SightBlockers) &&
                   Physics2D.Linecast(from, point + side, Layers.SightBlockers) &&
                   Physics2D.Linecast(from, point - side, Layers.SightBlockers);
        }

        void OnFlagTaken() => Audio.Play(Audio.Bank?.alarm, null, 0.7f, 1f, 0f);

        bool TrySpawn(float time)
        {
            DroneType type = PickType(time);
            if (type == null) return false;

            bool fromHatch = Random.value < 0.5f;
            if (!PickPoint(fromHatch ? hatches : rooftops, out Vector2 point) &&
                !PickPoint(fromHatch ? rooftops : hatches, out point)) return false;
            if (!hatches.Contains(point)) fromHatch = false;

            spawning = true;
            StartCoroutine(SpawnRoutine(type.prefab, point, fromHatch));
            return true;
        }

        /// <summary>Weighted pick that favours types with fewer drones alive, so the mix stays varied.</summary>
        DroneType PickType(float time)
        {
            float total = 0f;
            foreach (DroneType t in types)
                if (Available(t, time)) total += Weight(t);
            if (total <= 0f) return null;
            float r = Random.value * total;
            foreach (DroneType t in types)
            {
                if (!Available(t, time)) continue;
                r -= Weight(t);
                if (r <= 0f) return t;
            }
            return null;
        }

        static float Weight(DroneType t) => t.weight / (1f + AliveCount(t) * 0.8f);

        static int AliveCount(DroneType t)
        {
            int alive = 0;
            foreach (Drone d in Drone.Alive)
                if (d.name == t.prefab.name) alive++;
            return alive;
        }

        static bool Available(DroneType t, float time) => time >= t.unlockTime && AliveCount(t) < t.maxAlive;

        bool PickPoint(List<Vector2> pool, out Vector2 point)
        {
            candidates.Clear();
            Vector2 p = player.position;
            foreach (Vector2 c in pool)
            {
                float d = Vector2.Distance(c, p);
                if (d < minDistance || d > maxDistance * 1.4f) continue;
                if (VisionSystem.Instance.CanSee(c, 0.5f)) continue;
                if (NavGrid.Instance.DistanceAt(c) == int.MaxValue) continue;
                candidates.Add(c);
            }
            point = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : default;
            return candidates.Count > 0;
        }

        IEnumerator SpawnRoutine(Drone prefab, Vector2 point, bool fromHatch)
        {
            if (fromHatch)
            {
                // the hatch lid rattles: you hear it, and see the dust if it is in your light
                Audio.Play(Audio.Bank?.hatchOpen, point, 0.8f);
                float t = 0f;
                while (t < hatchWarning)
                {
                    Fx.Instance?.Dust(point, Vector2.up, 2);
                    t += 0.3f;
                    yield return new WaitForSeconds(0.3f);
                }
            }
            spawning = false;
            if (!GameManager.Instance.IsPlaying) yield break;
            Drone drone = Instantiate(prefab, point, Quaternion.identity);
            drone.name = prefab.name;
            drone.Emerge(fromHatch ? EmergeFrom.Hatch : EmergeFrom.Rooftop);
        }
    }
}
