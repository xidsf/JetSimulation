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

        // ── Pitch: 기수 올리기/내리기 (로컬 X축 회전)
        float pitchAmount = -pitch * settings.pitchSpeed * Time.fixedDeltaTime;

        // ── Roll: 좌우 기울기 (로컬 Z축 회전, 우측 입력 → 음수 방향)
        float rollAmount = -roll * settings.rollSpeed * Time.fixedDeltaTime;

        // ── 자동 Yaw: 롤 방향으로 자연스럽게 선회 (로컬 Y축)
        float yawAmount = roll * settings.rollSpeed * settings.autoYawStrength * Time.fixedDeltaTime;

        // 목표 회전값을 먼저 계산한 뒤 피치 제한 적용 (1프레임 지연 방지)
        Quaternion deltaRotation = Quaternion.Euler(pitchAmount, yawAmount, rollAmount);
        Quaternion targetRotation = _rb.rotation * deltaRotation;

        // ── 피치 각도 제한 적용 후 한 번만 MoveRotation 호출
        targetRotation = ClampPitchAngle(targetRotation);
        _rb.MoveRotation(targetRotation);
    }

    // ──────────────────────────────────────────────
    //  피치 각도 제한
    // ──────────────────────────────────────────────
    // 피치 각도를 제한한 Quaternion을 반환 (1프레임 지연 없이 목표 회전값 기준으로 처리)
    private Quaternion ClampPitchAngle(Quaternion rotation)
    {
        if (settings.maxPitchAngle >= 90f) return rotation;

        // 계산된 목표 회전 기준의 forward 벡터 사용
        Vector3 forward = rotation * Vector3.forward;
        float pitchAngle = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        if (Mathf.Abs(pitchAngle) > settings.maxPitchAngle)
        {
            float clampedPitch = Mathf.Clamp(pitchAngle, -settings.maxPitchAngle, settings.maxPitchAngle);
            float clampedPitchRad = clampedPitch * Mathf.Deg2Rad;

            // 수직(90도)에 가까울 때 flatForward가 zero벡터가 되는 NaN 방지
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }
            else
            {
                flatForward.Normalize();
            }

            Vector3 clampedForward = new Vector3(
                flatForward.x * Mathf.Cos(clampedPitchRad),
                Mathf.Sin(clampedPitchRad),
                flatForward.z * Mathf.Cos(clampedPitchRad)
            ).normalized;

            Quaternion clampedRotation = Quaternion.LookRotation(clampedForward, rotation * Vector3.up);
            return Quaternion.Slerp(rotation, clampedRotation, 0.5f);
        }

        return rotation;
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
