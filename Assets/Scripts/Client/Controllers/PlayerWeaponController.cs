using Shared;
using Shared.Data;
using Shared.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Controllers
{
    public class PlayerWeaponController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ParticleSystem muzzleFlash;

        private PlayerRuntime playerRuntime;

        private void Start()
        {
            PlayerRuntime.LocalPlayerSpawned += OnLocalPlayerSpawned;
            PlayerRuntime.LocalPlayerDespawned += OnLocalPlayerDespawned;
            GameEvents.WeaponFired += OnWeaponFired;

            if (PlayerRuntime.LocalPlayer != null)
                playerRuntime = PlayerRuntime.LocalPlayer;
        }

        private void OnDestroy()
        {
            PlayerRuntime.LocalPlayerSpawned -= OnLocalPlayerSpawned;
            PlayerRuntime.LocalPlayerDespawned -= OnLocalPlayerDespawned;
            GameEvents.WeaponFired -= OnWeaponFired;
        }

        private void Update()
        {
        }

        private void OnLocalPlayerSpawned(PlayerRuntime player)
        {
            playerRuntime = player;
        }

        private void OnLocalPlayerDespawned(PlayerRuntime player)
        {
            if (playerRuntime == player)
                playerRuntime = null;
        }

        public void OnFire(InputAction.CallbackContext context)
        {
            if (playerRuntime == null || !context.performed)
                return;

            if (PhaseManager.Instance == null || PhaseManager.Instance.CurrentPhase != GamePhase.Combat)
                return;

            if (mainCamera == null)
                return;

            var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out var distance))
                return;

            var targetPos = ray.GetPoint(distance);
            playerRuntime.FireWeaponServerRpc(targetPos);
        }

        public void OnSwitchWeapon(InputAction.CallbackContext context)
        {
            if (playerRuntime == null || !context.performed)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (var i = 0; i < 3; i++)
            {
                var key = i switch
                {
                    0 => Key.Digit1,
                    1 => Key.Digit2,
                    _ => Key.Digit3
                };

                if (keyboard[key].wasPressedThisFrame)
                {
                    playerRuntime.SwitchWeaponServerRpc(i);
                    break;
                }
            }
        }

        private void OnWeaponFired(PlayerRuntime player, Vector3 target, string weaponId)
        {
            if (player != playerRuntime)
                return;

            PlayMuzzleFlash();
        }

        private void PlayMuzzleFlash()
        {
            if (muzzleFlash == null)
                return;

            muzzleFlash.transform.position = playerRuntime.transform.position + Vector3.up * 1.5f;
            muzzleFlash.transform.rotation = playerRuntime.transform.rotation;
            muzzleFlash.Play();
        }
    }
}
