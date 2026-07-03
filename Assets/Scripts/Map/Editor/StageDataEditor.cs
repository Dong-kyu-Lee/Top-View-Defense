using UnityEditor;
using UnityEngine;

namespace TopViewDefense.Map.EditorTools
{
    /// <summary>
    /// StageData 인스펙터에 바둑판 그리드 페인터를 그린다.
    /// - 팔레트에서 TileType 선택 후 격자를 클릭/드래그해 페인트
    /// - 좌클릭 = 선택 타입 칠하기, 우클릭(또는 우드래그) = Ground로 지우기
    /// - 회전 구역(rotationEvents)을 격자 위에 오버레이로 표시
    ///
    /// 좌표 규약(문서 참고): x=열, y=행, 좌하단 (0,0). 화면은 위→아래로 그리므로
    /// 화면 행 r 은 gridY = height-1-r 로 뒤집어 매핑한다.
    /// </summary>
    [CustomEditor(typeof(StageData))]
    public class StageDataEditor : Editor
    {
        private TileType _brush = TileType.Buildable;
        private const float MaxCellSize = 34f;
        private const float MinCellSize = 12f;

        public override void OnInspectorGUI()
        {
            // --- 기본 필드(단, tiles 배열은 격자로 편집하므로 숨김) ---
            serializedObject.Update();
            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // m_Script
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(prop, true);
            while (prop.NextVisible(false))
            {
                if (prop.name == "tiles") continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            serializedObject.ApplyModifiedProperties();

            var data = (StageData)target;
            data.EnsureSize();

            EditorGUILayout.Space(8);
            DrawPalette();
            EditorGUILayout.Space(6);
            DrawGrid(data);
            EditorGUILayout.Space(6);
            DrawUtilityButtons(data);
        }

        // ---------------------------------------------------------------- 팔레트

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("팔레트 (칠할 타일 선택)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
                {
                    bool selected = _brush == type;
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = ColorOf(type);
                    string label = (selected ? "● " : "") + type;
                    if (GUILayout.Toggle(selected, label, EditorStyles.miniButton, GUILayout.Height(22)) && !selected)
                        _brush = type;
                    GUI.backgroundColor = prev;
                }
            }
            EditorGUILayout.HelpBox("좌클릭/드래그 = 칠하기,  우클릭/드래그 = Ground로 지우기", MessageType.None);
        }

        // ---------------------------------------------------------------- 격자

        private void DrawGrid(StageData data)
        {
            int w = data.width, h = data.height;
            if (w <= 0 || h <= 0) return;

            float avail = EditorGUIUtility.currentViewWidth - 40f;
            float cell = Mathf.Clamp(avail / w, MinCellSize, MaxCellSize);
            float gridW = cell * w;
            float gridH = cell * h;

            Rect area = GUILayoutUtility.GetRect(gridW, gridH, GUILayout.ExpandWidth(false));
            float x0 = area.x, y0 = area.y;

            // 배경
            EditorGUI.DrawRect(area, new Color(0f, 0f, 0f, 0.35f));

            // 셀
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int gx = c;
                    int gy = h - 1 - r;
                    var cellRect = new Rect(x0 + c * cell + 1, y0 + r * cell + 1, cell - 2, cell - 2);
                    EditorGUI.DrawRect(cellRect, ColorOf(data.GetTile(gx, gy)));

                    // Base/Spawn 은 글자로 강조
                    TileType t = data.GetTile(gx, gy);
                    if (t == TileType.Base || t == TileType.Spawn)
                        DrawCellLabel(cellRect, t == TileType.Base ? "B" : "S");
                }
            }

            // 회전 구역 오버레이
            DrawRotationOverlays(data, x0, y0, cell);

