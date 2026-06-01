using UnityEngine;

/// <summary>
/// 플레이어 리그(카메라)를 조종석 위치에 배치하는 스크립트.
/// PlayerJet 하위의 CockpitRoot에 붙입니다.
/// VR 모드 전환 시 이 스크립트에서 XR Rig를 세팅합니다.
/// </summary>
public class PlayerRigSetup : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector 설정
    // ──────────────────────────────────────────────
    [Header("=== 참조 ===")]
    [Tooltip("씬의 Main Camera Transform")]
    public Transform cameraTransform;

    [Tooltip("조종석 위치 오프셋 (로컬 좌표)")]
    public Vector3 cockpitLocalOffset = new Vector3(0f, 0.4f, 0.3f);

    [Header("=== VR 설정 (추후 사용) ===")]
    [Tooltip("XR Rig의 루트 Transform (VR 모드 시 할당)")]
    public Transform xrRigRoot;

    // ──────────────────────────────────────────────
    //  Unity 생명주기
    // ──────────────────────────────────────────────
    private void Start()
    {
        SetupCameraPosition();
    }

    // ──────────────────────────────────────────────
    //  카메라 초기 배치
    // ──────────────────────────────────────────────
    private void SetupCameraPosition()
    {
        if (cameraTransform == null)
        {
            // 자동으로 Main Camera 찾기
            Camera mainCam = Camera.main;
            if (mainCam != null)
                cameraTransform = mainCam.transform;
            else
            {
                Debug.LogWarning("[PlayerRigSetup] Main Camera를 찾을 수 없습니다.");
                return;
            }
        }

        if (VRInputManager.Instance != null && VRInputManager.Instance.useVR)
        {
            SetupForVR();
        }
        else
        {
            SetupForMouse();
        }
    }

    private void SetupForMouse()
    {
        // 카메라를 CockpitRoot의 자식으로 이동
        cameraTransform.SetParent(transform);
        cameraTransform.localPosition = cockpitLocalOffset;
        cameraTransform.localRotation = Quaternion.identity;

        Debug.Log("[PlayerRigSetup] 마우스 모드: 카메라를 조종석에 배치 완료");
    }

    private void SetupForVR()
    {
        // TODO: VR 구현 시 XR Rig를 조종석에 고정
        // xrRigRoot.SetParent(transform);
        // xrRigRoot.localPosition = cockpitLocalOffset;
        Debug.Log("[PlayerRigSetup] VR 모드: XR Rig 세팅 예정 위치");
    }
}
