using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    public enum PowerupKind { Mangal, TwinBarrel, Repair, Allies }

    /// <summary>
    /// Supply container lying in the streets. Drive over it to take what is inside:
    /// anti-drone "mangal" cage (soaks hits), twin barrel (permanent), repair kit, or three helper drones.
    /// </summary>
    public class SupplyCrate : MonoBehaviour
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] Light2D glow;
        [SerializeField] Sprite mangalIcon, twinIcon, repairIcon, alliesIcon;
        [SerializeField] int mangalHits = 3;
        [SerializeField] float repairAmount = 35f;
        [SerializeField] int allyCount = 3;
        [SerializeField] float pickupRadius = 0.85f;

        public PowerupKind Kind { get; private set; }

        public static Color ColorOf(PowerupKind kind) => kind switch
        {
            PowerupKind.Mangal => new Color(0.75f, 0.8f, 0.9f),
            PowerupKind.TwinBarrel => new Color(1f, 0.65f, 0.25f),
            PowerupKind.Allies => new Color(0.4f, 0.85f, 1f),
            _ => new Color(0.45f, 1f, 0.55f)
        };

        public static string NameOf(PowerupKind kind) => kind switch
        {
            PowerupKind.Mangal => "МАНГАЛ",
            PowerupKind.TwinBarrel => "СПАРКА",
            PowerupKind.Allies => "ДРОНЫ",
            _ => "РЕМОНТ"
        };

        public void Setup(PowerupKind kind)
        {
            Kind = kind;
            icon.sprite = kind switch
            {
                PowerupKind.Mangal => mangalIcon,
                PowerupKind.TwinBarrel => twinIcon,
                PowerupKind.Allies => alliesIcon,
                _ => repairIcon
            };
            Color c = ColorOf(kind);
            glow.color = c;
            // keep it hidden in the dark: tint through FogHidden instead of overriding the faded colour
            var fog = GetComponent<FogHidden>();
            if (fog != null) fog.SetBaseColor(icon, c); else icon.color = c;
        }

        void Update()
        {
            float t = Time.time * 3f + transform.position.x;
            // the camera rotates with the gun: keep the icon upright on screen, floating "above" the crate
            Transform view = Camera.main != null ? Camera.main.transform : transform;
            icon.transform.SetPositionAndRotation(transform.position + view.up * (0.55f + Mathf.Sin(t) * 0.08f), view.rotation);

            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            if (Vector2.Distance(gm.Player.transform.position, transform.position) <= pickupRadius) Collect(gm.Player);
        }

        void Collect(PlayerTank player)
        {
            switch (Kind)
            {
                case PowerupKind.Mangal: player.Health.AddMangal(mangalHits); break;
                case PowerupKind.TwinBarrel: player.Turret.EnableTwin(); break;
                case PowerupKind.Repair: player.Health.Heal(repairAmount); break;
                case PowerupKind.Allies: player.SpawnAllies(allyCount); break;
            }
            Color c = ColorOf(Kind);
            Fx.Instance?.Ping(transform.position, c, 2.5f);
            Fx.Instance?.LightFlash(transform.position, c, 2f, 3f, 0.3f);
            Audio.Play(Audio.Bank?.powerup, null, 0.9f, 1f, 0f);
            PowerupDirector.Instance?.OnCollected(this);
            Destroy(gameObject);
        }
    }
}