            // 마우스 페인팅 처리
            HandlePaintEvents(data, area, x0, y0, cell);
        }

        private void HandlePaintEvents(StageData data, Rect area, float x0, float y0, float cell)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (!area.Contains(e.mousePosition)) return;
            if (e.button != 0 && e.button != 1) return;

            int c = Mathf.FloorToInt((e.mousePosition.x - x0) / cell);
            int r = Mathf.FloorToInt((e.mousePosition.y - y0) / cell);
            int gx = c;
            int gy = data.height - 1 - r;
            if (!data.InBounds(gx, gy)) return;

            TileType paint = e.button == 1 ? TileType.Ground : _brush;
            if (data.GetTile(gx, gy) == paint) { e.Use(); return; }

            Undo.RecordObject(data, "Paint Tile");
            data.SetTile(gx, gy, paint);
            EditorUtility.SetDirty(data);
            e.Use();
            Repaint();
        }

        private void DrawRotationOverlays(StageData data, float x0, float y0, float cell)
        {
            if (data.rotationEvents == null) return;
            for (int i = 0; i < data.rotationEvents.Count; i++)
            {
                var ev = data.rotationEvents[i];
                if (ev == null || ev.size <= 0) continue;

                // 구역 좌상단(화면 기준) = grid (origin.x, origin.y + size - 1)
                int topRow = data.height - 1 - (ev.origin.y + ev.size - 1);
                var zoneRect = new Rect(x0 + ev.origin.x * cell, y0 + topRow * cell, ev.size * cell, ev.size * cell);

                Color line = new Color(1f, 0.85f, 0.2f, 0.9f);
                DrawRectOutline(zoneRect, line, 2f);

                string arrow = ev.IsClockwise ? "↻" : "↺";
                int deg = Mathf.Abs(ev.quarterTurnsCW % 4) * 90;
                DrawCellLabel(new Rect(zoneRect.x, zoneRect.y, zoneRect.width, 16f),
                    $"#{i} {arrow}{deg}° @{ev.triggerTime:0}s", line);
            }
        }

        // ---------------------------------------------------------------- 유틸 버튼

        private void DrawUtilityButtons(StageData data)
        {
            EditorGUILayout.LabelField("빠른 편집", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 Ground"))
                    FillAll(data, TileType.Ground);
                if (GUILayout.Button("전체 Buildable"))
                    FillAll(data, TileType.Buildable);
                if (GUILayout.Button("중앙 기지"))
                {
                    Undo.RecordObject(data, "Place Base");
                    data.SetTile(data.width / 2, data.height / 2, TileType.Base);
                    EditorUtility.SetDirty(data);
                }
                if (GUILayout.Button("모서리 스폰"))
                {
                    Undo.RecordObject(data, "Place Spawns");
                    foreach (var s in data.CornerSpawns())
                        data.SetTile(s, TileType.Spawn);
                    EditorUtility.SetDirty(data);
                }
            }
        }

        private void FillAll(StageData data, TileType type)
        {
            Undo.RecordObject(data, "Fill Grid");
            for (int y = 0; y < data.height; y++)
                for (int x = 0; x < data.width; x++)
                    data.SetTile(x, y, type);
            EditorUtility.SetDirty(data);
            Repaint();
        }

        // ---------------------------------------------------------------- 그리기 헬퍼

        private static void DrawRectOutline(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);                        // top
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);          // bottom
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);                        // left
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);         // right
        }

        private static GUIStyle _labelStyle;
        private static void DrawCellLabel(Rect r, string text, Color? color = null)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                };
            }
            var prev = _labelStyle.normal.textColor;
            _labelStyle.normal.textColor = color ?? Color.white;
            GUI.Label(r, text, _labelStyle);
            _labelStyle.normal.textColor = prev;
        }

        private static Color ColorOf(TileType type)
        {
            switch (type)
            {
                case TileType.Ground:    return new Color(0.32f, 0.32f, 0.35f);
                case TileType.Buildable: return new Color(0.30f, 0.65f, 0.35f);
                case TileType.Obstacle:  return new Color(0.70f, 0.28f, 0.24f);
                case TileType.Base:      return new Color(0.25f, 0.50f, 0.85f);
                case TileType.Spawn:     return new Color(0.85f, 0.75f, 0.25f);
                default:                 return Color.magenta;
            }
        }
    }
}
