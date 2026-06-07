using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

/// <summary>
/// 입력 추상화 레이어.
/// useVR = false : 마우스 움직임 → pitchInput/rollInput 변환 (현재 모드)
/// useVR = true  : Vive 컨트롤러 기울기 → pitchInput/rollInput 변환 (추후 구현)
/// 
/// 조종 방식 (마우스 모드):
///   - 게임 시작 시 커서 잠금 (Locked)
///   - 마우스 Y 위로 → 기수 올림 (pitchInput 양수)
///   - 마우스 X 우측 → 우측 롤 (rollInput 양수)
///   - ESC : 커서 잠금 해제 / 다시 클릭 : 잠금 복귀
/// </summary>
public class VRInputManager : MonoBehaviour
{
    private enum InputRotationAxis
    {
        X,
        Y,
        Z
    }

    // ──────────────────────────────────────────────
    //  인스턴스 (싱글톤)
    // ──────────────────────────────────────────────
    public static VRInputManager Instance { get; private set; }

    // ──────────────────────────────────────────────
    //  Inspector 설정
    // ──────────────────────────────────────────────
    [Header("=== 모드 ===")]
    [Tooltip("false = 마우스 모드 (PC 테스트) / true = VR 모드 (Vive)")]
    public bool useVR = false;

    [Header("=== 마우스 설정 ===")]
    [Tooltip("마우스 민감도 배율")]
    public float mouseSensitivity = 2.5f;

    [Tooltip("입력값 스무딩 속도 (높을수록 즉각 반응)")]
    public float inputSmoothing = 6f;

    [Tooltip("이 값 이하의 입력은 무시 (데드존)")]
    [Range(0f, 0.3f)]
    public float inputDeadzone = 0.05f;

    [Header("=== VR Controller Tilt ===")]
    [Tooltip("Right controller tilt angle that becomes full pitch/roll input.")]
    [Range(5f, 90f)]
    public float vrMaxTiltAngle = 35f;

    [Tooltip("Controller tilt under this angle is treated as idle input.")]
    [Range(0f, 30f)]
    public float vrIdleTiltAngle = 10f;

    [Tooltip("VR pitch input under this absolute value is ignored.")]
    [Range(0f, 0.95f)]
    public float vrPitchInputThreshold = 0.5f;

    [Tooltip("VR roll input under this absolute value is ignored.")]
    [Range(0f, 0.95f)]
    public float vrRollInputThreshold = 0.5f;

    [Tooltip("Scales aircraft pitch/roll rotation speed in VR mode.")]
    [Range(0.1f, 1f)]
    public float vrRotationSpeedScale = 0.45f;

    [Tooltip("Use the first right controller pose detected in VR mode as neutral.")]
    public bool autoCalibrateRightControllerNeutral = true;

    [Tooltip("Allow Unity XR Interaction Toolkit simulated controllers for editor testing.")]
    public bool allowSimulatedXRControllers = true;

    [Header("=== XRI Input Actions ===")]
    [Tooltip("XRI right controller Rotation action. If assigned, this is used before raw XRController deviceRotation.")]
    [SerializeField] private InputActionReference vrControllerRotationAction;

    [Tooltip("XRI right controller Trigger/Activate Value action. If assigned, this is used before raw XRController trigger.")]
    [SerializeField] private InputActionReference vrControllerTriggerAction;

    [Header("=== VR Input System Axis Mapping ===")]
    [Tooltip("Input System deviceRotation Euler axis used for pitch tilt.")]
    [SerializeField] private InputRotationAxis vrPitchInputAxis = InputRotationAxis.X;

    [Tooltip("Input System deviceRotation Euler axis used for left/right controller tilt.")]
    [SerializeField] private InputRotationAxis vrRollInputAxis = InputRotationAxis.Y;

    [Tooltip("Invert pitch input read from Input System deviceRotation.")]
    [SerializeField] private bool invertVRPitchInput = true;

    [Tooltip("Invert roll input read from Input System deviceRotation.")]
    [SerializeField] private bool invertVRRollInput = false;

    // ──────────────────────────────────────────────
    //  공개 읽기 속성 (다른 스크립트가 이 값을 소비)
    // ──────────────────────────────────────────────

    /// <summary>피치 입력 (-1 하강 ~ +1 상승)</summary>
    public float PitchInput { get; private set; }

