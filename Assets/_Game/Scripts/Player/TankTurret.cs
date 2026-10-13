using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Turret turns 1:1 with horizontal mouse movement (the camera follows it), independent of the
    /// hull - the hull then swings round after it. Fires ricocheting shells.
    /// </summary>
    public class TankTurret : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] SpriteRenderer barrel;
        [SerializeField] Projectile shellPrefab;
        [SerializeField] float fireCooldown = 0.32f;
        [SerializeField] float muzzleDistance = 0.75f;
        [Tooltip("Shots before the gun has to reload.")]
        [SerializeField] int magazineSize = 5;
        [SerializeField] float reloadTime = 3f;
        [SerializeField] float recoilDistance = 0.12f;
        [Header("Twin barrel pickup")]
        [SerializeField] float twinCooldownFactor = 0.6f;
        [SerializeField] float twinSpacing = 0.13f;
        [Header("Quick turn (Q)")]
        [Tooltip("How fast the 180 degree about-turn sweeps, degrees per second.")]
        [SerializeField] float turnAroundSpeed = 720f;

        public Vector2 AimDirection => transform.up;
        /// <summary>Where the player wants to aim (world angle, 0 = up). The camera follows this.</summary>
        public float AimAngle { get; private set; }
        public int MagazineSize => magazineSize;
        public int Ammo { get; private set; }
        public bool Reloading => reloadLeft > 0f;
        public float ReloadLeft => reloadLeft;
        /// <summary>Twin barrel found in a supply crate - it stays for the rest of the round.</summary>
        public bool TwinActive { get; private set; }

        float reloadLeft;
        Vector3 barrelRest;
        float cooldown;
        float recoil;
        float damage = 1f;
        float turnAroundLeft;
        Sprite singleSprite, twinSprite;
        GameObject hull;

        void Awake()
        {
            barrelRest = barrel.transform.localPosition;
            hull = GetComponentInParent<Rigidbody2D>().gameObject;
            ResetAim();
            Ammo = magazineSize;
        }

        /// <summary>Aim straight along the current turret direction (e.g. after spawning).</summary>
        public void ResetAim() => AimAngle = transform.eulerAngles.z;

        /// <summary>Applies the tank class picked in the menu.</summary>
        public void Configure(TankClass c)
        {
            fireCooldown = c.fireCooldown;
            damage = c.shellDamage;
            muzzleDistance = c.muzzleDistance;
            singleSprite = c.barrel;
            twinSprite = c.twinBarrel;
            barrel.sprite = singleSprite;
            barrelRest = new Vector3(0f, c.barrelOffset, 0f);
            barrel.transform.localPosition = barrelRest;
        }

        public void EnableTwin() => TwinActive = true;

        /// <summary>Main-menu showroom: a blank shot with flash, recoil and sound (no ammo used, hurts nothing).</summary>
        public void ShowcaseFire()
        {
            recoil = recoilDistance;
            Vector2 dir = transform.up;
            Vector2 origin = transform.position;
            Projectile shell = Instantiate(shellPrefab, origin, transform.rotation);
            shell.MakeHarmless();
            shell.Launch(origin, dir, muzzleDistance, hull);
            Fx.Instance?.MuzzleFlash(origin + dir * muzzleDistance, dir);
            CameraRig.Instance?.AddTrauma(0.08f);
            Audio.Play(Audio.Bank?.tankShot, origin, 0.45f, 1.05f);
        }

        void Update()
        {
            bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
            if (playing) Aim();

            Sprite wanted = TwinActive && twinSprite != null ? twinSprite : singleSprite;
            if (wanted != null && barrel.sprite != wanted) barrel.sprite = wanted;

            cooldown -= Time.deltaTime;
            if (reloadLeft > 0f)
            {
                reloadLeft -= Time.deltaTime;
                if (reloadLeft <= 0f)
                {
                    reloadLeft = 0f;
                    Ammo = magazineSize;
                    Audio.Play(Audio.Bank?.reloaded, transform.position, 0.8f);
                }
            }
            if (playing && input.FireHeld && cooldown <= 0f && Ammo > 0) Fire();

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.2f);
            barrel.transform.localPosition = barrelRest - Vector3.up * recoil;
        }

        void Aim()
        {
            AimAngle -= input.AimTurn; // mouse right = clockwise

            // Q: sweep the aim (and with it the camera and hull) round by 180 degrees
            if (input.TurnAroundPressed && turnAroundLeft <= 0f) turnAroundLeft = 180f;
            if (turnAroundLeft > 0f)
            {
                float step = Mathf.Min(turnAroundLeft, turnAroundSpeed * Time.deltaTime);
                AimAngle += step;
                turnAroundLeft -= step;
            }
#if UNITY_EDITOR
            if (input.TestAimPoint.HasValue)
            {
                Vector2 to = input.TestAimPoint.Value - (Vector2)transform.position;
                AimAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
            }
#endif
            AimAngle = Mathf.Repeat(AimAngle, 360f);
            transform.rotation = Quaternion.Euler(0, 0, AimAngle);
        }

        void Fire()
        {
            Ammo--;
            if (Ammo <= 0) reloadLeft = reloadTime; // magazine empty: 5 s reload
            bool twin = TwinActive;
            cooldown = fireCooldown * (twin ? twinCooldownFactor : 1f);
            recoil = recoilDistance;
            Vector2 dir = transform.up;
            Vector2 origin = transform.position;

            if (twin)
            {
                Vector2 side = new Vector2(dir.y, -dir.x) * twinSpacing;
                Launch(origin + side, dir);
                Launch(origin - side, dir);
            }
            else
            {
                Launch(origin, dir);
            }

            Fx.Instance?.MuzzleFlash(origin + dir * muzzleDistance, dir);
            CameraRig.Instance?.AddTrauma(twin ? 0.22f : 0.18f);
            Audio.Play(Audio.Bank?.tankShot, origin, 0.75f, twin ? 1.2f : 1.05f);
        }

        void Launch(Vector2 origin, Vector2 dir)
        {
            Projectile shell = Instantiate(shellPrefab, origin, transform.rotation);
            shell.SetDamage(damage);
            shell.Launch(origin, dir, muzzleDistance, hull);
        }
    }
}
