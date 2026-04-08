using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;

    private ThirdPersonCamera thirdPersonCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        thirdPersonCamera = GetComponent<ThirdPersonCamera>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        // Move relative to where the camera is facing
        Vector3 camForward = thirdPersonCamera != null ? thirdPersonCamera.PlanarForward : Vector3.forward;
        Vector3 camRight   = thirdPersonCamera != null ? thirdPersonCamera.PlanarRight   : Vector3.right;

        Vector3 direction = (camForward * vertical + camRight * horizontal).normalized;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position += direction * moveSpeed * Time.deltaTime;
    }
}
