using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Round goal: the tank starts at a random street far from the flag, finds the flag hidden at one
    /// of several spots (guided only by a radio direction-finder beep), then drives to the extraction
    /// point - where the evac guardian (the boss drone) drops in. Kill it, then hold the point.
    /// </summary>
    public class ObjectiveDirector : MonoBehaviour
    {
        public static ObjectiveDirector Instance { get; private set; }

        [SerializeField] Transform markersRoot;
        [SerializeField] FlagPickup flag;
        [SerializeField] EvacZone evac;
        [Header("Placement")]
        [SerializeField] float minPlayerFlagDistance = 35f;
        [SerializeField] float maxPlayerFlagDistance = 62f;
        [SerializeField] float minEvacDistanceFromFlag = 22f;
        [Header("Boss: evac guardian")]
        [SerializeField] Drone bossPrefab;
        [Tooltip("The guardian drops in when the flag carrier gets this close to the evac point.")]
        [SerializeField] float bossTriggerDistance = 13f;
        [Header("Radio direction finder")]
        [SerializeField] float signalRange = 85f;
        [SerializeField] float slowBeepInterval = 1.6f;
        [SerializeField] float fastBeepInterval = 0.16f;

        /// <summary>0 = far away, 1 = right on top of the flag. Direction is deliberately not given.</summary>
        public float SignalStrength { get; private set; }
        public bool FlagSpotted { get; private set; }
        public EvacZone Evac => evac;
        public Drone Boss { get; private set; }
        public bool BossAlive => Boss != null && !Boss.Health.IsDead;
        public bool BossDefeated { get; private set; }

        public event System.Action BeepPlayed;
        public event System.Action BossSpawned;
        public event System.Action BossKilled;
        /// <summary>Boss moved to a new phase (index) - the HUD names the new weak spot.</summary>
        public event System.Action<int> BossPhaseChanged;
        public HelicopterBoss BossArmor { get; private set; }

        Transform player;
        float beepTimer;
        bool bossTriggered;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            player = GameManager.Instance.Player.transform;
            var flagSpots = new List<Vector2>();
            var evacSpots = new List<Vector2>();
            foreach (MapMarker m in markersRoot.GetComponentsInChildren<MapMarker>())
            {
                if (m.kind == MarkerKind.FlagSpot) flagSpots.Add(m.transform.position);
                if (m.kind == MarkerKind.EvacSpot) evacSpots.Add(m.transform.position);
            }

            Vector2 flagPos = flagSpots[Random.Range(0, flagSpots.Count)];
            Vector2 evacPos = PickFar(evacSpots, flagPos, minEvacDistanceFromFlag);
            flag.transform.position = flagPos;
            evac.transform.position = evacPos;
            evac.gameObject.SetActive(false);
            PlacePlayer(flagPos, evacPos);

            flag.GetComponent<FogHidden>().FirstSeen += () => FlagSpotted = true;
            GameManager.Instance.FlagTaken += OnFlagTaken;
        }

        /// <summary>Random open street cell far from the flag (and not on top of the evac point).</summary>
        void PlacePlayer(Vector2 flagPos, Vector2 evacPos)
        {
            LevelMap map = NavGrid.Instance.Map;
            var good = new List<Vector2>();
            var fallback = new List<Vector2>();
            for (int y = 2; y < map.Height - 2; y++)
                for (int x = 2; x < map.Width - 2; x++)
                {
                    if (!OpenAround(map, x, y)) continue;
                    Vector2 p = map.CellCenter(x, y);
                    float d = Vector2.Distance(p, flagPos);
                    if (d < minPlayerFlagDistance || Vector2.Distance(p, evacPos) < 12f) continue;
                    (d <= maxPlayerFlagDistance ? good : fallback).Add(p);
                }
            List<Vector2> pool = good.Count > 0 ? good : fallback;
            if (pool.Count == 0) return;

            Vector2 start = pool[Random.Range(0, pool.Count)];
            var rb = player.GetComponent<Rigidbody2D>();
            float facing = OpenestDirection(map, map.WorldToCell(start));
            rb.position = start;
            rb.rotation = facing;
            player.SetPositionAndRotation(start, Quaternion.Euler(0, 0, facing));
            player.GetComponentInChildren<TankTurret>().ResetAim();
        }

        /// <summary>Hull rotation (degrees) facing down the longest open street - never nose into a wall.</summary>
        static float OpenestDirection(LevelMap map, Vector2Int cell)
        {
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.left, Vector2Int.down, Vector2Int.right };
            int best = 0, bestRun = -1;
            for (int i = 0; i < dirs.Length; i++)
            {
                int run = 0;
                Vector2Int c = cell + dirs[i];
                while (run < 12 && map.IsWalkable(c.x, c.y)) { run++; c += dirs[i]; }
                if (run > bestRun) { bestRun = run; best = i; }
            }
            return best * 90f; // 0 = up, 90 = left, 180 = down, 270 = right
        }

        static bool OpenAround(LevelMap map, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    char c = map[x + dx, y + dy];
                    if (c != '.' && c != ',') return false;
                }
            return true;
        }

        static Vector2 PickFar(List<Vector2> spots, Vector2 from, float minDistance)
        {
            var far = spots.FindAll(s => Vector2.Distance(s, from) >= minDistance);
            if (far.Count > 0) return far[Random.Range(0, far.Count)];
            spots.Sort((a, b) => Vector2.Distance(b, from).CompareTo(Vector2.Distance(a, from)));
            return spots[0];
        }

        void OnFlagTaken()
        {
            SignalStrength = 0f;
            evac.gameObject.SetActive(true);
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (!gm.IsPlaying) return;

            if (gm.HasFlag)
            {
                if (!bossTriggered && Vector2.Distance(player.position, evac.transform.position) <= bossTriggerDistance)
                    SpawnBoss();
                return;
            }

            float d = Vector2.Distance(player.position, flag.transform.position);
            SignalStrength = Mathf.Clamp01(1f - d / signalRange);

            beepTimer -= Time.deltaTime;
            if (beepTimer > 0f) return;
            beepTimer = Mathf.Lerp(slowBeepInterval, fastBeepInterval, Mathf.Pow(SignalStrength, 1.6f));
            Audio.Play(Audio.Bank?.radioBeep, null, 0.25f + SignalStrength * 0.25f, 0.85f + SignalStrength * 0.5f, 0f);
            BeepPlayed?.Invoke();
        }

        void SpawnBoss()
        {
            bossTriggered = true;
            if (bossPrefab == null)
            {
                BossDefeated = true;
                return;
            }
            Boss = Instantiate(bossPrefab, evac.transform.position, Quaternion.identity);
            Boss.name = bossPrefab.name;
            Boss.Emerge(EmergeFrom.Rooftop);
            Boss.Health.Died += OnBossDied;
            BossArmor = Boss.GetComponent<HelicopterBoss>();
            if (BossArmor != null) BossArmor.PhaseChanged += i => BossPhaseChanged?.Invoke(i);

            Fx.Instance?.Ping(evac.transform.position, new Color(1f, 0.3f, 0.2f), 6f);
            Audio.Play(Audio.Bank?.alarm, null, 0.8f, 0.8f, 0f);
            CameraRig.Instance?.AddTrauma(0.5f);
            BossSpawned?.Invoke();
        }

        void OnBossDied()
        {
            BossDefeated = true;
            GameManager.Instance?.HitStop(0.12f);
            CameraRig.Instance?.AddTrauma(0.8f);
            BossKilled?.Invoke();
        }
    }
}
