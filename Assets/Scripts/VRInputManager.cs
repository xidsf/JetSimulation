using System.Collections.Generic;
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

    [Tooltip("Use the first right controller pose detected in VR mode as neutral.")]
    public bool autoCalibrateRightControllerNeutral = true;

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
    private float _debugRollFromLocalZ;
    private float _debugRollFromLocalY;
    private float _debugPitchFromEulerX;
    private float _debugRollFromEulerY;
    private float _debugRollFromEulerZ;
    private string _debugInputSource = "None";
    private XRController _rightController;
    private UnityEngine.XR.InputDevice _rightHandXRDevice;
    private readonly List<UnityEngine.XR.InputDevice> _rightHandXRDevices = new List<UnityEngine.XR.InputDevice>();
    private Quaternion _rightControllerNeutralRotation = Quaternion.identity;
    private bool _hasRightControllerNeutral;

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

        if (!_hasRightControllerNeutral && autoCalibrateRightControllerNeutral)
        {
            SetRightControllerNeutral(controllerRotation);
        }

        GetControllerTiltInput(controllerRotation, out float targetPitch, out float targetRoll);

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
        if (_rightController != null && _rightController.added && IsRightHandController(_rightController))
        {
            return _rightController;
        }

        _rightController = null;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is XRController controller && IsRightHandController(controller))
            {
                _rightController = controller;
                return _rightController;
            }
        }

        return null;
    }

    private bool TryGetRightControllerInput(out Quaternion rotation, out float triggerValue)
    {
        if (TryGetRightControllerXRInput(out rotation, out triggerValue))
        {
            _debugInputSource = "Unity XR";
            return true;
        }

        if (TryGetRightControllerInputSystemInput(out rotation, out triggerValue))
        {
            _debugInputSource = "InputSystem";
            return true;
        }

        _debugInputSource = "None";
        rotation = Quaternion.identity;
        triggerValue = 0f;
        return false;
    }

    private bool TryGetRightControllerXRInput(out Quaternion rotation, out float triggerValue)
    {
        rotation = Quaternion.identity;
        triggerValue = 0f;

        if (!_rightHandXRDevice.isValid)
        {
            RefreshRightHandXRDevice();
        }

        if (!_rightHandXRDevice.isValid
            || !_rightHandXRDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation)
            || !IsValidRotation(rotation))
        {
            RefreshRightHandXRDevice();
        }

        if (!_rightHandXRDevice.isValid
            || !_rightHandXRDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation)
            || !IsValidRotation(rotation))
        {
            return false;
        }

        triggerValue = _rightHandXRDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigger)
            ? trigger
            : 0f;
        return true;
    }

    private void RefreshRightHandXRDevice()
    {
        _rightHandXRDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Right
            | UnityEngine.XR.InputDeviceCharacteristics.Controller,
            _rightHandXRDevices);

        _rightHandXRDevice = _rightHandXRDevices.Count > 0
            ? _rightHandXRDevices[0]
            : default;
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

    private void SetRightControllerNeutral(Quaternion rotation)
    {
        _rightControllerNeutralRotation = rotation;
        _hasRightControllerNeutral = true;
        _rawPitch = 0f;
        _rawRoll = 0f;
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
        float maxTiltSin = Mathf.Sin(maxTilt * Mathf.Deg2Rad);
        float idleTilt = Mathf.Clamp(vrIdleTiltAngle, 0f, maxTilt - 0.1f);
        float idleTiltNormalized = Mathf.Sin(idleTilt * Mathf.Deg2Rad) / maxTiltSin;
        Quaternion relativeRotation = Quaternion.Inverse(_rightControllerNeutralRotation) * rotation;

        Vector3 relativeForward = relativeRotation * Vector3.forward;
        Vector3 relativeRight = relativeRotation * Vector3.right;
        Vector3 relativeEuler = relativeRotation.eulerAngles;

        float pitchFromForward = Mathf.Clamp(relativeForward.y / maxTiltSin, -1f, 1f);
        _debugPitchFromEulerX = Mathf.Clamp(NormalizeAngle(relativeEuler.x) / maxTilt, -1f, 1f);
        float rawPitchInput = Mathf.Abs(_debugPitchFromEulerX) > Mathf.Abs(pitchFromForward)
            ? _debugPitchFromEulerX
            : pitchFromForward;
        pitchInput = ApplyTiltIdleZone(rawPitchInput, idleTiltNormalized);

        float rollFromLocalZ = -relativeRight.y / maxTiltSin;
        float rollFromLocalY = relativeForward.x / maxTiltSin;

        _debugRollFromLocalZ = Mathf.Clamp(rollFromLocalZ, -1f, 1f);
        _debugRollFromLocalY = Mathf.Clamp(rollFromLocalY, -1f, 1f);
        _debugRollFromEulerY = Mathf.Clamp(NormalizeAngle(relativeEuler.y) / maxTilt, -1f, 1f);
        _debugRollFromEulerZ = Mathf.Clamp(NormalizeAngle(relativeEuler.z) / maxTilt, -1f, 1f);

        float rawRollInput = PickLargestMagnitude(
            _debugRollFromLocalZ,
            _debugRollFromLocalY,
            _debugRollFromEulerY,
            _debugRollFromEulerZ);
        rollInput = ApplyTiltIdleZone(rawRollInput, idleTiltNormalized);
    }

    private float PickLargestMagnitude(params float[] values)
    {
        float result = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            if (Mathf.Abs(values[i]) > Mathf.Abs(result))
            {
                result = values[i];
            }
        }

        return result;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
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

        GUILayout.BeginArea(new Rect(10, 10, 300, 210));
        GUI.Box(new Rect(0, 0, 300, 210), "");
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 13;

        GUILayout.Label($"[VRInputManager] Mode: {(useVR ? "VR" : "Mouse")}", style);
        GUILayout.Label($"Input Src  : {_debugInputSource}", style);
        GUILayout.Label($"PitchInput : {PitchInput:F3}", style);
        GUILayout.Label($"RollInput  : {RollInput:F3}", style);
        GUILayout.Label($"Roll Z/Y   : {_debugRollFromLocalZ:F3} / {_debugRollFromLocalY:F3}", style);
        GUILayout.Label($"Euler X/Y/Z: {_debugPitchFromEulerX:F3} / {_debugRollFromEulerY:F3} / {_debugRollFromEulerZ:F3}", style);
        GUILayout.Label($"Idle Tilt  : {vrIdleTiltAngle:F1} deg", style);
        GUILayout.Label($"Trigger    : {TriggerValue:F3}", style);
        GUILayout.Label(IsCursorLocked ? "Cursor: LOCKED (ESC to unlock)" : "Cursor: FREE (Click to lock)", style);
        GUILayout.EndArea();
    }
}
