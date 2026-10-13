using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Behind-the-barrel camera: it rotates with the aim so the gun always points up the screen,
    /// sits close behind the tank and shows more space ahead (more still with the focused beam).
    /// Also applies trauma-based screen shake.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [SerializeField] float baseSize = 4f;
        [SerializeField] float menuZoom = -1.2f;
        [Tooltip("Main menu: the tank stands this far right of the screen centre (the menu is on the left).")]
        [SerializeField] float menuShift = 2.2f;
        [Header("Follow")]
        [SerializeField] float smoothTime = 0.1f;
        [Tooltip("How far ahead of the tank (along the aim) the view is centred - the tank sits low on screen.")]
        [SerializeField] float aheadOffset = 1.6f;
        [Tooltip("Ahead offset while the flashlight is focused (RMB) - look down the long beam.")]
        [SerializeField] float focusedAheadOffset = 4f;
        [SerializeField] float rotationSmoothTime = 0.22f;
        [Header("Shake")]
        [SerializeField] float maxShakeOffset = 0.35f;
        [SerializeField] float maxShakeRoll = 1.5f;
        [SerializeField] float traumaDecay = 1.6f;

        public Camera Camera => cam;

        TankTurret turret;
        Vector3 velocity;
        float angle;
        float angleVelocity;
        float trauma;
        float zoomOffset;
        float targetZoomOffset;
        bool snapped;

        void Awake()
        {
            Instance = this;
            cam.orthographicSize = baseSize;
            turret = target.GetComponentInChildren<TankTurret>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Adds shake; distant events shake less.</summary>
        public void AddTrauma(float amount, Vector2? at = null)
        {
            if (at.HasValue)
                amount *= Mathf.Clamp01(1.2f - Vector2.Distance(at.Value, target.position) / 14f);
            trauma = Mathf.Clamp01(trauma + amount);
        }

        public void SetZoom(float extraSize) => targetZoomOffset = extraSize;

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // main menu: upright close-up showroom of the chosen tank
            bool menu = GameManager.Instance != null && GameManager.Instance.State == GameState.Menu;
            // follow the turret itself (traverse-limited), not the raw mouse: smooth, no motion sickness
            float wantedAngle = menu ? 0f : turret.transform.eulerAngles.z;
            float beam = VisionSystem.Instance != null ? VisionSystem.Instance.Focus : 0f;
            float ahead = menu ? 0f : Mathf.Lerp(aheadOffset, focusedAheadOffset, beam);

            angle = snapped ? Mathf.SmoothDampAngle(angle, wantedAngle, ref angleVelocity, rotationSmoothTime, Mathf.Infinity, dt)
                            : wantedAngle;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            Vector2 focus = (Vector2)target.position + (Vector2)transform.up * ahead;
            if (menu) focus -= (Vector2)transform.right * menuShift;
            Vector3 desired = new Vector3(focus.x, focus.y, transform.position.z);
            // first frame: jump straight to the tank (it has been placed at its random start by now)
            transform.position = snapped ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, dt)
                                         : desired;
            snapped = true;

            zoomOffset = Mathf.Lerp(zoomOffset, menu ? menuZoom : targetZoomOffset, dt * 3f);
            cam.orthographicSize = baseSize + zoomOffset;

            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            float shake = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            cam.transform.localPosition = new Vector3(
                (Mathf.PerlinNoise(t, 0.1f) * 2f - 1f) * maxShakeOffset * shake,
                (Mathf.PerlinNoise(0.2f, t) * 2f - 1f) * maxShakeOffset * shake, 0f);
            cam.transform.localRotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t, t) * 2f - 1f) * maxShakeRoll * shake);
        }
    }
}
