using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Booby-trap stretched across a narrow passage: a barely visible wire. Drive into it and the tank
    /// instantly loses half its armour. Shoot the detonator box at the side of the passage to disarm it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Tripwire : MonoBehaviour
    {
        [SerializeField] SpriteRenderer wire;
        [SerializeField] Health detonator;
        [SerializeField] SpriteRenderer detonatorLed;
        [Tooltip("Share of the tank's max armour lost when it trips the wire.")]
        [SerializeField, Range(0, 1)] float damageShare = 0.5f;

        bool spent;

        /// <summary>Stretches the wire from <paramref name="a"/> to <paramref name="b"/>; the detonator sits at <paramref name="a"/>.</summary>
        public void Setup(Vector2 a, Vector2 b)
        {
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = b - a;
            transform.SetPositionAndRotation(mid, Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
            wire.size = new Vector2(dir.magnitude, wire.size.y);
            var trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(dir.magnitude, 0.25f);
            detonator.transform.position = a + dir.normalized * 0.25f;
        }

        void Awake() => detonator.Died += Disarm;

        void Update()
        {
            // the detonator's LED blinks faintly - the only hint in the dark
            if (detonatorLed != null) detonatorLed.enabled = !spent && Mathf.Repeat(Time.time * 1.3f + transform.position.x, 1f) < 0.12f;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (spent || other.GetComponentInParent<PlayerTank>() == null) return;
            spent = true;
            Health tank = other.GetComponentInParent<Health>();
            tank.TakeDamage(tank.Max * damageShare, ((Vector2)other.transform.position - (Vector2)transform.position).normalized);
            Fx.Instance?.Sparks(other.transform.position, Vector2.up, new Color(1f, 0.6f, 0.25f), 20, 6f);
            Fx.Instance?.LightFlash(other.transform.position, new Color(1f, 0.55f, 0.2f), 3f, 3f, 0.25f);
            Fx.Instance?.Dust(other.transform.position, Vector2.up, 8);
            Audio.Play(Audio.Bank?.playerHit, other.transform.position, 1f, 0.8f);
            CameraRig.Instance?.AddTrauma(0.6f);
            Destroy(gameObject);
        }

        void Disarm()
        {
            if (spent) return;
            spent = true;
            Fx.Instance?.Sparks(detonator.transform.position, Vector2.up, new Color(1f, 0.8f, 0.4f), 12, 4f);
            Fx.Instance?.Dust(transform.position, Vector2.up, 4);
            Audio.Play(Audio.Bank?.wallImpact, transform.position, 0.8f, 1.4f);
            Destroy(gameObject);
        }
    }
}
