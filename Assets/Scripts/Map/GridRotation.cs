using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 회전 구역(N x N)의 논리 좌표와 방향을 90° 배수로 회전시키는 순수 유틸리티.
    /// 시각(Transform) 회전과 별개로, 그리드 데이터를 동기화하는 데 사용한다.
    ///
    /// 좌표계 규약: x = 열(오른쪽 +), y = 행(위쪽 +). 위에서 내려다본 탑뷰 기준.
    /// quarterTurnsCW = 시계방향 90° 스텝 수 (1=90°, 2=180°, 3=270°, 음수=반시계).
    /// </summary>
    public static class GridRotation
    {
        /// <summary>
        /// N x N 구역 안의 로컬 좌표를 시계방향으로 quarterTurnsCW * 90° 회전한 좌표를 반환.
        /// 구역 크기 밖으로 벗어나지 않도록 구역 중심 기준으로 재매핑된다.
        /// </summary>
        /// <param name="local">구역 좌하단(0,0) 기준 로컬 좌표. 범위 0..size-1.</param>
        /// <param name="size">구역 한 변의 칸 수 (예: 3 또는 4).</param>
        public static Vector2Int RotateInBlock(Vector2Int local, int size, int quarterTurnsCW)
        {
            int turns = Mod(quarterTurnsCW, 4);
            int max = size - 1;
            for (int i = 0; i < turns; i++)
            {
                // 시계 90°: (x, y) -> (y, max - x)
                local = new Vector2Int(local.y, max - local.x);
            }
            return local;
        }

        /// <summary>
        /// 그리드 전역 좌표를, origin에서 시작하는 size x size 회전 구역 기준으로 회전시킨 전역 좌표.
        /// </summary>
        /// <param name="world">그리드 전역 좌표.</param>
        /// <param name="origin">회전 구역의 좌하단 전역 좌표.</param>
        /// <param name="size">구역 한 변의 칸 수.</param>
        public static Vector2Int RotateWorld(Vector2Int world, Vector2Int origin, int size, int quarterTurnsCW)
        {
            Vector2Int local = world - origin;
            Vector2Int rotated = RotateInBlock(local, size, quarterTurnsCW);
            return origin + rotated;
        }

        /// <summary>지정 좌표가 origin~origin+size 구역 안에 포함되는지.</summary>
        public static bool Contains(Vector2Int world, Vector2Int origin, int size)
        {
            return world.x >= origin.x && world.x < origin.x + size
                && world.y >= origin.y && world.y < origin.y + size;
        }

        /// <summary>방향을 시계방향으로 quarterTurnsCW * 90° 회전.</summary>
        public static Direction Rotate(Direction dir, int quarterTurnsCW)
        {
            // enum 순서(N=0,E=1,S=2,W=3)가 시계방향이므로 그대로 더하면 된다.
            return (Direction)Mod((int)dir + quarterTurnsCW, 4);
        }

        /// <summary>Transform 회전에 사용할 Y축 각도(도). 시계 90°는 월드 +Y 기준 +90°.</summary>
        public static float ToYawDegrees(int quarterTurnsCW) => quarterTurnsCW * 90f;

        /// <summary>C#의 % 는 음수에서 음수를 반환하므로 항상 [0, m) 로 정규화.</summary>
        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}
