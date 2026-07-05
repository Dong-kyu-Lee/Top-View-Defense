using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛 연출(총구 플래시·탄체·임팩트)용 경량 오브젝트 풀. 발사는 초당 다수 × 터렛 다수라
    /// Instantiate/Destroy 반복이 GC 스파이크를 내므로, 프리팹별로 인스턴스를 재사용한다.
    ///
    /// 이 계층은 <b>순수 연출</b>이다 — 데미지는 <see cref="Turret"/>의 히트스캔에서 이미 확정되고,
    /// 풀이 다루는 오브젝트는 게임플레이(<see cref="Combat.IDamageable"/>)에 관여하지 않는다.
    ///
    /// 씬 배선이 필요 없다: 최초 사용 시 자동 생성되고(<see cref="Instance"/>), 씬이 바뀌면 함께 파괴된다.
    /// </summary>
    public sealed class VfxPool : MonoBehaviour
    {
        private static VfxPool _instance;

        private static VfxPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("VfxPool");
                    _instance = go.AddComponent<VfxPool>();
                }
                return _instance;
            }
        }

        // 프리팹 → 비활성 인스턴스 큐. 인스턴스는 PooledInstance 마커로 자기 출처 프리팹을 기억한다.
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools =
            new Dictionary<GameObject, Queue<GameObject>>();

        private const float DefaultOneShotLifetime = 1.5f;

        /// <summary>탄체 등 스스로 수명을 관리하는 오브젝트를 풀에서 꺼낸다(사용 후 <see cref="Release"/> 필수).</summary>
        public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot)
            => Instance.GetInternal(prefab, pos, rot);

        /// <summary>오브젝트를 풀로 반납한다(비활성화 후 재사용 대기).</summary>
        public static void Release(GameObject instance) => Instance.ReleaseInternal(instance);

        /// <summary>총구 플래시·임팩트 같은 1회성 파티클을 재생하고, 파티클 수명 뒤 자동 반납한다.</summary>
        public static void PlayOneShot(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;
            GameObject go = Instance.GetInternal(prefab, pos, rot);
            Instance.StartCoroutine(Instance.ReleaseAfter(go, Instance.LifetimeOf(go)));
        }

        private GameObject GetInternal(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;

            Queue<GameObject> q = PoolFor(prefab);
            GameObject go = null;
            while (q.Count > 0 && go == null)
                go = q.Dequeue(); // 씬 전환 등으로 파괴된(=null) 항목은 건너뛴다.

            if (go == null)
            {
                go = Instantiate(prefab);
                PooledInstance marker = go.AddComponent<PooledInstance>();
                marker.Prefab = prefab;
            }

            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            PlayParticles(go);
            return go;
        }

        private void ReleaseInternal(GameObject go)
        {
            if (go == null) return;

            StopParticles(go);
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            PooledInstance marker = go.GetComponent<PooledInstance>();
            if (marker != null && marker.Prefab != null)
                PoolFor(marker.Prefab).Enqueue(go);
            else
                Destroy(go); // 마커 없는 오브젝트는 풀 키가 없으므로 그냥 파괴(안전장치).
        }

        private IEnumerator ReleaseAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReleaseInternal(go);
        }

        private Queue<GameObject> PoolFor(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out Queue<GameObject> q))
            {
                q = new Queue<GameObject>();
                _pools[prefab] = q;
            }
            return q;
        }

        // 파티클 시스템 수명(재생 시간 + 파티클 최대 수명) 중 최댓값. 파티클이 없으면 기본값.
        private float LifetimeOf(GameObject go)
        {
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            float max = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                float life = main.duration + main.startLifetime.constantMax;
                if (life > max) max = life;
            }
            return max > 0f ? max : DefaultOneShotLifetime;
        }

        private static void PlayParticles(GameObject go)
        {
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(true);
                systems[i].Play(true);
            }
        }

        private static void StopParticles(GameObject go)
        {
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>풀 인스턴스가 자기 출처 프리팹을 기억하기 위한 마커(런타임 부착 전용).</summary>
    public sealed class PooledInstance : MonoBehaviour
    {
        public GameObject Prefab;
    }
}
