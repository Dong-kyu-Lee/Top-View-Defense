using System.Collections;
using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 일정 간격으로 <see cref="PlayerEconomy"/>에 에너지를 자동 지급한다.
    /// (CLAUDE.md 7장 ② - "n초 간격으로 맵에 기본 자동 드랍")
    ///
    /// 스테이지 진행 중 꾸준히 소량의 에너지를 흘려보내 배치/재배치 선택지를 유지시키는 역할.
    /// 적 처치 드랍·에너지 터렛 생산과 함께 에너지 획득처의 한 축이 된다.
    ///
    /// 일시정지 연동: <c>WaitForSeconds</c>가 <c>Time.timeScale</c>을 따르므로,
    /// 이후 설정 메뉴에서 timeScale=0으로 일시정지하면 티커도 자동으로 함께 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnergyDripper : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("에너지를 지급할 지갑. 비우면 Instance 또는 씬에서 탐색.")]
        [SerializeField] private PlayerEconomy economy;

        [Header("Drip")]
        [Tooltip("자동 지급 간격(초).")]
        [Min(0.1f)] [SerializeField] private float interval = 10f;

        [Tooltip("1회 지급량.")]
        [Min(1)] [SerializeField] private int amountPerTick = 10;

        [Tooltip("체크 시 Start에서 자동 시작.")]
        [SerializeField] private bool autoBegin = true;

        [Tooltip("체크 시 시작 직후 첫 간격을 기다리지 않고 즉시 1회 지급.")]
        [SerializeField] private bool dripOnStart = false;

        private Coroutine _loop;

        private void Start()
        {
            if (autoBegin) Begin();
        }

        /// <summary>자동 지급을 시작한다(중복 무시).</summary>
        public void Begin()
        {
            if (_loop != null) return;

            if (economy == null)
                economy = PlayerEconomy.Instance ?? FindObjectOfType<PlayerEconomy>();
            if (economy == null)
            {
                Debug.LogError("[EnergyDripper] PlayerEconomy를 찾을 수 없습니다.", this);
                return;
            }

            _loop = StartCoroutine(Drip());
        }

        /// <summary>자동 지급을 중단한다.</summary>
        public void StopDrip()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        private IEnumerator Drip()
        {
            if (dripOnStart)
                economy.Add(amountPerTick);

            var wait = new WaitForSeconds(interval);
            while (true)
            {
                yield return wait;
                economy.Add(amountPerTick);
            }
        }

        private void OnDisable() => StopDrip();
    }
}
