using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>The objective flag. Hidden in the dark until seen; drive over it to capture.</summary>
    [RequireComponent(typeof(FogHidden))]
    public class FlagPickup : MonoBehaviour
    {
        [SerializeField] Transform banner;
        [SerializeField] float waveSpeed = 4f;

        bool taken;

        void Update()
        {
            if (banner != null) banner.localScale = new Vector3(1f + Mathf.Sin(Time.time * waveSpeed) * 0.08f, 1f, 1f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (taken || other.GetComponentInParent<PlayerTank>() == null) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            taken = true;
            GameManager.Instance.TakeFlag();
            Fx.Instance?.Ping(transform.position, new Color(1f, 0.85f, 0.3f), 4f);
            Fx.Instance?.LightFlash(transform.position, new Color(1f, 0.8f, 0.4f), 3f, 5f, 0.5f);
            Audio.Play(Audio.Bank?.flagPickup, null, 1f, 1f, 0f);
            CameraRig.Instance?.AddTrauma(0.3f);
            gameObject.SetActive(false);
        }
    }
}
