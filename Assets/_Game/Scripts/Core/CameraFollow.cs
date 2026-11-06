using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Fixed-angle top-down camera that follows the player, with a small screen shake.
    /// On narrow (portrait) screens it pulls back so at least <see cref="minVisibleWidth"/> meters
    /// of ground stay visible left-to-right. It also tracks which patch of ground is on screen,
    /// so enemies can spawn just outside it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        public Transform target;
        [Tooltip("Viewing angle, and the closest the camera may get.")]
        public Vector3 offset = new(0f, 15f, -8.5f);
        [Tooltip("Meters of ground always visible across the screen at the player's position.")]
        public float minVisibleWidth = 11f;
        public float smoothing = 10f;
        public float maxShake = 0.6f;

        [Header("Fog, relative to the camera distance")]
        public float fogStartPadding = 6f;
        public float fogEndPadding = 30f;

        /// <summary>World X/Z bounds of the ground currently on screen (Rect.y is world Z).</summary>
        public Rect GroundRect { get; private set; }

        Camera cam;
        Vector3 currentOffset;
        Vector3 basePosition;
        float shake;
        int framedWidth, framedHeight;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            UpdateFraming();
        }

        void Start()
        {
            if (target) basePosition = target.position + currentOffset;
            transform.SetPositionAndRotation(basePosition, Quaternion.LookRotation(-currentOffset));
            UpdateGroundRect();
        }

        public void Shake(float amount) => shake = Mathf.Min(maxShake, shake + amount);

        /// <summary>True if the point is on screen, give or take <paramref name="margin"/> meters.</summary>
        public bool IsInView(Vector3 p, float margin)
        {
            var r = GroundRect;
            return p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.z >= r.yMin - margin && p.z <= r.yMax + margin;
        }

        void LateUpdate()
        {
            if (Screen.width != framedWidth || Screen.height != framedHeight) UpdateFraming();
            if (!target) return;

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            basePosition = Vector3.Lerp(basePosition, target.position + currentOffset, t);

            Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero;
            shake = Mathf.MoveTowards(shake, 0f, 3f * Time.deltaTime);

            transform.SetPositionAndRotation(basePosition + jitter, Quaternion.LookRotation(-currentOffset));
            UpdateGroundRect();
        }

        /// <summary>Picks the camera distance for the current screen shape.</summary>
        void UpdateFraming()
        {
            framedWidth = Screen.width;
            framedHeight = Screen.height;

            float tanHalfHorizontal = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float neededDistance = minVisibleWidth * 0.5f / tanHalfHorizontal;
            float distance = Mathf.Max(offset.magnitude, neededDistance);
            currentOffset = offset.normalized * distance;

            RenderSettings.fogStartDistance = distance + fogStartPadding;
            RenderSettings.fogEndDistance = distance + fogEndPadding;
            cam.farClipPlane = distance + fogEndPadding + 10f;
        }

        void UpdateGroundRect()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(i % 2, i / 2, 0f));
                if (ray.direction.y >= -0.01f) continue; // looking at the sky: ignore
                Vector3 p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            if (minX <= maxX) GroundRect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }
    }
}
