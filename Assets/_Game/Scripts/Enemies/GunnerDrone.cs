using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Keeps its distance and fires ricocheting shells in bursts, with a visible wind-up so the
    /// player can react. The boss helicopter is the same behaviour plus HelicopterBoss armour/phases.
    /// </summary>
    public class GunnerDrone : Drone
    {
        [SerializeField] Projectile shellPrefab;
        [SerializeField] float preferredMin = 4.5f;
        [SerializeField] float preferredMax = 8f;
        [SerializeField] float fireRange = 11f;
        [SerializeField] float fireInterval = 2f;
        [SerializeField] float windUp = 0.5f;
        [SerializeField] int burstCount = 1;
        [SerializeField] float burstSpacing = 0.14f;
        [SerializeField] float spread = 4f;
        [SerializeField] float muzzleDistance = 0.55f;
        [SerializeField] Light2D eyeLight;

        float fireTimer;
        float windUpTimer = -1f;
        int shotsLeft;
        float burstTimer;
        float strafeSign = 1f;
        float strafeTimer;
        float eyeBase;

        protected override void Awake()
        {
            base.Awake();
            fireTimer = fireInterval * Random.Range(0.6f, 1.1f);
            if (eyeLight != null) eyeBase = eyeLight.intensity;
        }

        protected override Vector2 DesiredVelocity()
        {
            Vector2 to = ToPlayer;
            float d = to.magnitude;
            if (!HasLineOfFire(to) || d > preferredMax) return ChaseDirection() * speed;

            Vector2 dir = to / d;
            Vector2 strafe = new Vector2(-dir.y, dir.x) * strafeSign * 0.6f;
            if (d < preferredMin) return (-dir + strafe) * speed * 0.8f;
            return strafe * speed * 0.7f;
        }

        protected override Vector2 FacingDirection() => player != null && Active ? ToPlayer : base.FacingDirection();

        /// <summary>Burst size and pause between bursts (the boss gets angrier each phase).</summary>
        public void SetAttack(int burst, float interval)
        {
            burstCount = burst;
            fireInterval = interval;
        }

        protected override void Update()
        {
            base.Update();
            if (!Active || player == null || !IsGameRunning || Stunned) return;

            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(1.2f, 2.5f);
                strafeSign = -strafeSign;
            }

            Vector2 to = ToPlayer;
            bool canShoot = to.magnitude < fireRange && HasLineOfFire(to) && !PlayerConcealed;

            if (shotsLeft > 0)
            {
                burstTimer -= Time.deltaTime;
                if (burstTimer <= 0f)
                {
                    Shoot(to);
                    shotsLeft--;
                    burstTimer = burstSpacing;
                }
                return;
            }

            if (windUpTimer >= 0f)
            {
                windUpTimer += Time.deltaTime;
                if (eyeLight != null) eyeLight.intensity = eyeBase + windUpTimer / windUp * 3f;
                if (windUpTimer >= windUp)
                {
                    windUpTimer = -1f;
                    if (eyeLight != null) eyeLight.intensity = eyeBase;
                    shotsLeft = burstCount;
                    burstTimer = 0f;
                }
                return;
            }

            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f && canShoot)
            {
                fireTimer = fireInterval * Random.Range(0.85f, 1.15f);
                windUpTimer = 0f;
            }
        }

        void Shoot(Vector2 to)
        {
            Vector2 dir = Quaternion.Euler(0, 0, Random.Range(-spread, spread)) * to.normalized;
            Vector2 origin = transform.position;
            Projectile shell = Instantiate(shellPrefab, origin, Quaternion.identity);
            shell.SetDamage(shell.Damage * Difficulty.EnemyDamage);
            shell.Launch(origin, dir, muzzleDistance, gameObject);
            Fx.Instance?.LightFlash(origin + dir * muzzleDistance, new Color(1f, 0.3f, 0.25f), 1.5f, 2.5f, 0.08f);
            Audio.Play(Audio.Bank?.droneShot, origin, 0.6f);
        }
    }
}
