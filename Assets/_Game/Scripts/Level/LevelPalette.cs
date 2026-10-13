using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Art and prefabs used to turn a text map into a level.</summary>
    [CreateAssetMenu(menuName = "Ricochet Berlin/Level Palette")]
    public class LevelPalette : ScriptableObject
    {
        [Header("Materials")]
        public Material litMaterial;
        public Material unlitMaterial;

        [Header("Ground & ruins")]
        public Sprite ground;
        public Sprite courtyard;
        public Sprite roof;
        [Tooltip("Soft 9-slice square drawn under buildings so they read as tall.")]
        public Sprite dropShadow;
        public Sprite[] rubble;
        public Sprite slab;

        [Header("Props & decals")]
        public Sprite[] crates;
        public Sprite tankTrap;
        public Sprite crater;
        public Sprite wire;
        public Sprite[] deadTrees;
        public Sprite manhole;
        public Sprite scorch;
        public Sprite[] oilSpills;

        [Header("Prefabs")]
        public GameObject explosiveBarrel;
        public GameObject fire;
    }
}
