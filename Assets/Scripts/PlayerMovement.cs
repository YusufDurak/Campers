using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    private ThirdPersonCamera thirdPersonCamera;
    private CharacterController _controller;
    private float _verticalVelocity;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        thirdPersonCamera = GetComponent<ThirdPersonCamera>();
        _controller = GetComponent<CharacterController>();

        if (_controller == null)
        {
            Debug.LogError("[PlayerMovement] CharacterController not found on Player. Add a CharacterController component (Height ~2, Radius ~0.5, Center y ~1).");
            enabled = false;
        }
    }

    private void Update()
    {
        if (_controller == null) return;

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

        // Gravity. While grounded we keep a small negative value (-2 instead of 0)
        // so CharacterController.isGrounded stays true on slopes and doesn't jitter.
        if (_controller.isGrounded)
        {
            _verticalVelocity = -2f;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = direction * moveSpeed + Vector3.up * _verticalVelocity;
        _controller.Move(motion * Time.deltaTime);
    }
}
