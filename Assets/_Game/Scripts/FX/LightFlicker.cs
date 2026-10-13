using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>Burning-ruin fire: flickering light plus rising embers.</summary>
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] Light2D fireLight;
        [SerializeField] float baseIntensity = 1.1f;
        [SerializeField] float flicker = 0.35f;
        [SerializeField] float speed = 6f;
        [SerializeField] float emberInterval = 0.15f;
        [SerializeField] Sprite ember;

        float seed;
        float emberTimer;

        void Awake() => seed = Random.value * 100f;

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            fireLight.intensity = baseIntensity + (n - 0.5f) * 2f * flicker;

            emberTimer -= Time.deltaTime;
            if (emberTimer > 0f || Fx.Instance == null) return;
            emberTimer = emberInterval * Random.Range(0.6f, 1.4f);
            Vector2 v = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.3f));
            Fx.Instance.Spawn(ember, (Vector2)transform.position + Random.insideUnitCircle * 0.3f, v, Random.Range(0.6f, 1.2f), 0.18f, 0.05f,
                              new Color(1f, 0.7f, 0.2f), new Color(1f, 0.2f, 0f, 0f), unlit: true);
        }
    }
}
