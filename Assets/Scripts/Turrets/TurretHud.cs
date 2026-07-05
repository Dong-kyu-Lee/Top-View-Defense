using System.Collections.Generic;
using TopViewDefense.Core;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 프로토타입용 즉석 HUD(IMGUI). 화면 하단에 터렛 선택 버튼과 좌상단에 에너지 잔량을 그린다.
    /// 정식 uGUI 슬롯 UI는 이후 교체 — MapBuilder의 Cube 폴백처럼, 씬 배선 없이 배치 루프를
    /// 바로 굴려보기 위한 임시 계층이다.
    ///
    /// 버튼 클릭 → <see cref="TurretPlacer.Arm"/>. 목록은 인스펙터 지정, 비우면 Resources/TurretData 전체 로드.
    /// (하단 버튼 클릭이 배치로 오인되지 않도록 <see cref="TurretPlacer"/>가 하단 여백을 무시한다.)
    /// </summary>
    public class TurretHud : MonoBehaviour
    {
        [SerializeField] private TurretPlacer placer;

        [Tooltip("표시할 터렛 목록. 비우면 Resources/TurretData 폴더 전체를 로드.")]
        [SerializeField] private List<TurretData> turrets = new List<TurretData>();

        private void Awake()
        {
            if (placer == null) placer = FindObjectOfType<TurretPlacer>();
            if (turrets == null || turrets.Count == 0)
                turrets = new List<TurretData>(Resources.LoadAll<TurretData>("TurretData"));
        }

        private void OnGUI()
        {
            const float w = 150f, h = 56f, pad = 8f;

            int energy = PlayerEconomy.Instance != null ? PlayerEconomy.Instance.Energy : 0;
            GUI.Label(new Rect(pad, pad, 300f, 24f), $"에너지: {energy}");

            float y = Screen.height - h - pad;

            if (placer != null && placer.Armed != null)
                GUI.Label(new Rect(pad, y - 24f, 500f, 24f),
                    $"'{placer.Armed.displayName}' 배치할 칸을 클릭  (우클릭/ESC 취소)");

            float x = pad;
            for (int i = 0; i < turrets.Count; i++)
            {
                TurretData d = turrets[i];
                if (d == null) continue;

                bool armed = placer != null && placer.Armed == d;
                bool affordable = PlayerEconomy.Instance == null || PlayerEconomy.Instance.CanAfford(d.cost);

                GUI.enabled = affordable;
                string label = $"{(armed ? "▶ " : string.Empty)}{d.displayName}\n({d.cost} E)";
                if (GUI.Button(new Rect(x, y, w, h), label) && placer != null)
                    placer.Arm(d);
                GUI.enabled = true;

                x += w + pad;
            }
        }
    }
}
