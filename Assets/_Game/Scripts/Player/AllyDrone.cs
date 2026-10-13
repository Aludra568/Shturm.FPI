using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Small friendly drone from a supply crate. Circles the tank; when an enemy drone shows up in the
    /// light it dives at it and blows up - one use, a little damage, but it buys you time.
    /// </summary>
    public class AllyDrone : MonoBehaviour
    {
        [SerializeField] float orbitRadius = 1.1f;
        [SerializeField] float orbitSpeed = 90f;
        [SerializeField] float followSharpness = 6f;
        [SerializeField] float diveSpeed = 4f;
        [SerializeField] float targetRange = 7f;
        [SerializeField] float damage = 2f;
        [SerializeField] Transform body;
        [SerializeField] Transform[] rotors;

        /// <summary>Helper drones currently flying (HUD).</summary>
        public static int Count { get; private set; }

        PlayerTank owner;
        float angle;
        Drone target;

        void OnEnable() => Count++;
        void OnDisable() => Count--;

        public void Init(PlayerTank tank, float startAngle)
        {
            owner = tank;
            angle = startAngle;
        }

        void Update()
        {
            foreach (Transform r in rotors) r.Rotate(0, 0, 1600f * Time.deltaTime);
            if (owner == null || owner.Health.IsDead) { Pop(); return; }
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            if (target == null || target.Health.IsDead) target = FindTarget();
            if (target != null) Dive();
            else Orbit();
        }

        void Orbit()
        {
            angle += orbitSpeed * Time.deltaTime;
            Vector2 slot = (Vector2)owner.transform.position + (Vector2)(Quaternion.Euler(0, 0, angle) * Vector3.up) * orbitRadius;
            Vector2 next = Vector2.Lerp(transform.position, slot, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Face(next - (Vector2)transform.position);
            transform.position = next;
        }

        void Dive()
        {
            Vector2 to = (Vector2)target.transform.position - (Vector2)transform.position;
            Face(to);
            transform.position += (Vector3)(to.normalized * diveSpeed * Time.deltaTime);
            if (to.magnitude > 0.5f) return;

            target.Health.TakeDamage(damage, to.normalized);
            Fx.Instance?.Explosion(transform.position, 0.45f);
            Audio.Play(Audio.Bank?.explosion, transform.position, 0.5f, 1.5f);
            Destroy(gameObject);
        }

        /// <summary>Nearest enemy drone the tank can actually see (allies don't fight blind).</summary>
        Drone FindTarget()
        {
            Drone best = null;
            float bestDist = targetRange;
            foreach (Drone d in Drone.Alive)
            {
                if (d.HiddenFromPlayer || d.Health.IsDead) continue;
                float dist = Vector2.Distance(d.transform.position, owner.transform.position);
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = d;
            }
            return best;
        }

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) body.up = dir;
        }

        void Pop()
        {
            Fx.Instance?.Sparks(transform.position, Vector2.up, new Color(0.5f, 0.9f, 1f), 6, 3f);
            Destroy(gameObject);
        }
    }
}
