using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Player
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DiagnosticCameraFollow : MonoBehaviour
    {
        [Header("Gate G - Local First Person Camera")]
        [SerializeField] private Vector3 eyeOffset =
            new Vector3(0f, 0.75f, 0.08f);
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity =
            0.12f;
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;

        private Transform rotationTarget;
        private Transform positionAnchor;
        private float yaw;
        private float pitch;
        private bool ownsCursorLock;
        private bool skipNextLookFrame;
        private float hitShakeStartedAt;
        private float hitShakeDuration;
        private float hitShakePositionAmplitude;
        private float hitShakeRotationAmplitude;
        private float hitShakeFrequency;
        private int hitShakeGeneration;

        public bool IsCursorCaptured =>
            rotationTarget != null && Cursor.lockState == CursorLockMode.Locked;

        public void SetTarget(Transform newTarget)
        {
            SetTarget(newTarget, null);
        }

        public void SetTarget(Transform newRotationTarget, Transform newPositionAnchor)
        {
            if (newRotationTarget == null)
            {
                return;
            }

            rotationTarget = newRotationTarget;
            positionAnchor = newPositionAnchor;
            yaw = rotationTarget.eulerAngles.y;
            pitch = 0f;
            CaptureCursor();
            SnapToTarget();
        }

        public void ClearTarget(Transform expectedTarget)
        {
            if (rotationTarget != expectedTarget)
            {
                return;
            }

            rotationTarget = null;
            positionAnchor = null;
            hitShakeDuration = 0f;
            ReleaseCursor();
        }

        public void PlayConfirmedHitShake(
            float duration,
            float positionAmplitude,
            float rotationAmplitude,
            float frequency)
        {
            hitShakeDuration = Mathf.Max(0.01f, duration);
            hitShakePositionAmplitude = Mathf.Max(0f, positionAmplitude);
            hitShakeRotationAmplitude = Mathf.Max(0f, rotationAmplitude);
            hitShakeFrequency = Mathf.Max(0.01f, frequency);
            hitShakeStartedAt = Time.unscaledTime;
            hitShakeGeneration++;
        }

        private void Update()
        {
            if (rotationTarget == null)
            {
                ReleaseOwnedCursorIfNecessary();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            if (!IsCursorCaptured || Mouse.current == null)
            {
                return;
            }

            // Locking can warp the hardware cursor to the Game view center.
            // Ignore that synthetic delta so recapturing does not snap view.
            if (skipNextLookFrame)
            {
                skipNextLookFrame = false;
                return;
            }

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw = Mathf.Repeat(
                yaw + mouseDelta.x * mouseSensitivity,
                360f);
            pitch = Mathf.Clamp(
                pitch - mouseDelta.y * mouseSensitivity,
                minimumPitch,
                maximumPitch);

            // Only yaw belongs to the networked Player. Pitch remains local
            // camera presentation and cannot tilt the CharacterController.
            rotationTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (rotationTarget == null)
            {
                ReleaseOwnedCursorIfNecessary();
                return;
            }

            // Right mouse is unused by combat, so it can recapture the cursor
            // without stealing a diagnostic UI click or causing an attack.
            if (!IsCursorCaptured &&
                Mouse.current != null &&
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                CaptureCursor();
            }

            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (rotationTarget == null)
            {
                return;
            }

            Vector3 basePosition = positionAnchor != null
                ? positionAnchor.position
                : rotationTarget.TransformPoint(eyeOffset);
            Quaternion baseRotation =
                rotationTarget.rotation * Quaternion.Euler(pitch, 0f, 0f);

            transform.SetPositionAndRotation(basePosition, baseRotation);
            ApplyConfirmedHitShake(baseRotation);
        }

        private void ApplyConfirmedHitShake(Quaternion baseRotation)
        {
            if (hitShakeDuration <= 0f ||
                (hitShakePositionAmplitude <= 0f &&
                 hitShakeRotationAmplitude <= 0f))
            {
                return;
            }

            float elapsed = Time.unscaledTime - hitShakeStartedAt;
            if (elapsed >= hitShakeDuration)
            {
                hitShakeDuration = 0f;
                return;
            }

            float normalizedTime = Mathf.Clamp01(elapsed / hitShakeDuration);
            float remaining = 1f - normalizedTime;
            float envelope = remaining * remaining;
            float phase =
                elapsed * hitShakeFrequency * Mathf.PI * 2f +
                hitShakeGeneration * 1.618034f;
            float noiseX = Mathf.Sin(phase);
            float noiseY = Mathf.Sin(phase * 1.37f + 1.1f);
            float noiseZ = Mathf.Sin(phase * 1.83f + 2.4f);
            float impactKick = Mathf.Sin(normalizedTime * Mathf.PI);

            Vector3 localPositionOffset = new Vector3(
                noiseX,
                noiseY,
                -impactKick * 0.35f) *
                (hitShakePositionAmplitude * envelope);
            Vector3 localEulerOffset = new Vector3(
                -impactKick + noiseY * 0.25f,
                noiseX * 0.25f,
                noiseZ * 0.2f) *
                (hitShakeRotationAmplitude * envelope);

            transform.position += baseRotation * localPositionOffset;
            transform.rotation *= Quaternion.Euler(localEulerOffset);
        }

        private void CaptureCursor()
        {
            if (!Application.isFocused)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ownsCursorLock = true;
            skipNextLookFrame = true;
        }

        private void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownsCursorLock = false;
        }

        private void ReleaseOwnedCursorIfNecessary()
        {
            if (ownsCursorLock)
            {
                ReleaseCursor();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ReleaseCursor();
            }
        }

        private void OnDisable()
        {
            hitShakeDuration = 0f;
            ReleaseOwnedCursorIfNecessary();
        }
    }
}
