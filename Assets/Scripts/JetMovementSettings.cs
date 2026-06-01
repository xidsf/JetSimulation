using UnityEngine;

/// <summary>
/// 비행기 조종 수치 설정값을 보관하는 ScriptableObject.
/// 메뉴: Assets > Create > JetSim > Movement Settings
/// </summary>
[CreateAssetMenu(fileName = "JetMovementSettings", menuName = "JetSim/Movement Settings", order = 1)]
public class JetMovementSettings : ScriptableObject
{
    [Header("=== 전진 속도 ===")]
    [Tooltip("비행기의 자동 전진 속도 (m/s)")]
    public float forwardSpeed = 80f;

    [Header("=== 피치 (Pitch: 상승/하강) ===")]
    [Tooltip("피치 회전 속도 (도/초)")]
    public float pitchSpeed = 50f;

    [Tooltip("최대 피치 각도 제한 (도)")]
    public float maxPitchAngle = 70f;

    [Header("=== 롤 (Roll: 좌우 기울기) ===")]
    [Tooltip("롤 회전 속도 (도/초)")]
    public float rollSpeed = 80f;

    [Header("=== 자동 Yaw ===")]
    [Tooltip("롤 입력에 비례해서 자동 Yaw 적용되는 강도")]
    [Range(0f, 1f)]
    public float autoYawStrength = 0.35f;

    [Header("=== 입력 보정 ===")]
    [Tooltip("마우스 입력 민감도")]
    public float mouseSensitivity = 2.5f;

    [Tooltip("입력값이 이 값 이하이면 0으로 처리 (데드존)")]
    [Range(0f, 0.3f)]
    public float inputDeadzone = 0.05f;

    [Tooltip("마우스를 멈춰도 입력값이 0으로 복귀하는 속도 (Lerp 계수)")]
    public float inputSmoothing = 6f;

    [Header("=== 카메라 위치 ===")]
    [Tooltip("조종석 카메라의 로컬 Offset")]
    public Vector3 cockpitOffset = new Vector3(0f, 0.4f, 0.3f);
}
