using System.Collections.Generic;
using UnityEngine;

public class RoadBuildHandler : MonoBehaviour
{
    [Header("Ссылки на 'Инструменты'")]
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private RoadManager roadManager;

    [Header("Данные")]
    [Tooltip("Какой 'чертеж' дороги использовать при строительстве по умолчанию (RD_SandRoad)")]
    [SerializeField] private RoadData defaultRoadData;

    [Header("Визуализация (Призраки)")]
    [SerializeField] private GameObject roadGhostPrefab;
    [SerializeField] private Transform roadGhostsRoot;
 

    private List<GameObject> _ghostPool = new List<GameObject>();
    private int _ghostPoolIndex = 0;
    private RoadPathfinder _pathfinder;
    private List<Vector2Int> _currentRoadCells = new List<Vector2Int>();
    private List<bool> _currentBuildValidity = new List<bool>();

    void Awake()
    {
        if (gridSystem == null) gridSystem = FindFirstObjectByType<GridSystem>();
        if (_pathfinder == null) _pathfinder = new RoadPathfinder(gridSystem);
        if (roadManager == null) roadManager = RoadManager.Instance;

        if (defaultRoadData == null)
            Debug.LogError("RoadBuildHandler: 'Default Road Data' (RD_SandRoad) не назначен!", this);

        if (roadGhostPrefab == null)
            Debug.LogError("RoadBuildHandler: 'Road Ghost Prefab' не назначен!", this);
        if (roadGhostsRoot == null)
        {
            var go = GameObject.Find("RoadGhostsRoot");
            if (go == null) go = new GameObject("RoadGhostsRoot");
            roadGhostsRoot = go.transform;
            roadGhostsRoot.SetParent(null);         // корень сцены
            roadGhostsRoot.position = Vector3.zero; // без смещений
        }
    }

    public void StartRoadPreview()
    {
        ClearRoadPreview(); 
    }

    public void UpdateRoadPreview(Vector2Int startCell, Vector2Int endCell)
    {
        _ghostPoolIndex = 0;
        _currentRoadCells.Clear();
        _currentBuildValidity.Clear();
        HideUnusedGhosts();

        var path = _pathfinder.FindPath(startCell, endCell);

        // Путь не найден — просто ничего не рисуем
        if (path == null || path.Count == 0)
            return;

        // Заполняем клетки пути и рисуем призраки
        foreach (var cell in path)
        {
            _currentRoadCells.Add(cell);
            ProcessGhostPreview(cell);
        }

        HideUnusedGhosts();
    }
    public void ExecuteRoadBuild()
    {
        int count = Mathf.Min(_currentRoadCells.Count, _currentBuildValidity.Count);
        for (int i = 0; i < count; i++)
        {
            if (_currentBuildValidity[i])
            {
                // --- ИЗМЕНЕНИЕ: Передаем 'defaultRoadData' ---
                roadManager.PlaceRoad(_currentRoadCells[i], defaultRoadData);
            }
        }
        ClearRoadPreview();
    }


    public void ClearRoadPreview()
    {
        _ghostPoolIndex = 0;
        HideUnusedGhosts();
        _currentRoadCells.Clear();
        _currentBuildValidity.Clear();
    }
    public bool HasPreview()
    {
        return _currentRoadCells.Count > 0 || _ghostPoolIndex > 0;
    }


    private void ProcessGhostPreview(Vector2Int currentCell)
    {
        GameObject ghost = GetGhostFromPool();
        ghost.transform.rotation = Quaternion.Euler(90, 0, 0);

        Vector3 worldPos = gridSystem.GetWorldPosition(currentCell.x, currentCell.y);

        // тот же сдвиг, что и у реальной дороги (центр в середине клетки)
        float offset = gridSystem.GetCellSize() / 2f;
        worldPos.x += offset;
        worldPos.z += offset;
        worldPos.y += 0.01f; // чуть приподняли «призрак»
        ghost.transform.position = worldPos;

        // --- ПРОВЕРКИ ДОПУСТИМОСТИ ---
        bool blockedByBuilding = gridSystem.GetBuildingIdentityAt(currentCell.x, currentCell.y) != null;
        bool alreadyHasRoad = gridSystem.GetRoadTileAt(currentCell.x, currentCell.y) != null;
        bool canBuild = !blockedByBuilding && !alreadyHasRoad;

        // Запоминаем маршрут и валидность
        _currentBuildValidity.Add(canBuild);

        // --- ВИЗУАЛ: зелёный = можно, красный = нельзя ---
        if (ghost.TryGetComponent<Renderer>(out var r) && r.material != null)
        {
            Color c = canBuild ? new Color(0f, 1f, 0f, 0.55f) : new Color(1f, 0f, 0f, 0.55f);
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
            else r.material.color = c;
        }

        ghost.SetActive(true);
    }


    private GameObject GetGhostFromPool()
    {
        GameObject ghost;
        if (_ghostPoolIndex < _ghostPool.Count)
        {
            ghost = _ghostPool[_ghostPoolIndex];
        }
        else
        {
            ghost = Instantiate(roadGhostPrefab, roadGhostsRoot);
            var col = ghost.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            _ghostPool.Add(ghost);
        }

        ghost.SetActive(true);
        _ghostPoolIndex++;
        return ghost;
    }

    private void HideUnusedGhosts()
    {
        for (int i = _ghostPoolIndex; i < _ghostPool.Count; i++)
        {
            _ghostPool[i].SetActive(false);
        }
    }
}