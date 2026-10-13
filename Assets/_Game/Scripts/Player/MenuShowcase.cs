using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Main-menu showroom behind the menu: the tank slowly turns round on the spot, the turret looks
    /// about and fires blank ricocheting shots now and then. Hands the aim over when the round starts.
    /// </summary>
    public class MenuShowcase : MonoBehaviour
    {
        [SerializeField] float hullTurnSpeed = 22f;
        [Tooltip("How far the turret swings to each side of the hull, degrees.")]
        [SerializeField] float turretSweep = 55f;
        [SerializeField] float sweepPeriod = 5f;
        [SerializeField] float shotInterval = 2.4f;

        Rigidbody2D rb;
        TankTurret turret;
        float time, nextShot = 1.5f;
        bool wasMenu;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            turret = GetComponentInChildren<TankTurret>();
        }

        static bool InMenu => GameManager.Instance != null && GameManager.Instance.State == GameState.Menu;

        void FixedUpdate()
        {
            if (InMenu) rb.MoveRotation(rb.rotation + hullTurnSpeed * Time.fixedDeltaTime);
        }

        void Update()
        {
            bool menu = InMenu;
            if (!menu)
            {
                // round started: the player takes over from where the turret is looking now
                if (wasMenu) turret.ResetAim();
                wasMenu = false;
                return;
            }
            wasMenu = true;

            time += Time.deltaTime;
            float offset = Mathf.Sin(time * Mathf.PI * 2f / sweepPeriod) * turretSweep;
            turret.transform.rotation = Quaternion.Euler(0, 0, rb.rotation + offset);

            nextShot -= Time.deltaTime;
            if (nextShot <= 0f)
            {
                nextShot = shotInterval * Random.Range(0.8f, 1.25f);
                turret.ShowcaseFire();
            }
        }
    }
}
