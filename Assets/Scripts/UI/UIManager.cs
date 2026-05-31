using System.Collections.Generic;
using UnityEngine;

namespace JetSimulation.UI
{
    // 관리할 UI 패널들의 종류를 정의합니다.
    public enum UIPanelType
    {
        HUD,
        Warning,
        GameOver
    }

    // 인스펙터에서 Enum과 GameObject를 짝지어주기 위한 클래스
    [System.Serializable]
    public class UIPanelMapping
    {
        public UIPanelType panelType;
        public GameObject panelObject;
    }

    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Panels Setup")]
        // 유니티 인스펙터에서 등록할 리스트
        [SerializeField] private List<UIPanelMapping> panelMappings;

        // 실제 런타임에서 빠르게 검색할 딕셔너리
        private Dictionary<UIPanelType, GameObject> uiMap = new Dictionary<UIPanelType, GameObject>();

        private void Awake()
        {
            // 싱글톤 세팅 (GameManager 등에서 쉽게 접근하기 위함)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 리스트 데이터를 딕셔너리로 변환하여 검색 속도(O(1)) 확보
            foreach (var mapping in panelMappings)
            {
                if (mapping.panelObject != null && !uiMap.ContainsKey(mapping.panelType))
                {
                    uiMap.Add(mapping.panelType, mapping.panelObject);

                    // 초기화 시 HUD를 제외한 모든 패널 끄기
                    mapping.panelObject.SetActive(mapping.panelType == UIPanelType.HUD);
                }
            }
        }

        /// <summary>
        /// 특정 UI 패널만 활성화하고 나머지는 비활성화합니다.
        /// </summary>
        public void ShowPanel(UIPanelType typeToShow)
        {
            foreach (var kvp in uiMap)
            {
                kvp.Value.SetActive(kvp.Key == typeToShow);
            }
            Debug.Log($"[UIManager] {typeToShow} 화면 활성화");
        }

        /// <summary>
        /// 기존 화면을 유지하면서 특정 경고창 등을 추가로 띄울 때 사용합니다.
        /// </summary>
        public void EnablePanelOverlay(UIPanelType typeToEnable, bool enable)
        {
            if (uiMap.TryGetValue(typeToEnable, out GameObject panel))
            {
                panel.SetActive(enable);
            }
        }
    }
}