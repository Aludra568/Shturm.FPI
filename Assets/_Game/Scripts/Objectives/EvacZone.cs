using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Extraction pad, revealed once the flag is taken. Hold position inside it while drones swarm in;
    /// leaving drains the progress.
    /// </summary>
    public class EvacZone : MonoBehaviour
    {
        [SerializeField] float radius = 2.2f;
        [SerializeField] float holdTime = 3f;
        [SerializeField] Light2D beacon;
        [SerializeField] Transform padVisual;
        [SerializeField] Sprite smokeSprite;

        public float Progress { get; private set; }
        public bool PlayerInside { get; private set; }
        public float Radius => radius;
        /// <summary>Extraction is impossible while the evac guardian is alive.</summary>
        public bool Locked => ObjectiveDirector.Instance != null && !ObjectiveDirector.Instance.BossDefeated;

        float smokeTimer;
        float beaconBase;

        void Awake()
        {
            if (beacon != null) beaconBase = beacon.intensity;
        }

        void Update()
        {
            if (beacon != null) beacon.intensity = beaconBase * (0.75f + Mathf.Sin(Time.time * 4f) * 0.25f);
            if (padVisual != null) padVisual.Rotate(0, 0, 20f * Time.deltaTime);

            // green signal smoke drifting up from the pad
            smokeTimer -= Time.deltaTime;
            if (smokeTimer <= 0f && Fx.Instance != null)
            {
                smokeTimer = 0.12f;
                Fx.Instance.Spawn(smokeSprite, (Vector2)transform.position + Random.insideUnitCircle * 0.4f,
                                  new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.6f)), 2.2f, 0.5f, 2.2f,
                                  new Color(0.4f, 1f, 0.5f, 0.55f), new Color(0.3f, 0.8f, 0.4f, 0f), drag: 0.6f, unlit: true);
            }

            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            PlayerInside = Vector2.Distance(gm.Player.transform.position, transform.position) <= radius;
            float before = Progress;
            bool holding = PlayerInside && !Locked;
            Progress = Mathf.Clamp01(Progress + Time.deltaTime / holdTime * (holding ? 1f : -0.5f));
            if (Progress >= 1f && before < 1f)
            {
                Fx.Instance?.Ping(transform.position, new Color(0.5f, 1f, 0.6f), 6f);
                Audio.Play(Audio.Bank?.win, null, 1f, 1f, 0f);
                gm.Win();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
