using UnityEngine;

/// <summary>
/// 비행기 조종 핵심 로직.
/// VRInputManager에서 pitchInput / rollInput을 받아 비행기 Transform을 제어합니다.
/// 
/// 비행 모델:
///   - 항상 transform.forward 방향으로 자동 전진
///   - pitchInput > 0 : 기수 올림 (상승)
///   - rollInput  > 0 : 우측으로 기울어짐
///   - rollInput에 비례해서 약한 자동 Yaw 적용 (자연스러운 선회)
/// 
/// 이 스크립트는 PlayerJet 루트 오브젝트에 붙입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerJetController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector 설정
    // ──────────────────────────────────────────────
    [Header("=== 참조 ===")]
    [Tooltip("JetMovementSettings ScriptableObject")]
    public JetMovementSettings settings;

    // ──────────────────────────────────────────────
    //  내부 변수
    // ──────────────────────────────────────────────
    private Rigidbody _rb;
    private VRInputManager _input;

    // ──────────────────────────────────────────────
    //  Unity 생명주기
    // ──────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Rigidbody 설정 - 물리 중력 끄고 직접 제어
        _rb.useGravity = false;
        _rb.drag = 0f;
        _rb.angularDrag = 2f;
        _rb.isKinematic = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.None;
    }

    private void Start()
    {
        _input = VRInputManager.Instance;
        if (_input == null)
        {
            Debug.LogError("[PlayerJetController] VRInputManager를 찾을 수 없습니다! 씬에 VRInputManager 오브젝트를 추가하세요.");
        }

        if (settings == null)
        {
            Debug.LogError("[PlayerJetController] JetMovementSettings가 할당되지 않았습니다!");
        }
    }

    private void FixedUpdate()
    {
        // _input이 null이면 재시도 (동적 생성 대응)
        if (_input == null)
        {
            _input = VRInputManager.Instance;
        }

        if (_input == null || settings == null) return;

        ApplyForwardThrust();
        ApplyRotation();
    }

    // ──────────────────────────────────────────────
    //  자동 전진
    // ──────────────────────────────────────────────
    private void ApplyForwardThrust()
    {
        // 항상 기수 방향으로 전진 (속도 직접 설정)
        _rb.velocity = transform.forward * settings.forwardSpeed;
    }

    // ──────────────────────────────────────────────
    //  회전 처리 (Pitch / Roll / 자동 Yaw)
    // ──────────────────────────────────────────────
    private void ApplyRotation()
    {
        float pitch = _input.PitchInput;
        float roll  = _input.RollInput;
        float rotationSpeedScale = _input.useVR ? Mathf.Max(0f, _input.vrRotationSpeedScale) : 1f;

        // ── Pitch: 기수 올리기/내리기 (로컬 X축 회전)
        float pitchAmount = -pitch * settings.pitchSpeed * rotationSpeedScale * Time.fixedDeltaTime;

        // ── Roll: 좌우 기울기 (로컬 Z축 회전, 우측 입력 → 음수 방향)
        float rollAmount = -roll * settings.rollSpeed * rotationSpeedScale * Time.fixedDeltaTime;

        // VR 조종에서는 좌/우 컨트롤러 기울임이 순수 롤만 담당한다.
        // 기체가 기울어진 뒤 pitch가 로컬 축으로 적용되면서 진행 방향이 자연스럽게 바뀐다.
        float yawAmount = _input.useVR
            ? 0f
            : roll * settings.rollSpeed * settings.autoYawStrength * Time.fixedDeltaTime;

        // 목표 회전값을 바로 적용한다. VR 조종은 로컬 회전 기준이라 pitch 제한을 두지 않는다.
        Quaternion deltaRotation = Quaternion.Euler(pitchAmount, yawAmount, rollAmount);
        Quaternion targetRotation = _rb.rotation * deltaRotation;
        _rb.MoveRotation(targetRotation);
    }

    // ──────────────────────────────────────────────
    //  디버그 시각화
    // ──────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        // 전진 방향 표시 (파란색)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        // 상향 방향 표시 (초록색)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.up * 3f);
    }
}
