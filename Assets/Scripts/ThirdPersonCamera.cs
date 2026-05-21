using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private Camera _ownCamera;
    private float _yaw;
    private float _pitch = 15f;
    private bool _sceneLoadSubscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        CreateRuntimeCamera();
        DisableForeignMainCameras();

        SceneManager.sceneLoaded += OnUnitySceneLoaded;
        _sceneLoadSubscribed = true;

        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CreateRuntimeCamera()
    {
        GameObject camObj = new GameObject($"PlayerCamera_{OwnerClientId}");

        // Parent the runtime camera to the player so it survives NGO scene
        // migration during LoadSceneMode.Single transitions (LobbyScene ->
        // GameScene). When the player NetworkObject is moved to the new
        // scene, the parented camera follows; when the player despawns, the
        // camera is destroyed automatically.
        camObj.transform.SetParent(transform, worldPositionStays: false);

        _ownCamera = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        _cam = camObj.transform;

        // Place camera correctly on the first frame to avoid a one-frame snap.
        Quaternion initialRot = Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f);
        _cam.position = transform.position + initialRot * new Vector3(0f, 0f, -distance) + Vector3.up * height;
        _cam.LookAt(transform.position + Vector3.up * 1.5f);
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // After the lobby->game transition, GameScene's own MainCamera becomes
        // active. Disable any MainCamera that isn't ours so this player sees
        // through their own runtime camera.
        DisableForeignMainCameras();

        // LobbyUI intentionally unlocks the cursor when the local client
        // connects (so the Ready button is clickable). After the transition
        // to GameScene there is no more UI to interact with, so re-lock the
        // cursor for gameplay. Restricted to GameScene by name so we don't
        // accidentally lock the cursor in some future menu scene.
        if (scene.name == "GameScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void DisableForeignMainCameras()
    {
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null) continue;
            if (cam == _ownCamera) continue;
            if (cam.CompareTag("MainCamera"))
            {
                cam.gameObject.SetActive(false);
            }
        }
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

    public override void OnNetworkDespawn()
    {
        if (_sceneLoadSubscribed)
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            _sceneLoadSubscribed = false;
        }

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (_sceneLoadSubscribed)
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            _sceneLoadSubscribed = false;
        }

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
