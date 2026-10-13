using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Ties the tank's parts together and owns its feedback: chosen class and colour, visible damage
    /// (cracks, a thrown track that slows you down, fire), shield and twin-barrel pickups, nitro,
    /// engine sound and the captured flag on the hull.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(TankController))]
    public class PlayerTank : MonoBehaviour
    {
        [SerializeField] TankClass[] classes;
        [Header("Parts")]
        [SerializeField] TankTurret turret;
        [SerializeField] SpriteRenderer hullSprite;
        [SerializeField] SpriteRenderer turretSprite;
        [SerializeField] SpriteRenderer barrelSprite;
        [SerializeField] SpriteRenderer trackLeft;
        [SerializeField] SpriteRenderer trackRight;
        [SerializeField] SpriteRenderer damageOverlay;
        [SerializeField] SpriteRenderer[] headlights;
        [Tooltip("Short beam from the hull front: shows where the hull points without HUD clutter.")]
        [SerializeField] Light2D headlightBeam;
        [Tooltip("Anti-drone cage drawn over the tank while the mangal pickup has hits left.")]
        [SerializeField] SpriteRenderer mangalCage;
        [SerializeField] Sprite slatSprite;
        [SerializeField] AllyDrone allyPrefab;
        [SerializeField] GameObject carriedFlag;
        [SerializeField] Transform exhaust;
        [SerializeField] AudioSource engine;
        [Header("Effects")]
        [SerializeField] Sprite flameSprite;
        [SerializeField] Sprite smokeSprite;
        [Tooltip("Speed multiplier once a track has been blown off.")]
        [SerializeField] float brokenTrackSpeed = 0.7f;

        public Health Health { get; private set; }
        public TankController Controller { get; private set; }
        public TankTurret Turret => turret;
        public TankAbility Ability { get; private set; }
        public TankClass[] Classes => classes;
        public TankClass Class { get; private set; }
        public Color Color { get; private set; }
        public bool TrackLost { get; private set; }

        /// <summary>For the HUD damage vignette (0..1 strength).</summary>
        public event Action<float> Hurt;
        /// <summary>Fired when the damage state changes (track lost/repaired) - HUD message.</summary>
        public event Action<string> StatusMessage;

        float hitFlash;
        float smokeTimer;
        SpriteRenderer lostTrack;

        void Awake()
        {
            Health = GetComponent<Health>();
            Controller = GetComponent<TankController>();
            Health.Damaged += OnDamaged;
            Health.Healed += _ => RefreshDamageState();
            Health.Died += OnDied;
            Health.MangalBlocked += OnMangalBlocked;
            Ability = GetComponent<TankAbility>();
            Ability.Started += OnAbilityStarted;
            Ability.Ended += kind => { if (kind == AbilityKind.Nitro) CameraRig.Instance?.SetZoom(0f); };
            carriedFlag.SetActive(false);
            damageOverlay.enabled = false;
            mangalCage.enabled = false;
            ApplySelection();
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.FlagTaken += () => carriedFlag.SetActive(true);
            if (engine != null && Audio.Bank != null)
            {
                engine.clip = Audio.Bank.engineLoop;
                engine.loop = true;
                engine.Play();
            }
        }

        // ------------------------------------------------------------------ class & colour

        /// <summary>Re-reads the menu choice (called again whenever the player changes it).</summary>
        public void ApplySelection()
        {
            if (classes == null || classes.Length == 0) return;
            Apply(classes[Mathf.Clamp(TankSelection.ClassIndex, 0, classes.Length - 1)], TankSelection.Color);
        }

        void Apply(TankClass c, Color color)
        {
            Class = c;
            Color = color;
            hullSprite.sprite = c.hull;
            turretSprite.sprite = c.turret;
            damageOverlay.sprite = c.damage;
            trackLeft.sprite = c.track;
            trackRight.sprite = c.track;
            trackLeft.transform.localPosition = new Vector3(-c.trackOffset, 0f, 0f);
            trackRight.transform.localPosition = new Vector3(c.trackOffset, 0f, 0f);

            float frontY = c.hull.bounds.extents.y * 0.85f;
            for (int i = 0; i < headlights.Length; i++)
                headlights[i].transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * c.trackOffset * 0.55f, frontY, 0f);
            headlightBeam.transform.localPosition = new Vector3(0f, frontY, 0f);

            // oval (capsule) hitbox matching hull + tracks: slides round corners instead of snagging
            var body = GetComponent<CapsuleCollider2D>();
            body.direction = CapsuleDirection2D.Vertical;
            body.size = c.colliderSize;
            mangalCage.transform.localScale = new Vector3(c.colliderSize.x / 0.8f, c.colliderSize.y / 1f, 1f);
            Health.SetMax(c.maxHealth);
            Controller.SetStats(c.moveSpeed, c.turnSpeed);
            turret.Configure(c);
            GetComponent<TankAbility>().Configure(c.ability);
            ApplyTint(Color.white, 0f);
        }

        void ApplyTint(Color flashColor, float flash)
        {
            Color body = Color.Lerp(Color, flashColor, flash);
            hullSprite.color = body;
            turretSprite.color = Color.Lerp(Color * 0.92f, flashColor, flash);
            barrelSprite.color = Color.Lerp(Color * 0.85f, flashColor, flash);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (engine != null)
            {
                float s = Controller.Speed01;
                engine.pitch = Mathf.Lerp(engine.pitch, 0.7f + s * 0.5f + (Controller.NitroActive ? 0.35f : 0f), Time.deltaTime * 5f);
                engine.volume = Health.IsDead ? 0f : 0.18f + s * 0.17f;
            }

            if (Fx.Instance != null && !Health.IsDead)
            {
                if (Controller.NitroActive && exhaust != null)
                    Fx.Instance.Spawn(flameSprite, (Vector2)exhaust.position + UnityEngine.Random.insideUnitCircle * 0.12f,
                                      -(Vector2)transform.up * UnityEngine.Random.Range(2f, 4f), 0.25f, 0.45f, 0.1f,
                                      new Color(0.5f, 0.8f, 1f), new Color(0.2f, 0.3f, 1f, 0f), unlit: true);
                EmitDamageSmoke();
            }

            // active armour: hull glows hot orange while it is up
            if (Ability.Active && Ability.Kind == AbilityKind.ActiveArmor && hitFlash <= 0f)
                ApplyTint(new Color(1f, 0.6f, 0.25f), 0.35f + 0.2f * Mathf.Sin(Time.time * 8f));
            else if (Ability.Kind == AbilityKind.ActiveArmor && !Ability.Active && hitFlash <= 0f && Health.DamageScale >= 1f)
                ApplyTint(Color.white, 0f);

            if (hitFlash > 0f)
            {
                hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 6f);
                ApplyTint(new Color(1f, 0.35f, 0.3f), hitFlash);
            }

            // the cage looks more battered with every hit it soaks up
            mangalCage.enabled = Health.HasMangal;
            if (Health.HasMangal) mangalCage.color = new Color(1f, 1f, 1f, 0.45f + 0.18f * Health.MangalHits);
        }

        void EmitDamageSmoke()
        {
            float hp = Health.Normalized;
            if (hp > 0.5f) return;
            smokeTimer -= Time.deltaTime;
            if (smokeTimer > 0f) return;
            bool burning = hp <= 0.25f;
            smokeTimer = burning ? 0.06f : 0.18f;
            Vector2 pos = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * 0.2f;
            Fx.Instance.Spawn(smokeSprite, pos, new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), 0.8f), 1.2f, 0.25f, 0.9f,
                              new Color(0.25f, 0.24f, 0.23f, 0.7f), new Color(0.2f, 0.2f, 0.2f, 0f), drag: 0.5f);
            if (burning)
                Fx.Instance.Spawn(flameSprite, pos, new Vector2(0f, 0.6f), 0.35f, 0.35f, 0.05f,
                                  new Color(1f, 0.6f, 0.2f), new Color(1f, 0.2f, 0f, 0f), unlit: true);
        }

        // ------------------------------------------------------------------ damage

        void OnDamaged(float amount, Vector2 direction)
        {
            hitFlash = 1f;
            float strength = Mathf.Clamp01(amount / 25f);
            CameraRig.Instance?.AddTrauma(0.25f + strength * 0.35f);
            Audio.Play(Audio.Bank?.playerHit, transform.position, 0.9f);
            Fx.Instance?.Sparks(transform.position, -direction, new Color(1f, 0.6f, 0.3f), 10, 5f);
            Hurt?.Invoke(strength);
            RefreshDamageState();
        }

        /// <summary>At half armour a track is blown off (slower), repairs above half put it back.</summary>
        void RefreshDamageState()
        {
            bool damaged = Health.Normalized <= 0.5f && !Health.IsDead;
            damageOverlay.enabled = Health.Normalized <= 0.5f;
            if (damaged && !TrackLost) LoseTrack();
            else if (!damaged && TrackLost && !Health.IsDead) RepairTrack();
        }

        void LoseTrack()
        {
            TrackLost = true;
            lostTrack = UnityEngine.Random.value < 0.5f ? trackLeft : trackRight;
            lostTrack.enabled = false;
            Controller.DamageSpeedFactor = brokenTrackSpeed;

            Vector3 world = lostTrack.transform.position;
            Fx.Instance?.Decal(Class.trackDebris, world, transform.rotation * Quaternion.Euler(0, 0, UnityEngine.Random.Range(-30f, 30f)), 1f);
            Fx.Instance?.Sparks(world, -transform.right, new Color(1f, 0.7f, 0.3f), 14, 6f);
            Fx.Instance?.Dust(world, Vector2.up, 6);
            Audio.Play(Audio.Bank?.wallImpact, transform.position, 1f, 0.7f);
            CameraRig.Instance?.AddTrauma(0.4f);
            StatusMessage?.Invoke("ГУСЕНИЦА СБИТА");
        }

        void RepairTrack()
        {
            TrackLost = false;
            if (lostTrack != null) lostTrack.enabled = true;
            Controller.DamageSpeedFactor = 1f;
            StatusMessage?.Invoke("ГУСЕНИЦА ЦЕЛА");
        }

        void OnMangalBlocked(Vector2 direction)
        {
            // slats fly off the cage
            Vector2 at = (Vector2)transform.position - direction * 0.5f;
            Fx.Instance?.Sparks(at, -direction, new Color(1f, 0.75f, 0.4f), 10, 5f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 v = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-60f, 60f)) * -direction * UnityEngine.Random.Range(2f, 4f);
                Fx.Instance?.Spawn(slatSprite, at, v, 0.7f, 0.5f, 0.4f, new Color(0.6f, 0.6f, 0.6f), new Color(0.4f, 0.4f, 0.4f, 0f), drag: 3f);
            }
            Audio.Play(Audio.Bank?.armorHit, transform.position, 0.8f, 1.2f);
            CameraRig.Instance?.AddTrauma(0.2f);
            StatusMessage?.Invoke(Health.HasMangal ? "МАНГАЛ -1" : "МАНГАЛ РАЗБИТ");
        }

        /// <summary>Helper drones from a supply crate: they circle the tank and ram the nearest enemy.</summary>
        public void SpawnAllies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AllyDrone ally = Instantiate(allyPrefab, transform.position, Quaternion.identity);
                ally.Init(this, i * 360f / count);
            }
        }

        void OnDied()
        {
            Explosion.Detonate(transform.position, 0.1f, 0f, 1.6f, gameObject);
            Fx.Instance?.Scorch(transform.position, 2f);
            Color burnt = new Color(0.25f, 0.22f, 0.2f);
            hullSprite.color = turretSprite.color = barrelSprite.color = burnt;
            damageOverlay.enabled = true;
            foreach (var h in headlights) h.enabled = false;
            headlightBeam.enabled = false;
            turret.enabled = false;
            Controller.enabled = false;
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }

        void OnAbilityStarted(AbilityKind kind)
        {
            StatusMessage?.Invoke(TankAbility.NameOf(kind) + "!");
            if (kind == AbilityKind.ActiveArmor)
            {
                Audio.Play(Audio.Bank?.abilityArmor, transform.position, 0.9f);
                Fx.Instance?.Ping(transform.position, new Color(1f, 0.6f, 0.25f), 2.2f);
                return;
            }
            if (kind != AbilityKind.Nitro) return;
            CameraRig.Instance?.SetZoom(0.7f);
            CameraRig.Instance?.AddTrauma(0.2f);
            Audio.Play(Audio.Bank?.nitro, transform.position, 0.9f);
        }
    }
}
