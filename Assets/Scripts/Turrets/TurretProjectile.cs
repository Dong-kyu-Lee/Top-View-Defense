using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛이 쏘는 <b>순수 연출용</b> 탄체. 데미지는 <see cref="Turret"/>의 히트스캔에서 발사 시점에
    /// 이미 확정됐으므로, 이 컴포넌트는 게임플레이(<see cref="Combat.IDamageable"/>)에 관여하지 않는다.
    /// 목표(살아 있으면 추적, 죽으면 마지막 지점)로 날아가 임팩트 파티클을 남기고 <see cref="VfxPool"/>로 반납된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurretProjectile : MonoBehaviour
    {
        private const float ArriveDistance = 0.08f; // 도착 판정 거리(월드).
        private const float MaxLifetime = 5f;       // 목표를 영영 못 잡는 경우의 안전 상한.

        private Transform _target;      // 살아 있는 동안 추적할 대상(파괴되면 null → 마지막 목적지 사용).
        private Vector3 _destination;   // 목표가 없을 때 향하는 고정 지점(발사 시 스냅샷).
        private float _speed;
        private GameObject _impactPrefab;
        private float _life;

        /// <summary>발사. start에서 출발해 target(있으면 추적)/destination으로 이동한다.</summary>
        public void Launch(Vector3 start, Transform target, Vector3 destination, float speed, GameObject impactPrefab)
        {
            transform.position = start;
            _target = target;
            _destination = destination;
            _speed = Mathf.Max(0.01f, speed);
            _impactPrefab = impactPrefab;
            _life = 0f;
            FaceTravel(_destination - start);
        }

        private void Update()
        {
            _life += Time.deltaTime;

            // 대상이 살아 있으면 목적지를 그 위치로 갱신(호밍). 파괴됐으면 마지막 목적지를 유지.
            if (_target != null) _destination = _target.position;

            Vector3 pos = transform.position;
            Vector3 to = _destination - pos;

            if (to.sqrMagnitude <= ArriveDistance * ArriveDistance || _life >= MaxLifetime)
            {
                Arrive();
                return;
            }

            float step = _speed * Time.deltaTime;
            transform.position = pos + to.normalized * Mathf.Min(step, to.magnitude);
            FaceTravel(to);
        }

        private void Arrive()
        {
            if (_impactPrefab != null)
                VfxPool.PlayOneShot(_impactPrefab, transform.position, transform.rotation);
            _target = null;
            VfxPool.Release(gameObject);
        }

        private void FaceTravel(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
