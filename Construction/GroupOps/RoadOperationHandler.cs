// --- RoadOperationHandler.cs ---
using System.Collections.Generic;
using UnityEngine;

public class RoadOperationHandler : MonoBehaviour
{
    public static RoadOperationHandler Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private GridSystem _gridSystem;
    [SerializeField] private PlayerInputController _inputController;
    [SerializeField] private RoadManager _roadManager;
    [SerializeField] private NotificationManager _notificationManager;
    [SerializeField] private Transform _ghostsRoot; // Нужен 'корень' для призраков

    // Пул призраков
    private readonly List<GameObject> _ghostPool = new();
    private int _ghostPoolIndex = 0;

    // "Слепок" операции
    private readonly List<Vector2Int> _relativeOffsets = new();
    private readonly List<RoadData> _roadTypes = new();
    private Vector2Int _anchorPos; // Оригинальная позиция якоря
    private bool _isMoving = false;
    private bool _canPlace = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        if (_gridSystem == null) _gridSystem = FindFirstObjectByType<GridSystem>();
        if (_inputController == null) _inputController = FindFirstObjectByType<PlayerInputController>();
        if (_roadManager == null) _roadManager = RoadManager.Instance;
        if (_notificationManager == null) _notificationManager = FindFirstObjectByType<NotificationManager>();

        if (_ghostsRoot == null)
            _ghostsRoot = new GameObject("RoadGhostsRoot_Mass").transform;
    }

    /// <summary>
    /// Шаг 1: "Поднять" дороги и переключить режим.
    /// </summary>
    public void StartOperation(List<Vector2Int> roadCells, bool isMove)
    {
        if (roadCells == null || roadCells.Count == 0) return;

        QuietCancel(); // Очищаем предыдущую операцию, если была

        _isMoving = isMove;

        // 1. Найти якорь (левый нижний)
        _anchorPos = roadCells[0];
        int minPosSum = _anchorPos.x + _anchorPos.y;
        foreach (var cell in roadCells)
        {
            int sum = cell.x + cell.y;
            if (sum < minPosSum)
            {
                minPosSum = sum;
                _anchorPos = cell;
            }
        }

        // 2. Создать "слепок" и (опционально) удалить оригинал
        foreach (var cell in roadCells)
        {
            RoadTile tile = _gridSystem.GetRoadTileAt(cell.x, cell.y);
            if (tile == null || tile.roadData == null) continue;

            _relativeOffsets.Add(cell - _anchorPos);
            _roadTypes.Add(tile.roadData);

            if (_isMoving)
            {
                _roadManager.RemoveRoad(cell);
            }
        }
        
        _inputController.SetMode(InputMode.RoadOperation);
    }

    /// <summary>
    /// Шаг 2: Обновление призраков (вызывается из State_RoadOperation).
    /// </summary>
    public void UpdatePreview(Vector2Int mouseGridPos)
    {
        _ghostPoolIndex = 0;
        _canPlace = true;
        if (mouseGridPos.x == -1)
        {
            HideGhosts();
            return;
        }

        for (int i = 0; i < _relativeOffsets.Count; i++)
        {
            Vector2Int targetCell = mouseGridPos + _relativeOffsets[i];
            RoadData data = _roadTypes[i];
            
            GameObject ghost = GetGhostFromPool(data);
            
            // Позиционирование (как в RoadManager)
            Vector3 worldPos = _gridSystem.GetWorldPosition(targetCell.x, targetCell.y);
            float offset = _gridSystem.GetCellSize() / 2f;
            worldPos.x += offset; worldPos.z += offset; worldPos.y += 0.01f;
            ghost.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(90, 0, 0));

            // Проверка и покраска
            bool canBuild = !_gridSystem.IsCellOccupied(targetCell.x, targetCell.y);
            if (!canBuild) _canPlace = false;
            
            // (Используем BuildingVisuals, если он есть на префабе призрака,
            // или просто меняем материал)
            var visuals = ghost.GetComponent<BuildingVisuals>();
            if (visuals)
            {
                visuals.SetState(VisualState.Ghost, canBuild);
            }
            else if (ghost.TryGetComponent<Renderer>(out var r))
            {
                r.material.color = canBuild ? Color.green : Color.red;
            }
        }
        HideUnusedGhosts();
    }

    /// <summary>
    /// Шаг 3: Построить дороги (вызывается из State_RoadOperation).
    /// </summary>
    public void ExecutePlace()
    {
        Vector2Int mouseGridPos = GridSystem.MouseGridPosition;
        if (mouseGridPos.x == -1) return;

        if (!_canPlace)
        {
            _notificationManager?.ShowNotification("Место занято!");
            return;
        }

        for (int i = 0; i < _relativeOffsets.Count; i++)
        {
            Vector2Int targetCell = mouseGridPos + _relativeOffsets[i];
            RoadData data = _roadTypes[i];
            _roadManager.PlaceRoad(targetCell, data);
        }

        // Если НЕ держим Shift (штамповка), выходим из режима
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            // Мы успешно *построили*, так что _isMoving = false (не надо восстанавливать)
            _isMoving = false; 
            CancelAndExitMode();
        }
        else
        {
            // Если штампуем, мы "сбрасываем" флаг _isMoving,
            // т.к. оригинал (если он был) уже 'потрачен' на первую копию.
            _isMoving = false; 
        }
    }
    
    /// <summary>
    /// Вызывается из State_RoadOperation по ПКМ.
    /// Восстанавливает дороги (если перемещали) и выходит в None.
    /// </summary>
    public void CancelAndExitMode()
    {
        QuietCancel();
        _inputController.SetMode(InputMode.None);
    }
    
    /// <summary>
    /// "Тихая" отмена. Вызывается из OnExit стейта.
    /// </summary>
    public void QuietCancel()
    {
        // Если мы "отменяем" "перемещение", надо восстановить дороги
        if (_isMoving)
        {
            for (int i = 0; i < _relativeOffsets.Count; i++)
            {
                Vector2Int originalCell = _anchorPos + _relativeOffsets[i];
                RoadData data = _roadTypes[i];
                _roadManager.PlaceRoad(originalCell, data);
            }
        }

        HideGhosts();
        _relativeOffsets.Clear();
        _roadTypes.Clear();
        _isMoving = false;
        _ghostPoolIndex = 0;
    }

    /// <summary>
    /// Просто прячет призраки (для OnExit).
    /// </summary>
    public void HideGhosts()
    {
        HideUnusedGhosts();
    }
    
    // --- (Пул призраков - похож на GroupOperationHandler) ---
    private GameObject GetGhostFromPool(RoadData data)
    {
        GameObject ghost;
        if (_ghostPoolIndex < _ghostPool.Count)
        {
            ghost = _ghostPool[_ghostPoolIndex];
        }
        else
        {
            ghost = Instantiate(data.roadPrefab, _ghostsRoot);
            ghost.layer = LayerMask.NameToLayer("Ghost");
            foreach (var col in ghost.GetComponentsInChildren<Collider>())
                col.enabled = false; // Призракам не нужны коллайдеры
            _ghostPool.Add(ghost);
        }
        ghost.SetActive(true);
        _ghostPoolIndex++;
        return ghost;
    }
    private void HideUnusedGhosts()
    {
        for (int i = _ghostPoolIndex; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }
}