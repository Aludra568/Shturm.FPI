using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Turns the text map into GameObjects: ground, merged building blocks with shadow casters,
    /// rubble cover, diagonal slabs, props and markers. Run from the editor (Ricochet ▸ Rebuild Level)
    /// so the result is baked into the scene and visible to designers.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        [SerializeField] TextAsset mapFile;
        [SerializeField] LevelPalette palette;
        [SerializeField] int decorSeed = 1945;

        public TextAsset MapFile => mapFile;

        LevelMap map;
        System.Random rng;

        public void Build()
        {
            Clear();
            map = new LevelMap(mapFile.text);
            rng = new System.Random(decorSeed);

            BuildGround();
            BuildBuildings();
            BuildRubble();
            BuildSlabs();
            BuildProps();
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        // ------------------------------------------------------------------ ground

        void BuildGround()
        {
            var ground = Tiled("Ground", palette.ground, new Vector2(map.Width, map.Height), SortOrder.Ground, transform);
            ground.transform.position = new Vector2(map.Width, map.Height) * 0.5f;

            Transform yards = Group("Courtyards");
            foreach (RectInt r in map.MergeRects(c => c == ','))
                PlaceRect(Tiled("Courtyard", palette.courtyard, r.size, SortOrder.Courtyard, yards), r);
        }

        // ------------------------------------------------------------------ solid geometry

        void BuildBuildings()
        {
            Transform walls = Group("Buildings", Layers.Walls);
            MakeStaticComposite(walls.gameObject);

            foreach (RectInt r in map.MergeRects(c => c == '#'))
            {
                SpriteRenderer roof = Tiled("Block", palette.roof, r.size, SortOrder.Walls, walls);
                roof.gameObject.layer = Layers.Walls;
                PlaceRect(roof, r);
                // every block a slightly different shade of burnt brick and concrete
                float shade = Range(0.85f, 1.05f);
                roof.color = new Color(shade, shade * Range(0.95f, 1f), shade * Range(0.92f, 1f));

                if (palette.dropShadow != null)
                {
                    SpriteRenderer drop = Sprite("DropShadow", palette.dropShadow, SortOrder.Decals - 1, walls);
                    drop.drawMode = SpriteDrawMode.Sliced;
                    drop.size = (Vector2)r.size + new Vector2(1.4f, 1.4f);
                    drop.color = new Color(0f, 0f, 0f, 0.75f);
                    drop.transform.position = r.center + new Vector2(0.25f, -0.3f);
                }

                var box = roof.gameObject.AddComponent<BoxCollider2D>();
                box.size = r.size;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;

                var caster = roof.gameObject.AddComponent<ShadowCaster2D>();
                caster.selfShadows = false;
                caster.castsShadows = true;
            }
        }

        void BuildRubble()
        {
            Transform cover = Group("Rubble", Layers.Cover);
            MakeStaticComposite(cover.gameObject);

            foreach (Vector2Int c in map.FindAll('r'))
            {
                var piece = Sprite("Rubble", Pick(palette.rubble), SortOrder.Props, cover);
                piece.gameObject.layer = Layers.Cover;
                piece.transform.position = map.CellCenter(c);
                piece.transform.rotation = RandomRotation();
                piece.transform.localScale = Vector3.one * Range(1.05f, 1.25f);
                GroundShadow(map.CellCenter(c), Vector2.one * 0.9f, cover);

                // collider stays axis aligned (child of a rotated sprite would tilt it)
                // round piles get round hitboxes - the tank slides round them instead of snagging on corners
                var col = new GameObject("RubbleCollider").AddComponent<CircleCollider2D>();
                col.gameObject.layer = Layers.Cover;
                col.transform.SetParent(cover, false);
                col.transform.position = map.CellCenter(c);
                col.radius = 0.48f;
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }

        void BuildSlabs()
        {
            Transform slabs = Group("Slabs", Layers.Walls);
            var used = new HashSet<Vector2Int>();

            foreach (char kind in new[] { '/', '\\' })
            {
                // '/' climbs up-right, '\' goes down-right (world space, y up)
                var step = kind == '/' ? new Vector2Int(1, 1) : new Vector2Int(1, -1);
                foreach (Vector2Int start in map.FindAll(kind))
                {
                    if (used.Contains(start) || map[start.x - step.x, start.y - step.y] == kind) continue;
                    int length = 0;
                    var cell = start;
                    while (map[cell.x, cell.y] == kind) { used.Add(cell); length++; cell += step; }

                    Vector2 a = map.CellCenter(start);
                    Vector2 b = map.CellCenter(start + step * (length - 1));
                    Vector2 dir = ((Vector2)step).normalized;
                    float len = Vector2.Distance(a, b) + 1.2f;

                    SpriteRenderer slab = Tiled("Slab", palette.slab, new Vector2(0.62f, len), SortOrder.Walls, slabs);
                    slab.gameObject.layer = Layers.Walls;
                    slab.transform.position = (a + b) * 0.5f;
                    slab.transform.up = dir;

                    var box = slab.gameObject.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(0.5f, len);
                    var caster = slab.gameObject.AddComponent<ShadowCaster2D>();
                    caster.selfShadows = false;
                }
            }
        }

        // ------------------------------------------------------------------ props, decals, markers

        void BuildProps()
        {
            Transform props = Group("Props");
            Transform decals = Group("Decals");
            Transform markers = Group("Markers");

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2 p = map.CellCenter(x, y);
                    switch (map[x, y])
                    {
                        case 'c': SolidProp("Crate", Pick(palette.crates), p, 0.62f, props); break;
                        case 'x': SolidProp("TankTrap", palette.tankTrap, p, 0.6f, props, round: true); break;
                        case 'b':
                            Prefab(palette.explosiveBarrel, p, props);
                            GroundShadow(p, Vector2.one * 0.6f, props);
                            break;
                        case 'f':
                            Decal("Scorch", palette.scorch, p, 1.4f, decals);
                            Prefab(palette.fire, p, props);
                            break;
                        case 'o': Decal("Crater", palette.crater, p, Range(1.3f, 1.9f), decals); break;
                        case 'w': Decal("Wire", palette.wire, p, 1.1f, decals); break;
                        case 't': Decal("DeadTree", Pick(palette.deadTrees), p, Range(0.9f, 1.3f), decals, SortOrder.Props); break;
                        case 'M':
                            Decal("Manhole", palette.manhole, p, 0.85f, decals);
                            Marker(MarkerKind.Hatch, p, markers);
                            break;
                        case 'S': Marker(MarkerKind.PlayerStart, p, markers); break;
                        case 'F': Marker(MarkerKind.FlagSpot, p, markers); break;
                        case 'E': Marker(MarkerKind.EvacSpot, p, markers); break;
                        case '.':
                            if (rng.NextDouble() < 0.012) Decal("Oil", Pick(palette.oilSpills), p, Range(0.6f, 1.1f), decals);
                            break;
                    }
                }
            }
        }

        void SolidProp(string name, Sprite sprite, Vector2 pos, float size, Transform parent, bool round = false)
        {
            var sr = Sprite(name, sprite, SortOrder.Props, parent);
            sr.gameObject.layer = Layers.Cover;
            sr.transform.position = pos;
            sr.transform.rotation = RandomRotation();
            if (round) sr.gameObject.AddComponent<CircleCollider2D>().radius = sr.sprite.bounds.extents.x * 0.85f;
            else sr.gameObject.AddComponent<BoxCollider2D>().size = sr.sprite.bounds.size * 0.9f;
            sr.transform.localScale = Vector3.one * (size / Mathf.Max(sr.sprite.bounds.size.x, 0.01f));
            GroundShadow(pos, new Vector2(size, size), parent);
        }

        /// <summary>Soft dark footprint under anything solid - collision you can see.</summary>
        void GroundShadow(Vector2 pos, Vector2 size, Transform parent)
        {
            if (palette.dropShadow == null) return;
            SpriteRenderer drop = Sprite("Shadow", palette.dropShadow, SortOrder.Props - 1, parent);
            drop.drawMode = SpriteDrawMode.Sliced;
            drop.size = size + new Vector2(0.5f, 0.5f);
            drop.color = new Color(0f, 0f, 0f, 0.8f);
            drop.transform.position = pos + new Vector2(0.12f, -0.15f);
        }

        /// <summary>Walk-over decoration: muted and slightly transparent so it never reads as an obstacle.</summary>
        void Decal(string name, Sprite sprite, Vector2 pos, float size, Transform parent, int order = SortOrder.Decals)
        {
            var sr = Sprite(name, sprite, order, parent);
            sr.color = new Color(0.55f, 0.55f, 0.58f, 0.7f);
            sr.transform.position = pos;
            sr.transform.rotation = RandomRotation();
            sr.transform.localScale = Vector3.one * (size / Mathf.Max(sr.sprite.bounds.size.x, 0.01f));
        }

        void Prefab(GameObject prefab, Vector2 pos, Transform parent)
        {
            if (prefab == null) return;
#if UNITY_EDITOR
            var go = Application.isPlaying
                ? Instantiate(prefab, parent)
                : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            var go = Instantiate(prefab, parent);
#endif
            go.transform.position = pos;
        }

        void Marker(MarkerKind kind, Vector2 pos, Transform parent)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<MapMarker>().kind = kind;
        }

        // ------------------------------------------------------------------ helpers

        Transform Group(string name, int layer = 0)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        static void MakeStaticComposite(GameObject go)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        SpriteRenderer Sprite(string name, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.sharedMaterial = palette.litMaterial;
            return sr;
        }

        SpriteRenderer Tiled(string name, Sprite sprite, Vector2 size, int order, Transform parent)
        {
            SpriteRenderer sr = Sprite(name, sprite, order, parent);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            return sr;
        }

        static void PlaceRect(Component c, RectInt r) => c.transform.position = r.center;

        T Pick<T>(T[] items) => items[rng.Next(items.Length)];
        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        Quaternion RandomRotation() => Quaternion.Euler(0, 0, rng.Next(360));
    }
}
