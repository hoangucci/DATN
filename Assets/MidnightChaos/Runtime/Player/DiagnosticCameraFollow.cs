using UnityEngine;

namespace MidnightChaos.Player
{
    [DisallowMultipleComponent]
    public sealed class DiagnosticCameraFollow : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -9f);
        [SerializeField, Min(0f)] private float positionSharpness = 12f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);

        private Transform target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            float blend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.LookAt(target.position + lookOffset);
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset);
        }
    }
}
