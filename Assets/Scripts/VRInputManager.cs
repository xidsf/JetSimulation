using UnityEngine;
using UnityEngine.InputSystem;

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

        // 감도 적용 및 정규화 (256 픽셀 기준으로 -1 ~ 1 범위)
        float targetPitch =  mouseDelta.y / 256f * mouseSensitivity;
        float targetRoll  =  mouseDelta.x / 256f * mouseSensitivity;

        // -1 ~ 1 클램프
        targetPitch = Mathf.Clamp(targetPitch, -1f, 1f);
        targetRoll  = Mathf.Clamp(targetRoll,  -1f, 1f);

        // 스무딩
        _rawPitch = Mathf.Lerp(_rawPitch, targetPitch, Time.deltaTime * inputSmoothing);
        _rawRoll  = Mathf.Lerp(_rawRoll,  targetRoll,  Time.deltaTime * inputSmoothing);

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
        // TODO: 파트 1 VR 구현 시 XR 컨트롤러 데이터 읽기
        // 예: rightHandDevice.deviceRotation → pitchInput/rollInput 변환
        PitchInput   = 0f;
        RollInput    = 0f;
        TriggerValue = 0f;
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
    private void OnGUI()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        GUILayout.BeginArea(new Rect(10, 10, 260, 120));
        GUI.Box(new Rect(0, 0, 260, 120), "");
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 13;

        GUILayout.Label($"[VRInputManager] Mode: {(useVR ? "VR" : "Mouse")}", style);
        GUILayout.Label($"PitchInput : {PitchInput:F3}", style);
        GUILayout.Label($"RollInput  : {RollInput:F3}", style);
        GUILayout.Label($"Trigger    : {TriggerValue:F3}", style);
        GUILayout.Label(IsCursorLocked ? "Cursor: LOCKED (ESC to unlock)" : "Cursor: FREE (Click to lock)", style);
        GUILayout.EndArea();
    }
}
