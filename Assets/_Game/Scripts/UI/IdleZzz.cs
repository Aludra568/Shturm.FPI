using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RicochetBerlin
{
    /// <summary>Little joke: leave the controls alone for a few seconds and the tank "falls asleep" - Z's float up.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class IdleZzz : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] Camera cam;
        [SerializeField] float idleDelay = 4f;
        [SerializeField] float interval = 0.7f;
        [SerializeField] float lifetime = 1.8f;

        VisualElement hud;
        float idle;
        float timer;
        int count;

        void Start() => hud = GetComponent<UIDocument>().rootVisualElement.Q("hud");

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || gm.Player.Health.IsDead || IsActive())
            {
                idle = 0f;
                timer = 0f;
                return;
            }

            idle += Time.deltaTime;
            if (idle < idleDelay) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            StartCoroutine(Float(gm.Player.transform.position, count++));
        }

        bool IsActive() =>
            input.Move.sqrMagnitude > 0.01f || Mathf.Abs(input.AimTurn) > 0.05f || input.FireHeld || input.FocusHeld;

        IEnumerator Float(Vector2 world, int index)
        {
            var z = new Label(index % 3 == 2 ? "Z" : "z") { pickingMode = PickingMode.Ignore };
            z.AddToClassList("zzz");
            z.style.fontSize = 22 + (index % 3) * 10;
            hud.Add(z);

            float sway = Random.Range(0f, 6.28f);
            for (float t = 0f; t < lifetime; t += Time.deltaTime)
            {
                float k = t / lifetime;
                Vector3 vp = cam.WorldToViewportPoint(world);
                // drift up and to the right on screen, wobbling like a sleepy cartoon
                float x = vp.x * hud.layout.width + 30f + k * 40f + Mathf.Sin(sway + t * 4f) * 8f;
                float y = (1f - vp.y) * hud.layout.height - 50f - k * 90f;
                z.style.left = x;
                z.style.top = y;
                z.style.opacity = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
                z.style.rotate = new Rotate(-15f + Mathf.Sin(sway + t * 3f) * 10f);
                yield return null;
            }
            z.RemoveFromHierarchy();
        }
    }
}
