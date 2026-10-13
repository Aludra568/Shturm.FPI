using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Leaves fading track prints on the ground - shows where you have already searched.</summary>
    public class TrackMarks : MonoBehaviour
    {
        [SerializeField] Sprite trackSprite;
        [SerializeField] Material material;
        [SerializeField] int poolSize = 160;
        [SerializeField] float spacing = 0.35f;
        [SerializeField] float lifetime = 25f;
        [SerializeField] float size = 0.8f;

        SpriteRenderer[] marks;
        float[] born;
        int next;
        Vector2 lastPos;

        void Start()
        {
            var root = new GameObject("TrackMarks").transform;
            marks = new SpriteRenderer[poolSize];
            born = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var sr = new GameObject("track").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.sprite = trackSprite;
                sr.sharedMaterial = material;
                sr.sortingOrder = SortOrder.Tracks;
                sr.enabled = false;
                marks[i] = sr;
            }
            lastPos = transform.position;
        }

        void Update()
        {
            Vector2 pos = transform.position;
            if (Vector2.Distance(pos, lastPos) >= spacing)
            {
                lastPos = pos;
                SpriteRenderer sr = marks[next];
                born[next] = Time.time;
                next = (next + 1) % poolSize;
                sr.enabled = true;
                sr.transform.SetPositionAndRotation(pos, transform.rotation);
                sr.transform.localScale = Vector3.one * size;
            }

            for (int i = 0; i < poolSize; i++)
            {
                if (!marks[i].enabled) continue;
                float t = (Time.time - born[i]) / lifetime;
                if (t >= 1f) { marks[i].enabled = false; continue; }
                marks[i].color = new Color(0.1f, 0.08f, 0.07f, 0.45f * (1f - t));
            }
        }
    }
}
