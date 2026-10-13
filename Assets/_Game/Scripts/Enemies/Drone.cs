using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    public enum EmergeFrom { Hatch, Rooftop }

    /// <summary>
    /// Shared drone logic: steering (flow field + separation), facing, hit/death feedback,
    /// emerging from a manhole or dropping off a rooftop, and "sound pings" while hidden in the dark.
    /// Drones move in the same plane as the tank: walls block them and shells ricochet into them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public abstract class Drone : MonoBehaviour
    {
        public static readonly List<Drone> Alive = new();

        /// <summary>Drones on the map not counting the boss.</summary>
        public static int RegularAlive
        {
            get
            {
                int n = 0;
                foreach (Drone d in Alive) if (!d.IsBoss) n++;
                return n;
            }
        }

        [Tooltip("The unique evac guardian: excluded from regular spawn counts.")]
        [SerializeField] bool isBoss;
        public bool IsBoss => isBoss;
        public Health Health => health;
        public float TurnSpeed { get => turnSpeed; set => turnSpeed = value; }

        /// <summary>Knocked out of control for a moment: drifts and spins, does not attack.</summary>
        public void Stun(float seconds) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        protected bool Stunned => Time.time < stunnedUntil;
        /// <summary>The tank is hidden inside a smoke screen: drones lose the trail.</summary>
        protected bool PlayerConcealed => player != null && SmokeScreen.Covers(player.position);
        float stunnedUntil;
        /// <summary>True while the player cannot see this drone (it is in the dark or behind a wall).</summary>
        public bool HiddenFromPlayer => fog != null && !fog.IsVisible;

        [SerializeField] protected float speed = 3f;
        [SerializeField] protected float acceleration = 12f;
        [SerializeField] protected float turnSpeed = 360f;
        [SerializeField] protected float bodyRadius = 0.4f;
        [SerializeField] float separationRadius = 1.2f;
        [SerializeField] float separationWeight = 1.4f;
        [SerializeField] protected Transform body;
        [SerializeField] Transform[] rotors;
        [SerializeField] AudioSource buzz;
        [SerializeField] float deathExplosionSize = 0.7f;
        [Tooltip("Repair kit dropped sometimes when the player shoots this drone down.")]
        [SerializeField] GameObject dropPrefab;
        [SerializeField, Range(0, 1)] float dropChance = 0.25f;

        protected Rigidbody2D rb;
        protected Health health;
        protected FogHidden fog;
        protected Transform player;
        protected bool Active { get; private set; }

        float punch;
        float hitFlash;
        SpriteRenderer[] bodySprites;
        Color[] bodyColors;
        Vector3 bodyScale;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            fog = GetComponent<FogHidden>();
            bodyScale = body.localScale;
            bodySprites = body.GetComponentsInChildren<SpriteRenderer>();
            speed *= Difficulty.EnemySpeed;
            wanderSeed = Random.value * 100f;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        protected virtual void Start()
        {
            player = GameManager.Instance != null ? GameManager.Instance.Player.transform : null;
            if (buzz != null && Audio.Bank != null)
            {
                buzz.clip = Audio.Bank.droneLoop;
                buzz.loop = true;
                buzz.pitch = Random.Range(0.9f, 1.15f) * (bodyRadius > 0.5f ? 0.7f : 1.2f);
                buzz.Play();
            }
            if (!emerging) Active = true;
            // true colours come from FogHidden: by now it may already have faded the sprites out
            bodyColors = System.Array.ConvertAll(bodySprites, s => fog != null ? fog.GetBaseColor(s) : s.color);
        }

        void OnEnable() => Alive.Add(this);
        void OnDisable() => Alive.Remove(this);

        // ------------------------------------------------------------------ spawning

        bool emerging;

        public void Emerge(EmergeFrom from)
        {
            emerging = true;
            StartCoroutine(EmergeRoutine(from));
        }

        IEnumerator EmergeRoutine(EmergeFrom from)
        {
            Active = false;
            health.SetInvulnerable(true);
            var col = GetComponentInChildren<Collider2D>(); // the helicopter keeps its hitbox on the rotating body
            col.enabled = false;
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                // from a hatch it rises (grows), from a roof it drops (shrinks to normal size)
                float scale = from == EmergeFrom.Hatch ? Mathf.Lerp(0.3f, 1f, k) : Mathf.Lerp(1.9f, 1f, k * k);
                body.localScale = bodyScale * scale;
                yield return null;
            }
            body.localScale = bodyScale;
            if (from == EmergeFrom.Rooftop) Fx.Instance?.Dust(transform.position, Vector2.up, 6);
            col.enabled = true;
            health.SetInvulnerable(false);
            emerging = false;
            Active = true;
        }

        // ------------------------------------------------------------------ behaviour

        /// <summary>Top speed used to clamp steering (subclasses with a dash raise it).</summary>
        protected virtual float MaxSpeed => speed;

        /// <summary>Velocity the drone wants this physics step (before separation).</summary>
        protected abstract Vector2 DesiredVelocity();

        /// <summary>Where the drone's nose should point.</summary>
        protected virtual Vector2 FacingDirection() => rb.linearVelocity;

        protected virtual void FixedUpdate()
        {
            if (!Active || player == null || !IsGameRunning || Stunned)
            {
                rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
                return;
            }
            Vector2 desired = PlayerConcealed ? Wander() * speed * 0.4f : DesiredVelocity();
            desired += Separation() * speed;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.ClampMagnitude(desired, MaxSpeed * 1.3f), acceleration * Time.fixedDeltaTime);
        }

        protected virtual void Update()
        {
            foreach (Transform r in rotors) r.Rotate(0, 0, 1400f * Time.deltaTime);

            Vector2 face = FacingDirection();
            if (Stunned)
                body.Rotate(0, 0, 140f * Time.deltaTime); // spinning out of control
            else if (face.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(face.y, face.x) * Mathf.Rad2Deg - 90f;
                float z = Mathf.MoveTowardsAngle(body.eulerAngles.z, target, turnSpeed * Time.deltaTime);
                body.rotation = Quaternion.Euler(0, 0, z);
            }

            if (hitFlash > 0f && bodyColors != null)
            {
                // bright flash on every hit, so even a non-lethal hit clearly registers
                hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 7f);
                for (int i = 0; i < bodySprites.Length; i++)
                {
                    Color c = Color.Lerp(bodyColors[i], new Color(1f, 0.55f, 0.45f), hitFlash);
                    if (fog != null) fog.SetBaseColor(bodySprites[i], c); else bodySprites[i].color = c;
                }
            }

            if (punch > 0f)
            {
                punch = Mathf.Max(0f, punch - Time.deltaTime * 8f);
                body.localScale = bodyScale * (1f + punch * 0.25f);
            }

            if (buzz != null) buzz.volume = 0.22f * Audio.Falloff(transform.position) * (IsGameRunning ? 1f : 0f);

        }

        protected static bool IsGameRunning => GameManager.Instance == null || GameManager.Instance.IsPlaying ||
                                              GameManager.Instance.State == GameState.Lost;

        protected Vector2 ToPlayer => (Vector2)player.position - (Vector2)transform.position;

        /// <summary>Clear straight path wide enough for this drone's body.</summary>
        protected bool HasClearPath(Vector2 to)
        {
            return !Physics2D.CircleCast(transform.position, bodyRadius * 0.9f, to.normalized, to.magnitude - 0.6f, Layers.Solid);
        }

        /// <summary>Can a shell fly straight from here to the player?</summary>
        protected bool HasLineOfFire(Vector2 to)
        {
            return !Physics2D.Linecast(transform.position, (Vector2)transform.position + to, Layers.Solid);
        }

        protected Vector2 ChaseDirection()
        {
            Vector2 to = ToPlayer;
            if (HasClearPath(to)) return to.normalized;
            return NavGrid.Instance != null ? NavGrid.Instance.FlowDirection(transform.position) : to.normalized;
        }

        /// <summary>Aimless drifting while the target is lost in smoke.</summary>
        float wanderSeed;

        Vector2 Wander()
        {
            float t = Time.time * 0.4f + wanderSeed;
            return new Vector2(Mathf.PerlinNoise(t, 0f) * 2f - 1f, Mathf.PerlinNoise(0f, t) * 2f - 1f);
        }

        Vector2 Separation()
        {
            Vector2 push = Vector2.zero;
            Vector2 pos = transform.position;
            foreach (Drone other in Alive)
            {
                if (other == this) continue;
                Vector2 away = pos - (Vector2)other.transform.position;
                float d = away.magnitude;
                if (d < 0.001f || d > separationRadius) continue;
                push += away / d * (1f - d / separationRadius);
            }
            return push * separationWeight;
        }

        // ------------------------------------------------------------------ damage

        protected virtual void OnDamaged(float amount, Vector2 direction)
        {
            punch = 1f;
            hitFlash = 1f;
            Fx.Instance?.Sparks(transform.position, -direction, new Color(1f, 0.7f, 0.4f), 8, 4f);
            rb.AddForce(direction * 2.5f, ForceMode2D.Impulse);
            Audio.Play(Audio.Bank?.unitHit, transform.position, 0.7f);
        }

        protected virtual void OnDied() => Remove(creditKill: true);

        /// <summary>Death effects + cleanup. Kills (and loot) only count when the player caused them.</summary>
        protected void Remove(bool creditKill, bool explosionFx = true)
        {
            if (explosionFx)
            {
                Fx.Instance?.Explosion(transform.position, deathExplosionSize);
                Audio.Play(Audio.Bank?.explosion, transform.position, 0.7f, 1.25f);
                CameraRig.Instance?.AddTrauma(0.2f, transform.position);
            }
            Fx.Instance?.Scorch(transform.position, deathExplosionSize * 1.5f);
            if (creditKill)
            {
                GameManager.Instance?.RegisterKill(transform.position);
                GameManager.Instance?.HitStop(0.045f);
                if (dropPrefab != null && Random.value < dropChance)
                    Instantiate(dropPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}
