using Unity.Netcode;
using UnityEngine;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float pitchMin = -20f;
    [SerializeField] private float pitchMax = 60f;
    [SerializeField] private float positionSmoothSpeed = 12f;

    // Used by PlayerMovement to align movement to camera facing direction
    public Vector3 PlanarForward { get; private set; } = Vector3.forward;
    public Vector3 PlanarRight   { get; private set; } = Vector3.right;

    private Transform _cam;
    private float _yaw;
    private float _pitch = 15f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (Camera.main != null)
            Camera.main.gameObject.SetActive(false);

        GameObject camObj = new GameObject($"PlayerCamera_{OwnerClientId}");
        camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        _cam = camObj.transform;

        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (_cam == null) return;

        _yaw   += Input.GetAxis("Mouse X") * mouseSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // Flat (XZ) vectors for PlayerMovement to use
        Quaternion flatYaw = Quaternion.Euler(0f, _yaw, 0f);
        PlanarForward = flatYaw * Vector3.forward;
        PlanarRight   = flatYaw * Vector3.right;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;

        Quaternion rotation  = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    targetPos = transform.position + rotation * new Vector3(0f, 0f, -distance) + Vector3.up * height;

        _cam.position = Vector3.Lerp(_cam.position, targetPos, positionSmoothSpeed * Time.deltaTime);
        _cam.LookAt(transform.position + Vector3.up * 1.5f);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
