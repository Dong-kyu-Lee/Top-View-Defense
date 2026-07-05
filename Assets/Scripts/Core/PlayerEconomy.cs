using System;
using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 인게임 전술 재화 '에너지' 지갑 (CLAUDE.md 7장 ②).
    /// 스테이지 입장 시 초기 에너지로 시작해 터렛 구매에 소모하고, 철거 시 환급받는다.
    /// 적 처치 드랍(<see cref="Enemies.EnemyManager.OnEnemyKilled"/>)·주기적 자동 드랍 연동은
    /// 이후 경제 페이즈에서 이 지갑의 <see cref="Add"/>를 호출해 붙인다.
    ///
    /// 터렛 배치/UI가 여러 곳에서 접근하므로 EnemyManager와 동일하게 가벼운 <see cref="Instance"/>를 제공.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEconomy : MonoBehaviour
    {
        public static PlayerEconomy Instance { get; private set; }

        [Tooltip("스테이지 입장 시 지급되는 초기 에너지(CLAUDE.md 6장: 예 100).")]
        [SerializeField] private int startingEnergy = 100;

        /// <summary>현재 보유 에너지.</summary>
        public int Energy { get; private set; }

        /// <summary>에너지 변동 시(인자는 현재값). HUD가 구독한다.</summary>
        public event Action<int> OnEnergyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerEconomy] 인스턴스가 이미 존재합니다. 중복을 제거합니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            Energy = Mathf.Max(0, startingEnergy);
        }

        private void Start() => OnEnergyChanged?.Invoke(Energy);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>보유 에너지가 cost 이상이면 차감하고 true. 부족하면 변화 없이 false.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || Energy < cost) return false;
            Energy -= cost;
            OnEnergyChanged?.Invoke(Energy);
            return true;
        }

        /// <summary>에너지 획득(적 처치 드랍/자동 드랍/철거 환급).</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            Energy += amount;
            OnEnergyChanged?.Invoke(Energy);
        }

        /// <summary>cost를 감당할 수 있는지(차감하지 않음).</summary>
        public bool CanAfford(int cost) => cost >= 0 && Energy >= cost;
    }
}
