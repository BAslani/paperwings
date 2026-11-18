using UnityEngine;
using Unity.Cinemachine;
using PaperWings.Flight;

namespace PaperWings.CameraRig
{
    /// <summary>
    /// In the Mouse Aim control scheme the camera should look where you're AIMING, not where the
    /// nose points (the plane then visibly swings around to catch up, like War Thunder's mouse aim).
    ///
    /// It does this by feeding the Cinemachine camera two invisible helper points instead of the plane:
    ///  - Follow:  sits on the plane but faces the aim heading, so the camera stays behind your aim.
    ///  - LookAt:  a point out along the aim direction, so the view centres on your aim.
    /// In the Classic scheme (or when not flying) it hands both back to the plane itself.
    ///
    /// UNITY NOTE: [DefaultExecutionOrder] makes this LateUpdate run before Cinemachine's, so the
    /// helper points are already in place when the camera is positioned this frame.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CinemachineCamera))]
    public class MouseAimCamera : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] PlaneInput input;

        CinemachineCamera vcam;
        Transform followAnchor, lookPoint;

        void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            // Hidden helper objects (not saved with the scene).
            followAnchor = new GameObject("MouseAim_Follow") { hideFlags = HideFlags.HideAndDontSave }.transform;
            lookPoint = new GameObject("MouseAim_Look") { hideFlags = HideFlags.HideAndDontSave }.transform;
        }

        void OnDestroy()
        {
            if (followAnchor != null) Destroy(followAnchor.gameObject);
            if (lookPoint != null) Destroy(lookPoint.gameObject);
        }

        void LateUpdate()
        {
            if (plane == null || input == null) return;

            bool mouseAim = input.IsMouseAim && plane.State == PlaneState.Flying;
            if (!mouseAim)
            {
                SetTargets(plane.transform, plane.transform);
                return;
            }

            Vector3 p = plane.transform.position;
            followAnchor.SetPositionAndRotation(p, Quaternion.Euler(0f, input.AimYaw, 0f));
            float look = input.Controls != null ? input.Controls.cameraLookDistance : 40f;
            lookPoint.position = p + input.AimDirection * look;
            SetTargets(followAnchor, lookPoint);
        }

        void SetTargets(Transform follow, Transform lookAt)
        {
            if (vcam.Follow != follow) vcam.Follow = follow;
            if (vcam.LookAt != lookAt) vcam.LookAt = lookAt;
        }
    }
}
