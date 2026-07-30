using MidnightChaos.Combat;
using MidnightChaos.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ValidatedOwnerNetworkTransform))]
    [RequireComponent(typeof(NetworkHealth))]
    public sealed class DiagnosticNetworkPlayer : NetworkBehaviour
    {
        [Header("Diagnostic Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float rotationSpeed = 14f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;

        private CharacterController characterController;
        private NetworkHealth health;
        private Renderer cachedRenderer;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<NetworkHealth>();
            cachedRenderer = GetComponentInChildren<Renderer>();
            characterController.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            characterController.enabled = IsOwner;
            health.HealthChanged += HandleHealthChanged;
            RefreshBodyColor();

            if (!IsOwner)
            {
                return;
            }

            DiagnosticCameraFollow cameraFollow =
                FindFirstObjectByType<DiagnosticCameraFollow>();

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
            }
        }

        public override void OnNetworkDespawn()
        {
            health.HealthChanged -= HandleHealthChanged;
        }

        private void Update()
        {
            if (!IsSpawned ||
                !IsOwner ||
                !characterController.enabled ||
                health.IsDead)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            input.x = ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            input.y = ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 planarDirection = new Vector3(input.x, 0f, input.y);
            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (keyboard.spaceKey.wasPressedThisFrame && characterController.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = planarDirection * speed + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);

            if (planarDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(planarDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private void HandleHealthChanged(int previousHealth, int newHealth)
        {
            RefreshBodyColor();
        }

        private void RefreshBodyColor()
        {
            if (cachedRenderer == null)
            {
                return;
            }

            cachedRenderer.material.color = health.IsDead
                ? new Color(0.75f, 0.12f, 0.12f)
                : IsOwner
                    ? new Color(0.15f, 0.85f, 0.95f)
                    : new Color(0.65f, 0.68f, 0.72f);
        }
    }
}
