using MidnightChaos.Combat;
using MidnightChaos.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Player
{
    [DefaultExecutionOrder(-100)]
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
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;

        private CharacterController characterController;
        private NetworkHealth health;
        private DiagnosticCameraFollow localCamera;
        private Transform cameraAnchor;
        private float verticalVelocity;

        public float PlanarSpeed { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsGrounded =>
            characterController != null && characterController.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public bool IsAlive => health != null && !health.IsDead;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<NetworkHealth>();
            cameraAnchor = transform.Find("CameraAnchor");
            characterController.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            characterController.enabled = IsOwner;

            if (!IsOwner)
            {
                return;
            }

            localCamera =
                FindFirstObjectByType<DiagnosticCameraFollow>();

            if (localCamera == null)
            {
                Debug.LogError(
                    "[Gate G] Local first-person camera is missing.");
                return;
            }

            localCamera.SetTarget(transform, cameraAnchor);
        }

        public override void OnNetworkDespawn()
        {
            if (localCamera != null)
            {
                localCamera.ClearTarget(transform);
                localCamera = null;
            }

            PlanarSpeed = 0f;
            IsSprinting = false;
        }

        private void Update()
        {
            if (!IsSpawned ||
                !IsOwner ||
                !characterController.enabled ||
                health.IsDead)
            {
                PlanarSpeed = 0f;
                IsSprinting = false;
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

            Vector3 planarDirection =
                transform.right * input.x + transform.forward * input.y;
            planarDirection = Vector3.ProjectOnPlane(
                planarDirection,
                Vector3.up);

            if (planarDirection.sqrMagnitude > 1f)
            {
                planarDirection.Normalize();
            }

            IsSprinting =
                input.sqrMagnitude > 0.0001f &&
                keyboard.leftShiftKey.isPressed;
            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            PlanarSpeed = planarDirection.magnitude * speed;

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
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

    }
}
