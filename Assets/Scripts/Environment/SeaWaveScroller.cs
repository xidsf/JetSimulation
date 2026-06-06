using UnityEngine;

namespace JetSimulation.Environment
{
    public class SeaWaveScroller : MonoBehaviour
    {
        [Header("Main Waves")]
        [Tooltip("메인 파도가 흐르는 속도와 방향")]
        [SerializeField] private Vector2 mainWaveSpeed = new Vector2(0.015f, 0.02f);

        [Header("Detail Waves (Randomness)")]
        [Tooltip("불규칙성을 만들어낼 두 번째 파도의 속도와 방향 (메인과 엇갈리게 설정)")]
        [SerializeField] private Vector2 detailWaveSpeed = new Vector2(-0.01f, 0.025f);

        private Renderer seaRenderer;
        private Material seaMaterial;

        // 시간에 따른 누적 오프셋을 저장할 변수
        private Vector2 mainOffset;
        private Vector2 detailOffset;

        private void Start()
        {
            seaRenderer = GetComponent<Renderer>();
            if (seaRenderer != null)
            {
                seaMaterial = seaRenderer.material;
            }
        }

        private void Update()
        {
            if (seaMaterial != null)
            {
                // 각 파도의 위치를 매 프레임 독립적으로 누적 계산
                mainOffset += mainWaveSpeed * Time.deltaTime;
                detailOffset += detailWaveSpeed * Time.deltaTime;

                // 1. 메인 노말맵 이동 (기본 형태)
                seaMaterial.SetTextureOffset("_BaseMap", mainOffset);
                seaMaterial.SetTextureOffset("_BumpMap", mainOffset);

                // 2. 디테일 노말맵 이동 (불규칙한 교차 생성)
                // URP Lit 쉐이더의 디테일 맵 오프셋 속성에 접근
                seaMaterial.SetTextureOffset("_DetailAlbedoMap", detailOffset);
                seaMaterial.SetTextureOffset("_DetailNormalMap", detailOffset);
            }
        }
    }
}