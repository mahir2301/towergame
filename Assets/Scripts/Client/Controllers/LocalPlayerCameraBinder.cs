using Shared.Runtime;
using Unity.Cinemachine;
using UnityEngine;

namespace Client.Controllers
{
    public class LocalPlayerCameraBinder : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera mainCamera;

        private Transform currentTarget;

        private void Update()
        {
            var target = PlayerRuntime.LocalPlayer != null ? PlayerRuntime.LocalPlayer.transform : null;
            if (target == currentTarget || mainCamera == null)
                return;

            currentTarget = target;

            var cameraTarget = mainCamera.Target;
            cameraTarget.TrackingTarget = currentTarget;
            mainCamera.Target = cameraTarget;
        }
    }
}
