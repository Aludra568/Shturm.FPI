using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>A playable tank: stats and part sprites. Picked in the main menu.</summary>
    [CreateAssetMenu(menuName = "Ricochet Berlin/Tank Class")]
    public class TankClass : ScriptableObject
    {
        public string displayName = "ВОЛК";
        [TextArea] public string description = "Сбалансированный";

        [Header("Stats")]
        public float maxHealth = 100f;
        public float moveSpeed = 4.4f;
        public float turnSpeed = 95f;
        public float fireCooldown = 0.32f;
        public float shellDamage = 1f;
        [Tooltip("Oval hitbox: width x length of hull plus tracks.")]
        public Vector2 colliderSize = new(0.66f, 0.9f);

        [Tooltip("One-per-round special on Shift.")]
        public AbilityKind ability = AbilityKind.Nitro;

        [Header("Menu")]
        [Tooltip("Whole-tank picture (greyscale, tinted with the colour) drawn at true relative size.")]
        public Texture2D preview;
        [Tooltip("Index into TankSelection.Colors used when this tank is picked.")]
        public int defaultColorIndex;
        [Range(1, 3)] public int speedRating = 2;
        [Range(1, 3)] public int armorRating = 2;
        [Range(1, 3)] public int fireRating = 2;

        [Header("Sprites (forward = up, hull/turret tinted with the chosen colour)")]
        public Sprite hull;
        public Sprite track;
        public Sprite turret;
        public Sprite barrel;
        public Sprite twinBarrel;
        public Sprite damage;
        public Sprite trackDebris;
        [Tooltip("Distance of each track from the hull centre.")]
        public float trackOffset = 0.3f;
        [Tooltip("Distance from the turret centre to the barrel centre.")]
        public float barrelOffset = 0.3f;
        public float muzzleDistance = 0.75f;
    }
}