    /// <summary>롤 입력 (-1 좌측 롤 ~ +1 우측 롤)</summary>
    public float RollInput { get; private set; }

    /// <summary>왼손 트리거 (0~1) - 파트 3 기관총에서 사용 예정</summary>
    public float TriggerValue { get; private set; }

    /// <summary>커서가 잠겨있는지 여부</summary>
    public bool IsCursorLocked { get; private set; }

    // ──────────────────────────────────────────────
    //  내부 변수
    // ──────────────────────────────────────────────
    private float _rawPitch;
    private float _rawRoll;
    private Vector3 _debugInputEulerDelta;
    private float _debugInputPitchAngle;
    private float _debugInputRollAngle;
    private float _debugHMDYawDelta;
    private string _debugInputSource = "None";
    private string _debugInputDevice = "None";
    private XRController _rightController;
    private Vector3 _rightControllerNeutralEuler;
    private float _rightControllerNeutralHMDYaw;
    private bool _hasRightControllerNeutral;
    private bool _usingSimulatedController;

    // ──────────────────────────────────────────────
    //  Unity 생명주기
    // ──────────────────────────────────────────────
    private void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (!useVR)
        {
            LockCursor();
        }
    }

    private void OnEnable()
    {
        vrControllerRotationAction?.action?.Enable();
        vrControllerTriggerAction?.action?.Enable();
    }

    private void OnDisable()
    {
        vrControllerRotationAction?.action?.Disable();
        vrControllerTriggerAction?.action?.Disable();
    }

    private void Update()
    {
        HandleCursorLock();

        if (useVR)
        {
            UpdateVRInput();
        }
        else
        {
            UpdateMouseInput();
        }
    }

    // ──────────────────────────────────────────────
    //  커서 잠금 처리
    // ──────────────────────────────────────────────
    private void HandleCursorLock()
    {
        if (useVR) return;

        // ESC → 커서 잠금 해제
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // 잠금 해제 상태에서 좌클릭 → 다시 잠금
        if (!IsCursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsCursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsCursorLocked = false;
    }

    // ──────────────────────────────────────────────
    //  마우스 모드 입력
    // ──────────────────────────────────────────────
    private void UpdateMouseInput()
    {
        if (!IsCursorLocked || Mouse.current == null)
        {
            // 커서 잠금 해제 상태 → 입력값을 0으로 감쇠
            _rawPitch = Mathf.Lerp(_rawPitch, 0f, Time.deltaTime * inputSmoothing);
            _rawRoll  = Mathf.Lerp(_rawRoll,  0f, Time.deltaTime * inputSmoothing);
            PitchInput = ApplyDeadzone(_rawPitch);
            RollInput  = ApplyDeadzone(_rawRoll);
            return;
        }

        // 마우스 델타 읽기 (새 Input System)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float deltaTime = Time.deltaTime;
        if (deltaTime > 0f)
        {
            // 프레임 레이트에 독립적인 초당 픽셀 이동 속도 계산
            float mouseVelocityY = mouseDelta.y / deltaTime;
            float mouseVelocityX = mouseDelta.x / deltaTime;

            // 초당 1000픽셀 이동을 기준(1.0)으로 감도 적용
            float targetPitch = mouseVelocityY / 1000f * mouseSensitivity;
            float targetRoll  = mouseVelocityX / 1000f * mouseSensitivity;

            // -1 ~ 1 클램프
            targetPitch = Mathf.Clamp(targetPitch, -1f, 1f);
            targetRoll  = Mathf.Clamp(targetRoll,  -1f, 1f);

            // 스무딩
            _rawPitch = Mathf.Lerp(_rawPitch, targetPitch, deltaTime * inputSmoothing);
            _rawRoll  = Mathf.Lerp(_rawRoll,  targetRoll,  deltaTime * inputSmoothing);
        }

        PitchInput = ApplyDeadzone(_rawPitch);
        RollInput  = ApplyDeadzone(_rawRoll);

        // 임시: 스페이스바 → 트리거 (파트 3 테스트용)
        TriggerValue = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ? 1f : 0f;
    }

    // ──────────────────────────────────────────────
    //  VR 모드 입력 (Vive - 추후 구현)
    // ──────────────────────────────────────────────
    private void UpdateVRInput()
    {
        if (!TryGetRightControllerInput(out Quaternion controllerRotation, out float triggerValue))
        {
            ResetInputToNeutral();
            return;
        }

        Quaternion inputRotation = GetTiltInputRotation(controllerRotation);

        if (!_hasRightControllerNeutral && autoCalibrateRightControllerNeutral)
        {
            SetRightControllerNeutral(inputRotation);
        }

        GetControllerTiltInput(inputRotation, out float targetPitch, out float targetRoll);

        float deltaTime = Time.deltaTime;
        _rawPitch = Mathf.Lerp(_rawPitch, targetPitch, deltaTime * inputSmoothing);
        _rawRoll = Mathf.Lerp(_rawRoll, targetRoll, deltaTime * inputSmoothing);

        PitchInput = ApplyDeadzone(_rawPitch);
        RollInput = ApplyDeadzone(_rawRoll);
        TriggerValue = triggerValue;
    }

    // ──────────────────────────────────────────────
    //  헬퍼
    // ──────────────────────────────────────────────
    private float ApplyDeadzone(float value)
    {
        if (Mathf.Abs(value) < inputDeadzone) return 0f;
        return value;
    }

    // ──────────────────────────────────────────────
    //  디버그 GUI (에디터/빌드에서 입력값 확인용)
    // ──────────────────────────────────────────────
    private XRController GetRightHandController()
    {
        if (_rightController != null
            && _rightController.added
            && IsRightHandController(_rightController)
            && (allowSimulatedXRControllers || !IsSimulatedXRDevice(_rightController)))
        {
            _usingSimulatedController = IsSimulatedXRDevice(_rightController);
            return _rightController;
        }

        _rightController = null;
        XRController fallbackController = null;
        XRController simulatedFallbackController = null;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is not XRController controller
                || !IsRightHandController(controller))
            {
                continue;
            }

            if (IsSimulatedXRDevice(controller))
            {
                if (allowSimulatedXRControllers)
                {
                    simulatedFallbackController ??= controller;
                }

                continue;
            }

            if (IsPreferredViveController(controller))
            {
                _rightController = controller;
                _usingSimulatedController = false;
                return _rightController;
            }

            fallbackController ??= controller;
        }

        _rightController = fallbackController ?? simulatedFallbackController;
        _usingSimulatedController = _rightController != null && IsSimulatedXRDevice(_rightController);
        return _rightController;
    }

    private bool TryGetRightControllerInput(out Quaternion rotation, out float triggerValue)
    {
        if (TryGetRightControllerInputActionInput(out rotation, out triggerValue))
        {
            _debugInputSource = "XRI Action";
            return true;
        }

        if (TryGetRightControllerInputSystemInput(out rotation, out triggerValue))
        {
            _debugInputSource = "InputSystem";
            return true;
        }

        _debugInputSource = "None";
        _debugInputDevice = "None";
        _usingSimulatedController = false;
        rotation = Quaternion.identity;
        triggerValue = 0f;
        return false;
    }

    private bool TryGetRightControllerInputActionInput(out Quaternion rotation, out float triggerValue)
    {
        InputAction rotationAction = vrControllerRotationAction != null ? vrControllerRotationAction.action : null;
        if (rotationAction == null)
        {
            rotation = Quaternion.identity;
            triggerValue = 0f;
            return false;
        }

        rotation = rotationAction.ReadValue<Quaternion>();
        if (!IsValidRotation(rotation))
        {
            triggerValue = 0f;
            return false;
        }

        _debugInputDevice = rotationAction.name;
        _usingSimulatedController = false;

        InputAction triggerAction = vrControllerTriggerAction != null ? vrControllerTriggerAction.action : null;
        triggerValue = triggerAction != null ? triggerAction.ReadValue<float>() : 0f;
        return true;
    }

    private bool TryGetRightControllerInputSystemInput(out Quaternion rotation, out float triggerValue)
    {
        XRController rightController = GetRightHandController();
        if (rightController == null || rightController.deviceRotation == null)
        {
            rotation = Quaternion.identity;
            triggerValue = 0f;
            return false;
        }

        rotation = rightController.deviceRotation.ReadValue();
        if (!IsValidRotation(rotation))
        {
            triggerValue = 0f;
            return false;
        }

        _debugInputDevice = GetDeviceDebugName(rightController);
        AxisControl trigger = rightController.TryGetChildControl<AxisControl>("trigger");
        triggerValue = trigger != null ? trigger.ReadValue() : 0f;
        return true;
    }

    private bool IsRightHandController(InputDevice device)
    {
        foreach (var usage in device.usages)
        {
            if (usage == CommonUsages.RightHand)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPreferredViveController(InputDevice device)
    {
        string layout = device.layout ?? string.Empty;
        string product = device.description.product ?? string.Empty;
        string manufacturer = device.description.manufacturer ?? string.Empty;

        return layout.Contains("Vive")
            || product.Contains("Vive")
            || manufacturer.Contains("HTC");
    }

    private bool IsSimulatedXRDevice(InputDevice device)
    {
        string typeName = device.GetType().Name;
        string layout = device.layout ?? string.Empty;
        string name = device.name ?? string.Empty;
        string displayName = device.displayName ?? string.Empty;
        string product = device.description.product ?? string.Empty;

        return typeName.Contains("Simulated")
            || layout.Contains("Simulated")
            || name.Contains("Simulated")
            || displayName.Contains("Simulated")
            || product.Contains("Simulated");
    }

    private string GetDeviceDebugName(InputDevice device)
    {
        return $"{device.displayName} / {device.layout} / {device.description.product}";
    }

    private void SetRightControllerNeutral(Quaternion rotation)
    {
        _rightControllerNeutralEuler = rotation.eulerAngles;
        _rightControllerNeutralHMDYaw = TryGetHMDYaw(out float hmdYaw) ? hmdYaw : 0f;
        _debugHMDYawDelta = 0f;
        _hasRightControllerNeutral = true;
        _rawPitch = 0f;
        _rawRoll = 0f;
    }

    private Quaternion GetTiltInputRotation(Quaternion controllerRotation)
    {
        if (!_usingSimulatedController || !TryGetHMDYaw(out float hmdYaw))
        {
            _debugHMDYawDelta = 0f;
            return controllerRotation;
        }

        _debugHMDYawDelta = _hasRightControllerNeutral
            ? Mathf.DeltaAngle(_rightControllerNeutralHMDYaw, hmdYaw)
            : 0f;

        return Quaternion.Euler(0f, -_debugHMDYawDelta, 0f) * controllerRotation;
    }

    private bool TryGetHMDYaw(out float yaw)
    {
        XRHMD fallbackHMD = null;
        XRHMD simulatedHMD = null;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is not XRHMD hmd)
            {
                continue;
            }

            if (IsSimulatedXRDevice(hmd))
            {
                simulatedHMD ??= hmd;
            }
            else
            {
                fallbackHMD ??= hmd;
            }
        }

        XRHMD selectedHMD = _usingSimulatedController ? simulatedHMD ?? fallbackHMD : fallbackHMD ?? simulatedHMD;
        if (selectedHMD == null)
        {
            yaw = 0f;
            return false;
        }

        Quaternion hmdRotation = Quaternion.identity;
        if (selectedHMD.centerEyeRotation != null)
        {
            hmdRotation = selectedHMD.centerEyeRotation.ReadValue();
        }

        if (!IsValidRotation(hmdRotation) && selectedHMD.deviceRotation != null)
        {
            hmdRotation = selectedHMD.deviceRotation.ReadValue();
        }

        if (!IsValidRotation(hmdRotation))
        {
            yaw = 0f;
            return false;
        }

        yaw = hmdRotation.eulerAngles.y;
        return true;
    }

    private void ResetInputToNeutral()
    {
        _rawPitch = Mathf.Lerp(_rawPitch, 0f, Time.deltaTime * inputSmoothing);
        _rawRoll = Mathf.Lerp(_rawRoll, 0f, Time.deltaTime * inputSmoothing);
        PitchInput = ApplyDeadzone(_rawPitch);
        RollInput = ApplyDeadzone(_rawRoll);
        TriggerValue = 0f;
    }

    private void GetControllerTiltInput(Quaternion rotation, out float pitchInput, out float rollInput)
    {
        float maxTilt = Mathf.Max(1f, vrMaxTiltAngle);
        float idleTilt = Mathf.Clamp(vrIdleTiltAngle, 0f, maxTilt - 0.1f);
        float idleTiltNormalized = idleTilt / maxTilt;
        Vector3 currentEuler = rotation.eulerAngles;

        _debugInputEulerDelta = GetEulerDelta(_rightControllerNeutralEuler, currentEuler);
        _debugInputPitchAngle = GetAxisValue(_debugInputEulerDelta, vrPitchInputAxis);
        _debugInputRollAngle = GetAxisValue(_debugInputEulerDelta, vrRollInputAxis);

        float pitchSign = invertVRPitchInput ? -1f : 1f;
        float rawPitchInput = Mathf.Clamp(_debugInputPitchAngle / maxTilt * pitchSign, -1f, 1f);
        pitchInput = ApplyInputThreshold(
            ApplyTiltIdleZone(rawPitchInput, idleTiltNormalized),
            vrPitchInputThreshold);

        float rollSign = invertVRRollInput ? -1f : 1f;
        float rawRollInput = Mathf.Clamp(_debugInputRollAngle / maxTilt * rollSign, -1f, 1f);
        rollInput = ApplyInputThreshold(
            ApplyTiltIdleZone(rawRollInput, idleTiltNormalized),
            vrRollInputThreshold);
    }

    private Vector3 GetEulerDelta(Vector3 neutralEuler, Vector3 currentEuler)
    {
        return new Vector3(
            Mathf.DeltaAngle(neutralEuler.x, currentEuler.x),
            Mathf.DeltaAngle(neutralEuler.y, currentEuler.y),
            Mathf.DeltaAngle(neutralEuler.z, currentEuler.z));
    }

    private float GetAxisValue(Vector3 value, InputRotationAxis axis)
    {
        switch (axis)
        {
            case InputRotationAxis.X:
                return value.x;
            case InputRotationAxis.Y:
                return value.y;
            case InputRotationAxis.Z:
                return value.z;
            default:
                return 0f;
        }
    }

    private float ApplyInputThreshold(float value, float threshold)
    {
        float clampedThreshold = Mathf.Clamp(threshold, 0f, 0.95f);
        float magnitude = Mathf.Abs(value);
        if (magnitude <= clampedThreshold)
        {
            return 0f;
        }

        float remappedMagnitude = Mathf.InverseLerp(clampedThreshold, 1f, magnitude);
        return Mathf.Sign(value) * remappedMagnitude;
    }

    private float ApplyTiltIdleZone(float value, float idleThreshold)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= idleThreshold)
        {
            return 0f;
        }

        float remappedMagnitude = Mathf.InverseLerp(idleThreshold, 1f, magnitude);
        return Mathf.Sign(value) * remappedMagnitude;
    }

    private bool IsValidRotation(Quaternion rotation)
    {
        return IsFinite(rotation.x)
            && IsFinite(rotation.y)
            && IsFinite(rotation.z)
            && IsFinite(rotation.w)
            && rotation != new Quaternion(0f, 0f, 0f, 0f);
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnGUI()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 250));
        GUI.Box(new Rect(0, 0, 300, 250), "");
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 13;

        GUILayout.Label($"[VRInputManager] Mode: {(useVR ? "VR" : "Mouse")}", style);
        GUILayout.Label($"Input Src  : {_debugInputSource}", style);
        GUILayout.Label($"Device     : {_debugInputDevice}", style);
        GUILayout.Label($"PitchInput : {PitchInput:F3}", style);
        GUILayout.Label($"RollInput  : {RollInput:F3}", style);
        GUILayout.Label($"Sim/HMDYaw : {_usingSimulatedController} / {_debugHMDYawDelta:F1}", style);
        GUILayout.Label($"Euler dXYZ : {_debugInputEulerDelta.x:F1} / {_debugInputEulerDelta.y:F1} / {_debugInputEulerDelta.z:F1}", style);
        GUILayout.Label($"Input Deg  : P {_debugInputPitchAngle:F1}({vrPitchInputAxis}) / R {_debugInputRollAngle:F1}({vrRollInputAxis})", style);
        GUILayout.Label($"Idle Tilt  : {vrIdleTiltAngle:F1} deg", style);
        GUILayout.Label($"Input Cut  : P {vrPitchInputThreshold:F2} / R {vrRollInputThreshold:F2}", style);
        GUILayout.Label($"VR Rot x   : {vrRotationSpeedScale:F2}", style);
        GUILayout.Label($"Trigger    : {TriggerValue:F3}", style);
        GUILayout.Label(IsCursorLocked ? "Cursor: LOCKED (ESC to unlock)" : "Cursor: FREE (Click to lock)", style);
        GUILayout.EndArea();
    }
}
