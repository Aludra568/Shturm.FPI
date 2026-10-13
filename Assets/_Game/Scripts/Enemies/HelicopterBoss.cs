using System;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// The evac guardian's armour and phases. Armour lets only a fraction of damage through, except at
    /// one glowing weak spot that moves each phase (tail, then right side, then left side). It keeps its nose on the
    /// tank, so the weak spot always faces away - you have to bank shells off the walls to reach it.
    /// Each phase change knocks it into a short spin: a window for direct hits.
    /// </summary>
    [RequireComponent(typeof(GunnerDrone))]
    public class HelicopterBoss : MonoBehaviour, IArmor
    {
        [Serializable]
        public class Phase
        {
            public string weakSpotName = "ХВОСТ";
            [Tooltip("Direction of the weak spot relative to the nose, degrees (180 = tail, -90 = right, 90 = left).")]
            public float weakAngle = 180f;
            [Tooltip("Half-width of the vulnerable sector, degrees.")]
            public float weakHalfArc = 55f;
            [Tooltip("Where the glowing marker sits on the body (local, body space).")]
            public Vector2 markerPosition = new(0f, -1.15f);
            public float turnSpeed = 80f;
            public int burst = 3;
            public float fireInterval = 2.8f;
        }

        [SerializeField] Phase[] phases;
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer weakMarker;
        [Tooltip("Share of damage that still gets through the armour.")]
        [SerializeField] float armorDamage = 0.3f;
        [Tooltip("A hit this close to the glowing marker counts as a weak-spot hit.")]
        [SerializeField] float weakSpotRadius = 0.75f;
        [SerializeField] float stunOnPhaseChange = 2.5f;
        [SerializeField] float stunOnArrival = 2f;
        [Tooltip("Every so often the engine overheats and it spins helplessly - guarantees the weak spot " +
                 "comes round even if the boss parks it against a wall.")]
        [SerializeField] float overheatInterval = 14f;
        [SerializeField] float overheatDuration = 2.2f;

        public int PhaseIndex { get; private set; }
        public int PhaseCount => phases.Length;
        public Phase Current => phases[PhaseIndex];

        /// <summary>New phase index (for the HUD).</summary>
        public event Action<int> PhaseChanged;

        GunnerDrone drone;
        Health health;
        float flash;
        float overheatTimer;

        void Awake()
        {
            drone = GetComponent<GunnerDrone>();
            health = GetComponent<Health>();
            health.Damaged += (_, _) => { flash = 1f; CheckPhase(); };
        }

        void Start()
        {
            health.SetMax(health.Max * Difficulty.BossHealth);
            ApplyPhase();
            drone.Stun(stunOnArrival + 0.6f); // dazed after dropping in (0.6 s is the landing)
            overheatTimer = overheatInterval;
        }

        public float DamageMultiplier(Vector2 hitNormal, Vector2 point)
        {
            // weak spot: near the glowing marker, or anywhere on that side of the hull
            bool nearMarker = Vector2.Distance(point, weakMarker.transform.position) <= weakSpotRadius;
            Vector2 weakDir = Quaternion.Euler(0, 0, Current.weakAngle) * body.up;
            if (nearMarker || Vector2.Angle(weakDir, hitNormal) <= Current.weakHalfArc)
            {
                Fx.Instance?.Sparks(point, hitNormal, new Color(1f, 0.6f, 0.2f), 18, 7f);
                Fx.Instance?.LightFlash(point, new Color(1f, 0.5f, 0.2f), 2.5f, 3f, 0.2f);
                return 1f;
            }

            Fx.Instance?.Sparks(point, hitNormal, new Color(0.75f, 0.8f, 0.85f), 8, 4f);
            Audio.Play(Audio.Bank?.armorHit, point, 0.7f, 1.1f);
            return armorDamage;
        }

        void CheckPhase()
        {
            int wanted = Mathf.Clamp((int)((1f - health.Normalized) * phases.Length), 0, phases.Length - 1);
            if (health.IsDead || wanted <= PhaseIndex) return;
            PhaseIndex = wanted;
            ApplyPhase();
            drone.Stun(stunOnPhaseChange);
            overheatTimer = overheatInterval;
            Fx.Instance?.Explosion(body.position, 0.9f);
            CameraRig.Instance?.AddTrauma(0.5f);
            PhaseChanged?.Invoke(PhaseIndex);
        }

        void ApplyPhase()
        {
            Phase p = Current;
            weakMarker.transform.localPosition = p.markerPosition;
            drone.TurnSpeed = p.turnSpeed;
            drone.SetAttack(p.burst, p.fireInterval);
        }

        void Update()
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer <= 0f && !health.IsDead)
            {
                overheatTimer = overheatInterval;
                drone.Stun(overheatDuration);
                Fx.Instance?.Dust(body.position, Vector2.up, 8);
                Audio.Play(Audio.Bank?.armorHit, body.position, 0.6f, 0.6f);
            }

            // the weak spot pulses so it reads instantly, and flares when hit
            flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            weakMarker.transform.localScale = Vector3.one * (pulse + flash * 0.5f);
        }
    }
}
