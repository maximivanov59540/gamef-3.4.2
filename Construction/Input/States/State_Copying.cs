using UnityEngine;

public class State_Copying : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly GridSystem _gridSystem; // <-- ИСПРАВЛЕНИЕ 1
    private readonly SelectionManager _selectionManager;
    private readonly GroupOperationHandler _groupOperationHandler;
    
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    // --- ИСПРАВЛЕНИЕ 2 ---
    public State_Copying(PlayerInputController controller, NotificationManager notificationManager, 
                         BuildingManager buildingManager, GridSystem gridSystem, 
                         SelectionManager selectionManager, GroupOperationHandler groupOperationHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _gridSystem = gridSystem; // <-- И присвоили
        _selectionManager = selectionManager;
        _groupOperationHandler = groupOperationHandler;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Копирование (Пипетка)");
        _isDragging = false;
    }

    public void OnUpdate()
    {
        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();

        // 1. "НАЧАЛО" (Клик)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);
            if (id != null)
            {
                // --- СЛУЧАЙ А: "ПОШТУЧНЫЙ" КЛИК (Здание) ---
                _buildingManager.TryCopyBuilding(gridPos);
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
            _selectionManager.HideSelectionVisuals(); 
            
            Vector2Int a = _dragStartPosition;
            Vector2Int b = gridPos;
            if (b.x == -1) b = a;

            var buildings = _gridSystem.GetBuildingsInRect(a, b);
            var roads = _gridSystem.GetRoadsInRect(a, b);

            // --- ЛОГИКА "ДИСПЕТЧЕРА" ---
            if (roads.Count > 0 && buildings.Count == 0)
            {
                // СЛУЧАЙ 1: Только Дороги
                RoadOperationHandler.Instance.StartOperation(roads, isMove: false); // isMove = false
            }
            else if (buildings.Count > 0 && roads.Count == 0)
            {
                // СЛУЧАЙ 2: Только Здания (старая логика)
                _groupOperationHandler.StartMassCopy(buildings);
            }
            else if (buildings.Count > 0 && roads.Count > 0)
            {
                _notificationManager.ShowNotification("Нельзя копировать дороги и здания вместе!");
            }
        }

        // 4. "Отмена" (ПКМ) - ВЫХОД из режима
        if (Input.GetMouseButtonDown(1) && !_isDragging)
        {
            _controller.SetMode(InputMode.None);
        }
    }

    public void OnExit()
    {
        // ВАЖНО: не зовём CancelAllModes(), иначе убьём призрака,
        // которого только что создаст EnterBuildMode().
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}