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

        // ── Pitch: 기수 올리기/내리기 (로컬 X축 회전)
        float pitchAmount = -pitch * settings.pitchSpeed * Time.fixedDeltaTime;

        // ── Roll: 좌우 기울기 (로컬 Z축 회전, 우측 입력 → 음수 방향)
        float rollAmount = -roll * settings.rollSpeed * Time.fixedDeltaTime;

        // ── 자동 Yaw: 롤 방향으로 자연스럽게 선회 (로컬 Y축)
        float yawAmount = roll * settings.rollSpeed * settings.autoYawStrength * Time.fixedDeltaTime;

        Quaternion deltaRotation = Quaternion.Euler(pitchAmount, yawAmount, rollAmount);
        _rb.MoveRotation(_rb.rotation * deltaRotation);

        // ── 피치 각도 제한 (너무 수직으로 세워지지 않도록)
        ClampPitchAngle();
    }

    // ──────────────────────────────────────────────
    //  피치 각도 제한
    // ──────────────────────────────────────────────
    private void ClampPitchAngle()
    {
        if (settings.maxPitchAngle >= 90f) return;

        Vector3 forward = transform.forward;
        float pitchAngle = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        if (Mathf.Abs(pitchAngle) > settings.maxPitchAngle)
        {
            float clampedPitch = Mathf.Clamp(pitchAngle, -settings.maxPitchAngle, settings.maxPitchAngle);
            float clampedPitchRad = clampedPitch * Mathf.Deg2Rad;

            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            Vector3 clampedForward = new Vector3(
                flatForward.x * Mathf.Cos(clampedPitchRad),
                Mathf.Sin(clampedPitchRad),
                flatForward.z * Mathf.Cos(clampedPitchRad)
            ).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(clampedForward, transform.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRotation, 0.5f));
        }
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
