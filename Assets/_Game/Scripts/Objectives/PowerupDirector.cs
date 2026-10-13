using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Drops supply containers into open streets over time (a few on the map at once).</summary>
    public class PowerupDirector : MonoBehaviour
    {
        public static PowerupDirector Instance { get; private set; }

        [SerializeField] SupplyCrate cratePrefab;
        [SerializeField] int startCrates = 3;
        [SerializeField] int maxCrates = 4;
        [SerializeField] float interval = 22f;
        [SerializeField] float minDistanceFromPlayer = 7f;

        /// <summary>Picked-up modifier and its name, for the HUD.</summary>
        public event Action<PowerupKind> Collected;

        readonly List<SupplyCrate> crates = new();
        readonly List<Vector2> spots = new();
        float timer;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            yield return null; // the player is placed at its random start during Start
            // open street cells with room around them
            LevelMap map = NavGrid.Instance.Map;
            for (int y = 2; y < map.Height - 2; y++)
                for (int x = 2; x < map.Width - 2; x++)
                    if (map[x, y] == '.' && map.IsWalkable(x + 1, y) && map.IsWalkable(x - 1, y) &&
                        map.IsWalkable(x, y + 1) && map.IsWalkable(x, y - 1))
                        spots.Add(map.CellCenter(x, y));

            for (int i = 0; i < startCrates; i++) Spawn();
            timer = interval;
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            crates.RemoveAll(c => c == null);
            if (crates.Count < maxCrates) Spawn();
        }

        void Spawn()
        {
            Vector2 player = GameManager.Instance.Player.transform.position;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 p = spots[UnityEngine.Random.Range(0, spots.Count)];
                if (Vector2.Distance(p, player) < minDistanceFromPlayer) continue;
                if (crates.Exists(c => c != null && Vector2.Distance(c.transform.position, p) < 10f)) continue;

                SupplyCrate crate = Instantiate(cratePrefab, p, Quaternion.identity);
                crate.Setup(PickKind());
                crates.Add(crate);
                return;
            }
        }

        /// <summary>Random modifier; the twin barrel is permanent, so stop offering it once you have it.</summary>
        PowerupKind PickKind()
        {
            bool hasTwin = GameManager.Instance.Player.Turret.TwinActive ||
                           crates.Exists(c => c != null && c.Kind == PowerupKind.TwinBarrel);
            PowerupKind kind;
            do kind = (PowerupKind)UnityEngine.Random.Range(0, 4);
            while (hasTwin && kind == PowerupKind.TwinBarrel);
            return kind;
        }

        public void OnCollected(SupplyCrate crate)
        {
            crates.Remove(crate);
            Collected?.Invoke(crate.Kind);
        }
    }
}
