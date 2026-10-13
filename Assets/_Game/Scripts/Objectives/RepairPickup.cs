using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Repair kit dropped by downed drones: rewards fighting instead of hiding.</summary>
    public class RepairPickup : MonoBehaviour
    {
        [SerializeField] float heal = 15f;
        [SerializeField] float lifetime = 20f;
        [SerializeField] float magnetRadius = 2.5f;
        [SerializeField] float magnetSpeed = 7f;
        [SerializeField] SpriteRenderer icon;

        float age;

        void Update()
        {
            age += Time.deltaTime;
            if (age > lifetime) { Destroy(gameObject); return; }

            // bob, and blink during the last seconds
            float bob = 1f + Mathf.Sin(age * 5f) * 0.08f;
            icon.transform.localScale = Vector3.one * bob;
            icon.enabled = age < lifetime - 4f || Mathf.Repeat(age * 6f, 1f) < 0.6f;

            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            Vector2 to = (Vector2)gm.Player.transform.position - (Vector2)transform.position;
            if (to.magnitude < magnetRadius)
                transform.position += (Vector3)(to.normalized * magnetSpeed * Time.deltaTime);
            if (to.magnitude < 0.6f) Collect(gm.Player);
        }

        void Collect(PlayerTank player)
        {
            if (player.Health.Current >= player.Health.Max) return;
            player.Health.Heal(heal);
            Fx.Instance?.Ping(transform.position, new Color(0.4f, 1f, 0.5f), 1.6f);
            Audio.Play(Audio.Bank?.repair, transform.position, 0.8f);
            Destroy(gameObject);
        }
    }
}
