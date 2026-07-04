namespace TopViewDefense.Combat
{
    /// <summary>
    /// 공격의 속성. 실드병(공병)의 속성 내성이나 파이어/프리즈 터렛의 광역 효과 구분에 사용.
    /// (claude.md 4·5장 - 속성 내성 / 화염·경직탄)
    /// 지금은 값만 정의하고, 내성/도트 등 속성별 로직은 이후 페이즈에서 붙인다.
    /// </summary>
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Freeze = 2,
        Energy = 3,
    }

    /// <summary>
    /// 데미지를 받을 수 있는 대상의 공통 계약. 터렛(공격 측)과 적(피격 측)을 분리하기 위한 인터페이스.
    /// 터렛은 이 인터페이스로만 대상을 때리므로, 이후 파괴 가능한 장애물 등도 동일하게 확장할 수 있다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>이미 사망(파괴)했는지. 죽은 대상은 타게팅/피격에서 제외한다.</summary>
        bool IsDead { get; }

        /// <summary>월드 좌표(타게팅·투사체 조준용).</summary>
        UnityEngine.Vector3 Position { get; }

        /// <summary>데미지를 입힌다. type은 속성(내성/도트 판정용).</summary>
        void TakeDamage(float amount, DamageType type = DamageType.Physical);
    }
}
