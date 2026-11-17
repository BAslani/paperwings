using UnityEngine;

namespace PaperWings.CameraRig
{
    /// <summary>
    /// TEMPORARY chase camera for Stage 1 testing. Stage 4 replaces it with Cinemachine.
    /// Follows behind the plane's heading (ignoring bank so the horizon stays steady)
    /// and widens the field of view with speed.
    ///
    /// UNITY NOTE: Cameras usually update in LateUpdate, which runs after all Update calls
    /// and after physics interpolation, so the target has finished moving for this frame.
    /// </summary>
    public class SimpleChaseCamera : MonoBehaviour
    {
        [SerializeField] Rigidbody target;
        [SerializeField] Vector3 offset = new Vector3(0f, 2.2f, -7f);
        [SerializeField] float followSharpness = 6f;
        [SerializeField] float lookAhead = 6f;

        [Header("Speed FOV")]
        [SerializeField] float baseFov = 60f;
        [SerializeField] float maxFov = 80f;
        [SerializeField] float fovSpeedMin = 14f;
        [SerializeField] float fovSpeedMax = 40f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null) return;
            Transform t = target.transform;
            float dt = Time.deltaTime;

            // Heading only (flatten the forward vector) so banking doesn't swing the camera.
            Vector3 flatForward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = transform.forward;
            Quaternion heading = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

            Vector3 desiredPos = t.position + heading * offset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSharpness * dt));
            transform.LookAt(t.position + t.forward * lookAhead, Vector3.up);

            if (cam != null)
            {
                float speed = target.isKinematic ? 0f : target.linearVelocity.magnitude;
                float targetFov = Mathf.Lerp(baseFov, maxFov, Mathf.InverseLerp(fovSpeedMin, fovSpeedMax, speed));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-3f * dt));
            }
        }
    }
}
