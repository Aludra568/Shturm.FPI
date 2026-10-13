using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Fast suicide drone. Rushes the tank, arms with a beeping blink when close, then detonates.
    /// Shoot it early - or bank a shell around the corner it is coming from.
    /// </summary>
    public class KamikazeDrone : Drone
    {
        [SerializeField] float armRange = 1.7f;
        [SerializeField] float armTime = 0.75f;
        [SerializeField] float blastRadius = 1.8f;
        [SerializeField] float blastDamage = 24f;
        [SerializeField] Light2D warningLight;
        [Header("Dash: faster than any tank once it has you in reach")]
        [SerializeField] float dashSpeed = 2.4f;
        [SerializeField] float dashRange = 4f;

        float armTimer = -1f;
        bool exploded;

        public bool Armed => armTimer >= 0f;
        /// <summary>Close and with a clear path: charging at full dash speed.</summary>
        public bool Dashing { get; private set; }

        protected override Vector2 DesiredVelocity()
        {
            Vector2 to = ToPlayer;
            Dashing = Armed || (to.magnitude < dashRange && HasClearPath(to));
            return ChaseDirection() * (Dashing ? dashSpeed : speed);
        }

        protected override float MaxSpeed => Mathf.Max(speed, dashSpeed);

        protected override void Awake()
        {
            base.Awake();
            dashSpeed *= Difficulty.EnemySpeed;
        }

        protected override void Update()
        {
            base.Update();
            if (!Active || player == null || !IsGameRunning) return;

            if (!Armed && !PlayerConcealed && ToPlayer.magnitude < armRange && HasLineOfFire(ToPlayer))
            {
                armTimer = 0f;
                Audio.Play(Audio.Bank?.arming, transform.position, 0.9f);
            }

            if (Armed)
            {
                armTimer += Time.deltaTime;
                if (warningLight != null)
                    warningLight.intensity = Mathf.PingPong(armTimer * 12f, 1f) * 2f * (fog != null && fog.IsVisible ? 1f : 0.4f);
                if (armTimer >= armTime) Detonate();
            }
        }

        void Detonate()
        {
            if (exploded) return;
            exploded = true;
            health.SetInvulnerable(true);
            Explosion.Detonate(transform.position, blastRadius, blastDamage * Difficulty.EnemyDamage, 1f, gameObject);
            Remove(creditKill: false, explosionFx: false);
        }

        protected override void OnDied()
        {
            // shot down: still blows up, smaller - can take its friends with it
            if (!exploded)
            {
                exploded = true;
                Explosion.Detonate(transform.position, blastRadius * 0.8f, blastDamage * 0.6f * Difficulty.EnemyDamage, 0.8f, gameObject);
            }
            Remove(creditKill: true, explosionFx: false);
        }
    }
}
