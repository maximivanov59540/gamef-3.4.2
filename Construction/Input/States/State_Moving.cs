using UnityEngine;

/// <summary>
/// Режим "Перемещение" (Одиночное и Массовое)
/// </summary>
public class State_Moving : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly SelectionManager _selectionManager;
    private readonly GroupOperationHandler _groupOperationHandler;
    private readonly GridSystem _gridSystem;

    // --- Память ---
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    public State_Moving(PlayerInputController controller, NotificationManager notificationManager,
                        BuildingManager buildingManager, SelectionManager selectionManager, GroupOperationHandler groupOperationHandler, GridSystem gridSystem)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _selectionManager = selectionManager;
        _groupOperationHandler = groupOperationHandler;
        _gridSystem = gridSystem;
    }

    public void OnEnter()
    {
        _buildingManager?.ShowGrid(false); // <-- ДОБАВЬ ЭТУ СТРОКУ
    }

    public void OnUpdate()
    {
        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();
        
        // --- ЧАСТЬ 1: Мы УЖЕ "держим" 1 здание ---
        if (_buildingManager.IsHoldingBuilding())
        {
            // ... (эта часть остается без изменений) ...
            _buildingManager.UpdateGhostPosition(gridPos, worldPos);
            if (Input.GetKeyDown(KeyCode.R)) _buildingManager.RotateGhost();
            if (Input.GetMouseButtonDown(0) && !isOverUI) _buildingManager.TryPlaceBuilding(gridPos);
            if (Input.GetMouseButtonDown(1)) _buildingManager.CancelAllModes(); 
            return; 
        }

        // --- ЧАСТЬ 2: Мы "свободны" и "ищем" цель (Логика "Диспетчера") ---

        // 1. "НАЧАЛО" (Клик)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

            if (id != null)
            {
                // --- СЛУЧАЙ А: "ПОШТУЧНЫЙ" КЛИК (Здание) ---
                _buildingManager.TryPickUpBuilding(gridPos);
            }
            else if (gridPos.x != -1)
            {
                // --- СЛУЧАЙ Б: "МАССОВЫЙ" ДРАГ (Рамка) ---
                _isDragging = true;
                _dragStartPosition = gridPos;
                _selectionManager.StartSelection(worldPos);
            }
        }

        // 2. "ПЛАНИРОВАНИЕ" (Драг)
        if (Input.GetMouseButton(0) && _isDragging)
        {
            _selectionManager.UpdateSelection(worldPos);
        }

        // 3. "ИСПОЛНЕНИЕ" (Отпускание Драга) - "ДИСПЕТЧЕР"
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;
            // (Сначала прячем рамку)
            _selectionManager.HideSelectionVisuals(); 
            
            // Получаем границы рамки (как в State_Deleting)
            Vector2Int a = _dragStartPosition;
            Vector2Int b = gridPos;
            if (b.x == -1) b = a; // Защита, если отпустили за пределами

            // Ищем ОБА типа объектов
            var buildings = _gridSystem.GetBuildingsInRect(a, b);
            var roads = _gridSystem.GetRoadsInRect(a, b);

            // --- ЛОГИКА "ДИСПЕТЧЕРА" ---
            if (roads.Count > 0 && buildings.Count == 0)
            {
                // СЛУЧАЙ 1: Только Дороги
                RoadOperationHandler.Instance.StartOperation(roads, isMove: true);
            }
            else if (buildings.Count > 0 && roads.Count == 0)
            {
                // СЛУЧАЙ 2: Только Здания (старая логика)
                _groupOperationHandler.StartMassMove(buildings);
            }
            else if (buildings.Count > 0 && roads.Count > 0)
            {
                // СЛУЧАЙ 3: Смешанный выбор
                _notificationManager.ShowNotification("Нельзя перемещать дороги и здания вместе!");
            }
            // (Если roads.Count == 0 && buildings.Count == 0 - ничего не делаем)
        }

        // 4. "Отмена" (ПКМ) - ВЫХОД из режима
        if (Input.GetMouseButtonDown(1) && !_isDragging)
        {
            _controller.SetMode(InputMode.None);
        }
    }

    public void OnExit()
    {
        // Не чистим BuildingManager тут — иначе снесём только что созданный призрак.
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}