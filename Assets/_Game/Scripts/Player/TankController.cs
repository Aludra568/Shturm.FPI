using System;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Tank driving: W/S is throttle forward/back along the hull, and the hull swings round to follow
    /// the turret on its own (so W drives where you look). A/D turn the hull by hand while held.
    /// The hull has weight - it accelerates and brakes gradually.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TankController : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] float moveSpeed = 3.3f;
        [SerializeField] float reverseSpeedFactor = 0.6f;
        [SerializeField] float turnSpeed = 95f;
        [Tooltip("Hull turns slower at full speed than when pivoting in place.")]
        [SerializeField] float turnAtSpeedFactor = 0.75f;
        [SerializeField] float acceleration = 5f;
        [SerializeField] float braking = 9f;
        [Tooltip("Speed multiplier while the Lynx's nitro ability is running.")]
        [SerializeField] float nitroMultiplier = 1.7f;

        public bool NitroActive { get; private set; }
        public float Speed01 => Mathf.Abs(currentSpeed) / moveSpeed;
        public Vector2 Velocity => rb.linearVelocity;
        /// <summary>1 = healthy; lower when a track has been blown off.</summary>
        public float DamageSpeedFactor { get; set; } = 1f;


        Rigidbody2D rb;
        TankTurret turret;
        float currentSpeed;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            turret = GetComponentInChildren<TankTurret>();
        }

        /// <summary>Applies the tank class picked in the menu.</summary>
        public void SetStats(float speed, float turn)
        {
            moveSpeed = speed;
            turnSpeed = turn;
        }

        bool CanAct => GameManager.Instance == null || GameManager.Instance.IsPlaying;

        /// <summary>Nitro on/off (driven by TankAbility).</summary>
        public void SetBoost(bool on) => NitroActive = on;

        void FixedUpdate()
        {
            Vector2 stick = CanAct ? input.Move : Vector2.zero;
            float throttle = Mathf.Clamp(stick.y, -1f, 1f);
            float steer = Mathf.Clamp(stick.x, -1f, 1f);
            float dt = Time.fixedDeltaTime;

            // throttle: forward or slower reverse, with inertia; braking is quicker than speeding up
            float max = moveSpeed * DamageSpeedFactor * (NitroActive ? nitroMultiplier : 1f);
            float target = throttle >= 0f ? throttle * max : throttle * max * reverseSpeedFactor;
            bool slowingDown = Mathf.Abs(target) < Mathf.Abs(currentSpeed) || Mathf.Sign(target) != Mathf.Sign(currentSpeed);
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, (slowingDown ? braking : acceleration) * dt);

            // hull rotation: A/D by hand, otherwise swing round after the turret; a bit slower at full speed
            float turn = turnSpeed * Mathf.Lerp(1f, turnAtSpeedFactor, Speed01) * Mathf.Lerp(1f, DamageSpeedFactor, 0.6f) * dt;
            if (Mathf.Abs(steer) > 0.1f)
                rb.MoveRotation(rb.rotation - steer * turn);
            else if (CanAct && turret != null)
                rb.MoveRotation(rb.rotation + Mathf.Clamp(Mathf.DeltaAngle(rb.rotation, turret.AimAngle), -turn, turn));

            rb.linearVelocity = (Vector2)transform.up * currentSpeed;
            rb.angularVelocity = 0f;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            // pushing into a wall bleeds speed, so the hull does not keep "stored" momentum
            int layer = collision.gameObject.layer;
            if (layer == Layers.Walls || layer == Layers.Cover)
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, braking * 0.5f * Time.fixedDeltaTime);
        }
    }
}
