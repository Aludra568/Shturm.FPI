using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// A shell that flies until it runs out of bounces. Unlimited range: it only dies on a unit
    /// or on the wall hit after its last ricochet. After the first bounce it can hit its own shooter.
    /// The player's shells can parry enemy shells in flight: the enemy shell is knocked back and
    /// becomes the player's.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        /// <summary>Every shell in flight (for shell-vs-shell parries).</summary>
        static readonly List<Projectile> Live = new();

        [SerializeField] Team team = Team.Player;
        [SerializeField] float speed = 15f;
        [SerializeField] float radius = 0.1f;
        [Tooltip("Generous radius for being parried by a player shell.")]
        [SerializeField] float parryRadius = 0.35f;
        [SerializeField] int maxBounces = 3;
        [SerializeField] float damage = 1f;
        [Tooltip("Damage dealt to the shooter by its own ricochet.")]
        [SerializeField] float selfDamage = 8f;
        [Tooltip("A shell must fly this far before it can hurt its shooter (no point-blank suicide).")]
        [SerializeField] float selfArmDistance = 2.5f;
        [SerializeField] float maxLifetime = 8f;
        [SerializeField] Color sparkColor = new Color(1f, 0.8f, 0.4f);
        [SerializeField] TrailRenderer trail;

        public Team Team => team;
        public int Bounces { get; private set; }

        static readonly Color ParriedColor = new Color(1f, 0.85f, 0.45f);

        GameObject owner;
        Collider2D ownerCollider;
        Vector2 position;
        Vector2 direction;
        float age;
        float traveled;
        float pendingDistance;
        bool dead;
        bool harmless;

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public float Damage => damage;
        public void SetDamage(float value) => damage = value;
        /// <summary>Menu showcase shot: bounces off everything and damages nothing.</summary>
        public void MakeHarmless() => harmless = true;

        /// <summary>
        /// Fires from the shooter's centre; the first <paramref name="muzzleDistance"/> is swept instantly,
        /// so a barrel poking into a wall still bounces instead of passing through it.
        /// </summary>
        public void Launch(Vector2 from, Vector2 dir, float muzzleDistance, GameObject shooter)
        {
            owner = shooter;
            ownerCollider = shooter != null ? shooter.GetComponentInChildren<Collider2D>() : null;
            position = from;
            direction = dir.normalized;
            pendingDistance = muzzleDistance;
            Step(0f);
            if (trail != null) trail.Clear();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > maxLifetime) { Despawn(); return; }
            Vector2 before = position;
            Step(speed * Time.deltaTime);
            if (team == Team.Player && !dead && !harmless) TryParry(before, position);
        }

        /// <summary>Did this player shell sweep through an enemy shell this frame? Knock it back.</summary>
        void TryParry(Vector2 from, Vector2 to)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Projectile enemy = Live[i];
                if (enemy == null || enemy.dead || enemy.team == Team.Player) continue;
                Vector2 closest = ClosestOnSegment(from, to, enemy.position);
                Vector2 offset = enemy.position - closest;
                if (offset.magnitude > enemy.parryRadius + radius) continue;

                Vector2 normal = offset.sqrMagnitude > 0.0001f ? offset.normalized : -enemy.direction;
                enemy.Parried(Vector2.Reflect(enemy.direction, normal));
                Fx.Instance?.Sparks(enemy.position, normal, ParriedColor, 14, 6f);
                Fx.Instance?.LightFlash(enemy.position, ParriedColor, 2f, 2.5f, 0.15f);
                Audio.Play(Audio.Bank?.ricochet, enemy.position, 0.9f, 1.5f);
                GameManager.Instance?.RegisterParry(enemy.position);
                Despawn();
                return;
            }
        }

        /// <summary>Knocked back by the player: now flies the other way and hurts drones.</summary>
        void Parried(Vector2 newDirection)
        {
            team = Team.Player;
            owner = null;
            ownerCollider = null;
            Bounces = 0;
            direction = newDirection.normalized;
            sparkColor = ParriedColor;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.color = ParriedColor;
            foreach (var l in GetComponentsInChildren<Light2D>()) l.color = ParriedColor;
            if (trail != null) trail.colorGradient = Fade(ParriedColor);
        }

        static Gradient Fade(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return g;
        }

        static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 0.000001f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        }

        void Step(float distance)
        {
            float remaining = distance + pendingDistance;
            pendingDistance = 0f;

            // a fast drone can fly INTO the shell between frames; a sweep starting inside a collider
            // ignores it, so check for overlap first or the shell would pass straight through
            if (HitOverlappingUnit()) return;

            // several bounces can happen in one frame (corners) - resolve them in sequence
            for (int guard = 0; guard < 6 && remaining > 0f; guard++)
            {
                Collider2D ignore = CanHitOwner ? null : ownerCollider;
                if (!Ricochet.Cast(position, radius, direction, remaining, Layers.Projectile, ignore, out RaycastHit2D hit))
                {
                    position += direction * remaining;
                    traveled += remaining;
                    break;
                }

                position = hit.centroid;
                remaining -= hit.distance;
                traveled += hit.distance;

                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null && CanDamage(health))
                {
                    HitUnit(health, hit.collider, hit.point, hit.normal);
                    return;
                }

                if (!Bounce(hit)) return;
            }

            transform.position = position;
            transform.up = direction;
        }

        static readonly Collider2D[] Overlaps = new Collider2D[8];

        bool HitOverlappingUnit()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = (1 << Layers.Player) | (1 << Layers.Enemy) | (1 << Layers.Cover), useTriggers = false };
            int count = Physics2D.OverlapCircle(position, radius, filter, Overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = Overlaps[i];
                if (col == ownerCollider && !CanHitOwner) continue;
                var health = col.GetComponentInParent<Health>();
                if (health == null || !CanDamage(health)) continue;
                Vector2 normal = ((Vector2)position - (Vector2)col.bounds.center).normalized;
                HitUnit(health, col, position, normal.sqrMagnitude > 0f ? normal : -direction);
                return true;
            }
            return false;
        }

        void HitUnit(Health health, Collider2D col, Vector2 point, Vector2 normal)
        {
            bool self = health.gameObject == owner;
            float dealt = self ? selfDamage : damage;
            // armoured targets take full damage only on their weak spot
            var armor = col.GetComponentInParent<IArmor>();
            if (armor != null && team == Team.Player) dealt *= armor.DamageMultiplier(normal, point);
            health.TakeDamage(dealt, direction);
            if (health.IsDead && team == Team.Player && health.Team == Team.Enemy && Bounces > 0)
                GameManager.Instance?.RegisterRicochetKill(point, Bounces);
            Fx.Instance?.BulletImpact(point, -direction, sparkColor, unitHit: true);
            Despawn();
        }

        bool CanHitOwner => Bounces > 0 && traveled >= selfArmDistance;

        bool CanDamage(Health health)
        {
            if (harmless) return false;
            if (health.gameObject == owner) return CanHitOwner;
            return health.Team != team;
        }

        /// <summary>Reflects off a surface. Returns false (and despawns) when out of bounces.</summary>
        bool Bounce(RaycastHit2D hit)
        {
            if (Bounces >= maxBounces)
            {
                Fx.Instance?.BulletImpact(hit.point, hit.normal, sparkColor, unitHit: false);
                Despawn();
                return false;
            }

            Bounces++;
            direction = Vector2.Reflect(direction, hit.normal).normalized;
            position = hit.centroid + hit.normal * Ricochet.Skin;
            Fx.Instance?.Ricochet(hit.point, hit.normal, sparkColor, Bounces);
            return true;
        }

        void Despawn()
        {
            if (dead) return;
            dead = true;
            if (trail != null)
            {
                // let the tracer fade out on its own instead of popping
                trail.transform.SetParent(null, true);
                trail.autodestruct = true;
                trail.emitting = false;
            }
            Destroy(gameObject);
        }
    }
}
