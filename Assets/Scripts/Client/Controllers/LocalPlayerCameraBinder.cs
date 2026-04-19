using Unity.Cinemachine;
using UnityEngine;

namespace Client.Controllers
{
    public class LocalPlayerCameraBinder : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera mainCamera;
        [SerializeField] private LocalPlayerEntityResolver playerResolver;

        private Transform currentTarget;

        private void Awake()
        {
            if (playerResolver == null)
                playerResolver = GetComponent<LocalPlayerEntityResolver>();
        }

        private void Update()
        {
            var player = playerResolver != null ? playerResolver.CurrentPlayer : null;
            var target = player != null ? player.transform : null;
            if (target == currentTarget || mainCamera == null)
                return;

            currentTarget = target;

            var cameraTarget = mainCamera.Target;
            cameraTarget.TrackingTarget = currentTarget;
            mainCamera.Target = cameraTarget;
        }

        public bool HasRequiredReferences(out string issue)
        {
            if (mainCamera == null)
            {
                issue = "mainCamera is not assigned.";
                return false;
            }

            if (playerResolver == null && GetComponent<LocalPlayerEntityResolver>() == null)
            {
                issue = "playerResolver is missing and no LocalPlayerEntityResolver exists on this GameObject.";
                return false;
            }

            issue = null;
            return true;
        }
    }
}
