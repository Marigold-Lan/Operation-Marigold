#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MapEditorWindow : EditorWindow
{
    private const string BaseFolder = "Assets/Blueprint/Tiles/Base";
    private const string PlaceableFolder = "Assets/Blueprint/Tiles/Placeable";

    private MapRoot _mapRoot;
    private TileBaseType _selectedBase;
    private TilePlaceableType _selectedPlaceable;
    private bool _eraseMode;
    private bool _sceneGuiRegistered;
    private Plane _xzPlane = new Plane(Vector3.up, Vector3.zero);

    private TileBaseType[] _baseTypes;
    private TilePlaceableType[] _placeableTypes;
    private string[] _baseDisplayOptions;
    private string[] _placeableDisplayOptions;
    private int _baseIndex;
    private int _placeableIndex;


    private int _setupWidth = 10;
    private int _setupHeight = 10;
    private int _currentBaseRotation;
    private int _currentPlaceableRotation;

    private int _dragButton = -1;
    private Vector2Int? _lastDragCoord;
    private bool _dragIsErase;
    private bool _dragIsRotate;

    private bool _foldoutGrid = true;
    private bool _foldoutPaint = true;

    [MenuItem(OperationMarigoldPaths.ToolsMap + "/Map Editor")]
    public static void Open()
    {
        GetWindow<MapEditorWindow>("地图编辑器");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        _sceneGuiRegistered = true;
        RefreshBlueprintCache();
        TryEnterMapEditMode();
    }

    private void OnDisable()
    {
        if (_sceneGuiRegistered)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _sceneGuiRegistered = false;
        }
        RestoreSceneView();
    }

    private void TryEnterMapEditMode()
    {
        _mapRoot = Object.FindFirstObjectByType<MapRoot>();
        if (_mapRoot != null)
        {
            EnterMapEditMode();
        }
    }

    private void EnterMapEditMode()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;

        sv.showGrid = false;

        var center = _mapRoot != null ? _mapRoot.transform.position : Vector3.zero;
        var cs = _mapRoot != null ? _mapRoot.cellSize : 1f;
        var w = _mapRoot != null ? _mapRoot.gridWidth : 10;
        var h = _mapRoot != null ? _mapRoot.gridHeight : 10;

        sv.orthographic = true;
        sv.pivot = center;
        sv.rotation = Quaternion.Euler(90f, 0f, 0f);
        var boardExtent = Mathf.Max(w * Mathf.Max(cs, 1f), h * Mathf.Max(cs, 1f));
        sv.size = Mathf.Max(boardExtent * 0.6f, 5f);
        sv.Repaint();
    }

    private void RestoreSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.showGrid = true;
            sv.orthographic = false;
            sv.Repaint();
        }
    }

    private void RefreshBlueprintCache()
    {
        _baseTypes = LoadAllAssets<TileBaseType>(BaseFolder) ?? new TileBaseType[0];
        _placeableTypes = LoadAllAssets<TilePlaceableType>(PlaceableFolder) ?? new TilePlaceableType[0];
        _baseDisplayOptions = _baseTypes.Select(b => GetDisplayLabel(b.displayName, b.id, b.name)).ToArray();
        _placeableDisplayOptions = new[] { "无" }.Concat(_placeableTypes.Select(p => GetDisplayLabel(p.displayName, p.id, p.name))).ToArray();
        SyncSelectionToIndex();
    }

    private static string GetDisplayLabel(string displayName, string id, string assetName)
    {
        if (!string.IsNullOrEmpty(displayName)) return displayName;
        if (!string.IsNullOrEmpty(id)) return id;
        return string.IsNullOrEmpty(assetName) ? "未命名" : assetName;
    }

    private static T[] LoadAllAssets<T>(string folder) where T : Object
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        var list = new List<T>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) list.Add(asset);
        }
        return list.ToArray();
    }

    private void SyncSelectionToIndex()
    {
        _baseIndex = _selectedBase != null && _baseTypes != null && _baseTypes.Length > 0
            ? System.Array.IndexOf(_baseTypes, _selectedBase) : -1;
        if (_baseIndex < 0 && _baseTypes != null && _baseTypes.Length > 0) _baseIndex = 0;
        if (_baseIndex < 0) _baseIndex = 0;
        if (_baseTypes != null && _baseIndex >= _baseTypes.Length) _baseIndex = Mathf.Max(0, _baseTypes.Length - 1);

        _placeableIndex = _selectedPlaceable != null && _placeableTypes != null && _placeableTypes.Length > 0
            ? System.Array.IndexOf(_placeableTypes, _selectedPlaceable) + 1 : 0;
        if (_placeableIndex < 0) _placeableIndex = 0;
        var maxPlaceableIndex = (_placeableTypes != null ? _placeableTypes.Length : 0);
        if (_placeableIndex > maxPlaceableIndex) _placeableIndex = maxPlaceableIndex;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(2);
        var prevMapRoot = _mapRoot;
        _mapRoot = (MapRoot)EditorGUILayout.ObjectField("Map Root", _mapRoot, typeof(MapRoot), true);
        if (_mapRoot != null && _mapRoot != prevMapRoot)
            EnterMapEditMode();

        if (_mapRoot == null)
        {
            DrawCreateMapSection();
            return;
        }

        EditorGUILayout.Space(6);

        DrawGridSection();
        DrawPaintSection();
        DrawViewToolbar();
    }

    private void DrawCreateMapSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("新建地图", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _setupWidth = Mathf.Max(1, EditorGUILayout.IntField("宽", _setupWidth));
        _setupHeight = Mathf.Max(1, EditorGUILayout.IntField("高", _setupHeight));
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("创建并进入编辑"))
        {
            CreateMapRoot(_setupWidth, _setupHeight);
            EnterMapEditMode();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawGridSection()
    {
        _foldoutGrid = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGrid, "网格尺寸");
        if (_foldoutGrid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var w = _mapRoot.gridWidth;
            var h = _mapRoot.gridHeight;
            EditorGUILayout.LabelField($"{w} × {h}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("列", GUILayout.Width(16));
            if (GUILayout.Button("+右")) AddColumnRight();
            if (GUILayout.Button("+左")) AddColumnLeft();
            if (GUILayout.Button("-右")) RemoveColumnRight();
            if (GUILayout.Button("-左")) RemoveColumnLeft();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("行", GUILayout.Width(16));
            if (GUILayout.Button("+上")) AddRowTop();
            if (GUILayout.Button("+下")) AddRowBottom();
            if (GUILayout.Button("-上")) RemoveRowTop();
            if (GUILayout.Button("-下")) RemoveRowBottom();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPaintSection()
    {
        _foldoutPaint = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPaint, "绘制");
        if (_foldoutPaint)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("刷新蓝图", GUILayout.ExpandWidth(false)))
                RefreshBlueprintCache();

            if (_baseTypes.Length == 0)
            {
                EditorGUILayout.HelpBox($"未找到地基蓝图 ({BaseFolder})", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("地基", GUILayout.Width(36));
                var newBaseIndex = EditorGUILayout.Popup(_baseIndex, _baseDisplayOptions);
                EditorGUILayout.LabelField("旋转", GUILayout.Width(28));
                _currentBaseRotation = EditorGUILayout.IntPopup(_currentBaseRotation, new[] { "0°", "90°", "180°", "270°" }, new[] { 0, 90, 180, 270 }, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

                if (newBaseIndex >= 0 && newBaseIndex < _baseTypes.Length)
                {
                    if (newBaseIndex != _baseIndex) { _baseIndex = newBaseIndex; _selectedBase = _baseTypes[_baseIndex]; AutoSyncCellSizeFromBase(); }
                    else if (_selectedBase != _baseTypes[_baseIndex]) { _selectedBase = _baseTypes[_baseIndex]; AutoSyncCellSizeFromBase(); }
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("放置物", GUILayout.Width(36));
            if (_placeableTypes.Length == 0)
            {
                EditorGUILayout.Popup(0, new[] { "无" });
                _selectedPlaceable = null;
            }
            else
            {
                var newPlaceableIndex = EditorGUILayout.Popup(_placeableIndex, _placeableDisplayOptions);
                EditorGUILayout.LabelField("旋转", GUILayout.Width(28));
                _currentPlaceableRotation = EditorGUILayout.IntPopup(_currentPlaceableRotation, new[] { "0°", "90°", "180°", "270°" }, new[] { 0, 90, 180, 270 }, GUILayout.Width(50));

                if (newPlaceableIndex >= 0 && newPlaceableIndex < _placeableDisplayOptions.Length)
                {
                    _placeableIndex = newPlaceableIndex;
                    _selectedPlaceable = _placeableIndex > 0 && _placeableIndex - 1 < _placeableTypes.Length ? _placeableTypes[_placeableIndex - 1] : null;
                }
            }
            EditorGUILayout.EndHorizontal();

            _eraseMode = EditorGUILayout.Toggle("橡皮擦模式", _eraseMode);
            EditorGUILayout.LabelField(_eraseMode ? "点击/拖动删除格子" : "点击/拖动放置；Shift+点击旋转", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawViewToolbar()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("聚焦俯视图")) EnterMapEditMode();
        if (GUILayout.Button("刷新视图")) SceneView.RepaintAll();
        EditorGUILayout.EndHorizontal();
    }

    private void CreateMapRoot(int width, int height)
    {
        var go = new GameObject("Map");
        _mapRoot = go.AddComponent<MapRoot>();
        _mapRoot.gridWidth = Mathf.Max(1, width);
        _mapRoot.gridHeight = Mathf.Max(1, height);
        Undo.RegisterCreatedObjectUndo(go, "Create Map Root");
        Selection.activeGameObject = go;
    }

    private void AddColumnRight()
    {
        if (_mapRoot == null) return;
        Undo.RecordObject(_mapRoot, "Add Column Right");
        _mapRoot.gridWidth += 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void AddColumnLeft()
    {
        if (_mapRoot == null) return;
        ShiftAllCellsCoord(1, 0);
        Undo.RecordObject(_mapRoot, "Add Column Left");
        _mapRoot.gridWidth += 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void AddRowTop()
    {
        if (_mapRoot == null) return;
        Undo.RecordObject(_mapRoot, "Add Row Top");
        _mapRoot.gridHeight += 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void AddRowBottom()
    {
        if (_mapRoot == null) return;
        ShiftAllCellsCoord(0, 1);
        Undo.RecordObject(_mapRoot, "Add Row Bottom");
        _mapRoot.gridHeight += 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void RemoveColumnRight()
    {
        if (_mapRoot == null || _mapRoot.gridWidth <= 1) return;
        RemoveCellsWhere(c => c.gridCoord.x >= _mapRoot.gridWidth - 1);
        Undo.RecordObject(_mapRoot, "Remove Column Right");
        _mapRoot.gridWidth -= 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void RemoveColumnLeft()
    {
        if (_mapRoot == null || _mapRoot.gridWidth <= 1) return;
        RemoveCellsWhere(c => c.gridCoord.x <= 0);
        ShiftAllCellsCoord(-1, 0);
        Undo.RecordObject(_mapRoot, "Remove Column Left");
        _mapRoot.gridWidth -= 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void RemoveRowTop()
    {
        if (_mapRoot == null || _mapRoot.gridHeight <= 1) return;
        RemoveCellsWhere(c => c.gridCoord.y >= _mapRoot.gridHeight - 1);
        Undo.RecordObject(_mapRoot, "Remove Row Top");
        _mapRoot.gridHeight -= 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void RemoveRowBottom()
    {
        if (_mapRoot == null || _mapRoot.gridHeight <= 1) return;
        RemoveCellsWhere(c => c.gridCoord.y <= 0);
        ShiftAllCellsCoord(0, -1);
        Undo.RecordObject(_mapRoot, "Remove Row Bottom");
        _mapRoot.gridHeight -= 1;
        RepositionAllCells();
        MarkMapDirty();
    }

    private void ShiftAllCellsCoord(int dx, int dy)
    {
        if (_mapRoot == null || (dx == 0 && dy == 0)) return;
        var cells = new List<(Cell cell, Vector2Int oldCoord)>();
        foreach (Transform t in _mapRoot.transform)
        {
            var cell = t.GetComponent<Cell>();
            if (cell != null) cells.Add((cell, cell.gridCoord));
        }
        foreach (var (cell, _) in cells)
        {
            Undo.RecordObject(cell, "Shift Cell Coord");
            cell.gridCoord = new Vector2Int(cell.gridCoord.x + dx, cell.gridCoord.y + dy);
            EditorUtility.SetDirty(cell);
        }
    }

    private void MarkMapDirty()
    {
        EditorUtility.SetDirty(_mapRoot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(_mapRoot);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_mapRoot.gameObject.scene);
        EnterMapEditMode();
    }

    private void RemoveCellsWhere(System.Func<Cell, bool> predicate)
    {
        var toRemove = new List<Cell>();
        foreach (Transform t in _mapRoot.transform)
        {
            var cell = t.GetComponent<Cell>();
            if (cell != null && predicate(cell)) toRemove.Add(cell);
        }
        foreach (var cell in toRemove)
            Undo.DestroyObjectImmediate(cell.gameObject);
    }

    private void RepositionAllCells()
    {
        if (_mapRoot == null) return;
        foreach (Transform t in _mapRoot.transform)
        {
            var cell = t.GetComponent<Cell>();
            if (cell == null) continue;
            if (!_mapRoot.IsInBounds(cell.gridCoord)) continue;
            Undo.RecordObject(t, "Reposition Cell");
            t.position = _mapRoot.GridToWorld(cell.gridCoord);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_mapRoot == null) return;

        var e = Event.current;
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(0);
            return;
        }

        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            var gridCoord = PickGridCoord(sceneView);
            if (gridCoord.HasValue && _mapRoot.IsInBounds(gridCoord.Value))
            {
                e.Use();
                _dragIsErase = _eraseMode || e.button == 1;
                _dragIsRotate = !_dragIsErase && e.shift;
                PerformCellAction(gridCoord.Value);
                _dragButton = e.button;
                _lastDragCoord = gridCoord.Value;
            }
        }
        else if (e.type == EventType.MouseDrag && _dragButton >= 0)
        {
            var gridCoord = PickGridCoord(sceneView);
            if (gridCoord.HasValue && _mapRoot.IsInBounds(gridCoord.Value) && gridCoord.Value != _lastDragCoord)
            {
                e.Use();
                PerformCellAction(gridCoord.Value);
                _lastDragCoord = gridCoord.Value;
                sceneView.Repaint();
            }
        }
        else if (e.type == EventType.MouseUp || e.type == EventType.Ignore)
        {
            _dragButton = -1;
            _lastDragCoord = null;
        }

        DrawGrid(sceneView);
    }

    private Vector2Int? PickGridCoord(SceneView sceneView)
    {
        var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        var mapY = _mapRoot.GridOrigin.y;
        _xzPlane = new Plane(Vector3.up, new Vector3(0, mapY, 0));

        if (!_xzPlane.Raycast(ray, out float enter)) return null;

        var hit = ray.GetPoint(enter);
        return _mapRoot.WorldToGrid(hit);
    }

    private void DrawGrid(SceneView sceneView)
    {
        var mapY = _mapRoot.GridOrigin.y;
        var origin = _mapRoot.GridOrigin;
        var cs = _mapRoot.cellSize;
        var w = _mapRoot.gridWidth;
        var h = _mapRoot.gridHeight;

        Handles.color = new Color(1f, 1f, 1f, 0.5f);

        for (int x = 0; x <= w; x++)
        {
            var a = origin + new Vector3(x * cs, 0, 0);
            var b = origin + new Vector3(x * cs, 0, h * cs);
            Handles.DrawLine(a, b);
        }

        for (int z = 0; z <= h; z++)
        {
            var a = origin + new Vector3(0, 0, z * cs);
            var b = origin + new Vector3(w * cs, 0, z * cs);
            Handles.DrawLine(a, b);
        }
    }

    private Cell GetCellAt(Vector2Int gridCoord)
    {
        foreach (Transform t in _mapRoot.transform)
        {
            var cell = t.GetComponent<Cell>();
            if (cell != null && cell.gridCoord == gridCoord)
                return cell;
        }

        return null;
    }

    private void PlaceOrUpdateCellAt(Vector2Int gridCoord)
    {
        if (_selectedBase == null)
            return;

        var existing = GetCellAt(gridCoord);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Update Cell");
            existing.SetBaseRotation(_currentBaseRotation);
            existing.SetPlaceableRotation(_currentPlaceableRotation);
            existing.SetBase(_selectedBase);
            if (_selectedPlaceable != null && existing.CanAcceptPlaceable(_selectedPlaceable))
                existing.SetPlaceable(_selectedPlaceable);
            else
                existing.ClearPlaceable();
            EnsureCellView(existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            var go = new GameObject($"Cell_{gridCoord.x}_{gridCoord.y}");
            Undo.RegisterCreatedObjectUndo(go, "Create Cell");
            Undo.SetTransformParent(go.transform, _mapRoot.transform, "Create Cell");

            go.transform.position = _mapRoot.GridToWorld(gridCoord);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var cell = go.AddComponent<Cell>();
            Undo.RecordObject(cell, "Create Cell");
            EnsureCellView(cell);
            cell.gridCoord = gridCoord;
            cell.SetBaseRotation(_currentBaseRotation);
            cell.SetPlaceableRotation(_currentPlaceableRotation);
            cell.SetBase(_selectedBase);
            if (_selectedPlaceable != null && cell.CanAcceptPlaceable(_selectedPlaceable))
                cell.SetPlaceable(_selectedPlaceable);
            EditorUtility.SetDirty(cell);
        }
    }

    private static void EnsureCellView(Cell cell)
    {
        if (cell == null || cell.GetComponent<CellView>() != null) return;
        Undo.AddComponent<CellView>(cell.gameObject);
    }

    private void PerformCellAction(Vector2Int gridCoord)
    {
        if (_dragIsErase)
            RemoveCellAt(gridCoord);
        else if (_dragIsRotate)
            RotateCellAt(gridCoord);
        else
            PlaceOrUpdateCellAt(gridCoord);
    }

    private void RotateCellAt(Vector2Int gridCoord)
    {
        var cell = GetCellAt(gridCoord);
        if (cell == null) return;
        Undo.RecordObject(cell, "Rotate Cell");
        cell.SetBaseRotation((cell.BaseRotationDegrees + 90) % 360);
        cell.SetPlaceableRotation((cell.PlaceableRotationDegrees + 90) % 360);
        EditorUtility.SetDirty(cell);
    }

    private void RemoveCellAt(Vector2Int gridCoord)
    {
        var cell = GetCellAt(gridCoord);
        if (cell != null)
        {
            Undo.DestroyObjectImmediate(cell.gameObject);
        }
    }

    private void AutoSyncCellSizeFromBase()
    {
        if (_mapRoot == null || _selectedBase == null) return;

        var size = _selectedBase.cellSize;
        if (size <= 0 || _selectedBase.prefabNativeSize <= 0)
        {
            size = DetectXZSizeFromPrefab(_selectedBase.prefab);
            if (size <= 0)
                return;
            Undo.RecordObject(_selectedBase, "Auto Detect Cell Size");
            _selectedBase.cellSize = size;
            _selectedBase.prefabNativeSize = size;
            EditorUtility.SetDirty(_selectedBase);
        }

        Undo.RecordObject(_mapRoot, "Sync Cell Size");
        _mapRoot.cellSize = size;
        EditorUtility.SetDirty(_mapRoot);
    }

    private static float DetectXZSizeFromPrefab(GameObject prefab)
    {
        if (prefab == null) return 0f;

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (temp == null) return 0f;

        var rends = temp.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            DestroyImmediate(temp);
            return 0f;
        }

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        DestroyImmediate(temp);
        return Mathf.Max(b.size.x, b.size.z, 0.001f);
    }
}
#endif
