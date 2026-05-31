using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class SimpleFirstPersonPlayer : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("Camera")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float eyeHeight = 1.7f;
        [SerializeField] private bool disableOtherCameras = true;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            EnsureController();
            EnsureCamera();
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            Look();
            Move();
        }

        private void EnsureController()
        {
            controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CharacterController>();
            }

            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
        }

        private void EnsureCamera()
        {
            if (playerCamera == null)
            {
                var cameraObject = new GameObject("First Person Camera");
                cameraObject.transform.SetParent(transform);
                cameraObject.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
                cameraObject.transform.localRotation = Quaternion.identity;
                playerCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            playerCamera.tag = "MainCamera";
            playerCamera.enabled = true;

            if (!disableOtherCameras)
            {
                return;
            }

            foreach (var camera in FindObjectsOfType<Camera>())
            {
                if (camera != playerCamera)
                {
                    camera.enabled = false;
                }
            }

            foreach (var listener in FindObjectsOfType<AudioListener>())
            {
                if (listener.gameObject != playerCamera.gameObject)
                {
                    listener.enabled = false;
                }
            }
        }

        private void Move()
        {
            var input = ReadMoveInput();
            var move = transform.right * input.x + transform.forward * input.y;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            var speed = moveSpeed * (IsSprintPressed() ? sprintMultiplier : 1f);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            var velocity = move * speed;
            velocity.y = verticalVelocity;

            controller.Move(velocity * Time.deltaTime);
        }

        private void Look()
        {
            var mouseDelta = ReadMouseDelta() * mouseSensitivity;
            transform.Rotate(Vector3.up, mouseDelta.x, Space.World);

            pitch = Mathf.Clamp(pitch - mouseDelta.y, minPitch, maxPitch);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private Vector2 ReadMoveInput()
        {
            var input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                return input;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
#endif

            return input;
        }

        private Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        private bool IsSprintPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftShiftKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }

        private static void HandleCursorToggle()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#endif
        }
    }
}
