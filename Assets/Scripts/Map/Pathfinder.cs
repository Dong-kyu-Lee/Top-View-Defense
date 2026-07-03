using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 흐름장(Flow Field) 기반 경로탐색. 기지에서 전체 그리드로 역방향 Dijkstra를 1회 수행해
    /// 모든 셀에 "기지로 가는 다음 셀"과 최소 누적 비용을 채운다.
    ///
    /// 설계 근거(문서 5장 - 런타임 흐름 / CLAUDE.md 3장):
    /// - 목표(기지)는 하나, 적은 다수, 회전으로 적 위치가 수시로 바뀜, 전체 재계산이 잦음
    ///   → 적마다 경로를 들고 다니는 A*보다, 셀마다 방향을 심어두는 흐름장이 이 패턴에 맞다.
    ///   적은 매 프레임 "지금 선 셀의 다음 방향"만 읽으면 되고, 회전으로 밀려나도 새 셀 방향을 읽으면 된다.
    /// - 폐쇄회로 방지 룰(길이 완전히 막히면 장애물을 부수고 통과):
    ///   통행 가능 셀은 진입 비용 1, 파괴 가능 장애물은 큰 진입 비용을 부여한 단일 패스로 흡수한다.
    ///   → 걸어갈 길이 있으면 장애물을 절대 쓰지 않고, 막힌 곳에서만 최소한의 장애물을 관통한다.
    ///
    /// GridState만 의존하는 순수 로직. 좌표는 전부 그리드 좌표(Vector2Int).
    /// 회전/장애물 파괴 등으로 지형이 바뀌면 호출자가 <see cref="Recompute"/>를 다시 호출한다.
    /// </summary>
    public class Pathfinder
    {
        /// <summary>통행 가능 셀 진입 비용.</summary>
        private const int WalkCost = 1;

        /// <summary>
        /// 파괴 가능 장애물 진입 비용. 그리드에서 나올 수 있는 어떤 통행 경로의 총 비용보다도 커야
        /// "걸어갈 길이 있으면 장애물을 절대 쓰지 않는다"가 보장된다. (Width*Height*WalkCost 상한을 크게 상회)
        /// </summary>
        private const int ObstacleCost = 1_000_000;

        private const int Unreachable = int.MaxValue;

        private static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private readonly GridState _grid;

        /// <summary>각 셀에서 기지까지의 최소 누적 비용. 미도달은 <see cref="Unreachable"/>.</summary>
        private readonly int[,] _cost;

        /// <summary>각 셀에서 기지로 향하는 다음 셀. 미도달/기지 자신은 자기 좌표.</summary>
        private readonly Vector2Int[,] _next;

        private readonly List<Vector2Int> _destructionTargets = new List<Vector2Int>();

        private readonly MinHeap _heap;

        public Pathfinder(GridState grid)
        {
            _grid = grid;
            _cost = new int[grid.Width, grid.Height];
            _next = new Vector2Int[grid.Width, grid.Height];
            _heap = new MinHeap(grid.Width * grid.Height);
            Recompute();
        }

        /// <summary>
        /// 이번 계산에서 적이 통과하며 파괴하게 될 장애물 셀 목록(스폰→기지 경로 위의 Obstacle).
        /// 경고 UI/사전 파괴 타게팅에 사용. 통행 경로가 열려 있으면 비어 있다.
        /// </summary>
        public IReadOnlyList<Vector2Int> DestructionTargets => _destructionTargets;

        /// <summary>지형 변경(회전/장애물 파괴) 후 흐름장 전체를 다시 계산한다.</summary>
        public void Recompute()
        {
            int w = _grid.Width, h = _grid.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    _cost[x, y] = Unreachable;
                    _next[x, y] = new Vector2Int(x, y);
                }
            }

            _heap.Clear();

            // 다중 소스: 기지 셀(들)을 비용 0으로 시드. 없으면 GridState 폴백(중앙).
            List<Vector2Int> bases = _grid.FindCells(TileType.Base);
            if (bases.Count == 0)
                bases.Add(_grid.BaseCell);

            foreach (Vector2Int b in bases)
            {
                if (!_grid.InBounds(b)) continue;
                _cost[b.x, b.y] = 0;
                _heap.Push(0, b.x, b.y);
            }

            // 기지에서 바깥으로 역방향 확장. 셀 c(기지 방향)에서 이웃 n으로 관계를 relax:
            // 적은 n -> c 로 이동하므로 n의 진입 비용을 n에 귀속시킨다.
            while (_heap.TryPop(out int cost, out int cx, out int cy))
            {
                // 지연 삭제: 이미 더 낮은 비용으로 확정된 항목이면 스킵.
                if (cost > _cost[cx, cy]) continue;

                for (int i = 0; i < Neighbors.Length; i++)
                {
                    int nx = cx + Neighbors[i].x;
                    int ny = cy + Neighbors[i].y;
                    if (!_grid.InBounds(nx, ny)) continue;

                    int enter = EntryCost(_grid.GetTile(nx, ny));
                    if (enter < 0) continue; // 통행 불가(Buildable 등)

                    int newCost = cost + enter;
                    if (newCost < _cost[nx, ny])
                    {
                        _cost[nx, ny] = newCost;
                        _next[nx, ny] = new Vector2Int(cx, cy);
                        _heap.Push(newCost, nx, ny);
                    }
                }
            }

            RebuildDestructionTargets(bases);
        }

        /// <summary>셀 진입 비용. 통행 가능=1, 파괴 가능 장애물=큰 값, 통행 불가= -1.</summary>
        private static int EntryCost(TileType type)
        {
            if (type.IsWalkable()) return WalkCost;
            if (type.IsDestructible()) return ObstacleCost;
            return -1;
        }

        /// <summary>from에서 기지까지 도달 가능한 경로가 있는지.</summary>
        public bool HasPath(Vector2Int from)
            => _grid.InBounds(from) && _cost[from.x, from.y] != Unreachable;

        /// <summary>
        /// from 셀에서 기지로 가는 다음 셀을 반환. 경로가 없으면 false.
        /// 기지 셀에서는 자기 자신을 반환(도착 판정은 호출자 책임).
        /// </summary>
        public bool TryGetNextStep(Vector2Int from, out Vector2Int next)
        {
            if (!HasPath(from))
            {
                next = from;
                return false;
            }
            next = _next[from.x, from.y];
            return true;
        }

        /// <summary>from 셀의 기지까지 최소 누적 비용. 미도달은 int.MaxValue.</summary>
        public int GetCost(Vector2Int from)
            => _grid.InBounds(from) ? _cost[from.x, from.y] : Unreachable;

        // 스폰 셀에서 기지까지 next를 따라가며 실제로 관통하는 장애물만 파괴 대상으로 수집.
        private void RebuildDestructionTargets(List<Vector2Int> bases)
        {
            _destructionTargets.Clear();

            var baseSet = new HashSet<Vector2Int>(bases);
            var seen = new HashSet<Vector2Int>();

            foreach (Vector2Int spawn in _grid.FindCells(TileType.Spawn))
            {
                if (!HasPath(spawn)) continue;

                Vector2Int cur = spawn;
                int guard = _grid.Width * _grid.Height + 1; // 순환 방지 상한
                while (!baseSet.Contains(cur) && guard-- > 0)
                {
                    if (_grid.GetTile(cur).IsDestructible() && seen.Add(cur))
                        _destructionTargets.Add(cur);

                    Vector2Int step = _next[cur.x, cur.y];
                    if (step == cur) break; // 진행 불가(안전장치)
                    cur = step;
                }
            }
        }

        /// <summary>
        /// (cost 우선) 이진 최소 힙. Vector2Int 대신 x/y를 병렬 배열로 담아 박싱/할당을 피한다.
        /// </summary>
        private sealed class MinHeap
        {
            private int[] _cost;
            private int[] _x;
            private int[] _y;
            private int _count;

            public MinHeap(int capacity)
            {
                capacity = Mathf.Max(4, capacity);
                _cost = new int[capacity];
                _x = new int[capacity];
                _y = new int[capacity];
            }

            public void Clear() => _count = 0;

            public void Push(int cost, int x, int y)
            {
                if (_count == _cost.Length) Grow();

                int i = _count++;
                _cost[i] = cost; _x[i] = x; _y[i] = y;

                // 상향 정렬(sift-up)
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_cost[parent] <= _cost[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public bool TryPop(out int cost, out int x, out int y)
            {
                if (_count == 0)
                {
                    cost = 0; x = 0; y = 0;
                    return false;
                }

                cost = _cost[0]; x = _x[0]; y = _y[0];

                int last = --_count;
                _cost[0] = _cost[last]; _x[0] = _x[last]; _y[0] = _y[last];

                // 하향 정렬(sift-down)
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < _count && _cost[left] < _cost[smallest]) smallest = left;
                    if (right < _count && _cost[right] < _cost[smallest]) smallest = right;
                    if (smallest == i) break;
                    Swap(smallest, i);
                    i = smallest;
                }
                return true;
            }

            private void Swap(int a, int b)
            {
                (_cost[a], _cost[b]) = (_cost[b], _cost[a]);
                (_x[a], _x[b]) = (_x[b], _x[a]);
                (_y[a], _y[b]) = (_y[b], _y[a]);
            }

            private void Grow()
            {
                int n = _cost.Length * 2;
                System.Array.Resize(ref _cost, n);
                System.Array.Resize(ref _x, n);
                System.Array.Resize(ref _y, n);
            }
        }
    }
}
