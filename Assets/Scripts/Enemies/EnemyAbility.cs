using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 적의 특수 능력을 표현하는 컴포넌트의 추상 베이스. (claude.md 4장 - 종류별 특수 행동의 확장점)
    ///
    /// 능력치는 <see cref="EnemyData"/>(데이터)로, "능동 행동"은 이 컴포넌트로 분리한다.
    /// 능력은 적 프리팹에 부착해 두면 <see cref="Enemy.Init"/>가 <see cref="Initialize"/>로 소유자를 주입한다.
    /// 5번째 이후의 특수 적도 이 클래스를 상속한 컴포넌트를 프리팹에 붙이는 것으로 확장된다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnemyAbility : MonoBehaviour
    {
        /// <summary>이 능력을 소유한 적. Enemy.Init에서 주입된다.</summary>
        protected Enemy Owner { get; private set; }

        /// <summary>소유 적이 준비되면 호출된다. 상속 시 base.Initialize(owner)를 먼저 부른다.</summary>
        public virtual void Initialize(Enemy owner)
        {
            Owner = owner;
        }
    }
}
