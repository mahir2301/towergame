using Shared;
using Shared.Runtime;
using Shared.Utilities;
using Unity.Cinemachine;
using UnityEngine;

namespace Client.Controllers
{
    public class LocalPlayerCameraBinder : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera mainCamera;

        private void OnEnable()
        {
            ClientEvents.LocalPlayerChanged += OnLocalPlayerChanged;
            ApplyTarget(ClientEvents.CurrentLocalPlayer);
        }

        private void OnDisable()
        {
            ClientEvents.LocalPlayerChanged -= OnLocalPlayerChanged;
        }

        private void OnLocalPlayerChanged(PlayerRuntime player)
        {
            ApplyTarget(player);
        }

        private void ApplyTarget(PlayerRuntime player)
        {
            if (mainCamera == null)
                return;

            var cameraTarget = mainCamera.Target;
            cameraTarget.TrackingTarget = player != null ? player.transform : null;
            mainCamera.Target = cameraTarget;
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (mainCamera, nameof(mainCamera)));
        }
    }
}
