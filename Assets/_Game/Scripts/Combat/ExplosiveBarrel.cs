using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Fuel barrel: one shell sets it off. Great for clearing drone packs with a bank shot.</summary>
    [RequireComponent(typeof(Health))]
    public class ExplosiveBarrel : MonoBehaviour
    {
        [SerializeField] float radius = 2.4f;
        [SerializeField] float damage = 40f;

        void Awake() => GetComponent<Health>().Died += Explode;

        void Explode()
        {
            Explosion.Detonate(transform.position, radius, damage, 1.3f, gameObject);
            Fx.Instance?.Scorch(transform.position, 1.6f);
            Destroy(gameObject);
        }
    }
}
