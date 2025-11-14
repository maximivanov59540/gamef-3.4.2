using UnityEngine;

/// <summary>
/// Режим "Улучшение" (из Проекта в Реальное)
/// </summary>
public class State_Upgrading : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly GridSystem _gridSystem;
    private readonly SelectionManager _selectionManager;
    private readonly ResourceManager _resourceManager;
    private readonly RoadManager _roadManager;
    
    // --- Память ---
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    public State_Upgrading(PlayerInputController controller, NotificationManager notificationManager, 
                           BuildingManager buildingManager, GridSystem gridSystem, SelectionManager selectionManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _gridSystem = gridSystem;
        _selectionManager = selectionManager;
        _resourceManager = ResourceManager.Instance;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Улучшение (Инструмент)");
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
                _buildingManager.TryUpgradeBuilding(gridPos);
            }
            else
            {
                // --- СЛУЧАЙ Б: "МАССОВЫЙ" ДРАГ (Рамка) ИЛИ Апгрейд 1 Дороги ---
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

        // 3. "ИСПОЛНЕНИЕ" (Отпускание) - "ДИСПЕТЧЕР"
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
                // СЛУЧАЙ 1: Только Дороги (Логика апгрейда)
                int upgradedCount = 0;
                foreach (var cell in roads)
                {
                    RoadTile tile = _gridSystem.GetRoadTileAt(cell.x, cell.y);
                    if (tile == null || tile.roadData == null || tile.roadData.upgradeTarget == null)
                        continue; // Эту дорогу нельзя улучшить (либо уже мощеная, либо нет 'upgradeTarget')

                    RoadData targetData = tile.roadData.upgradeTarget;

                    if (_resourceManager.CanAfford(targetData.upgradeCost))
                    {
                        _resourceManager.SpendResources(targetData.upgradeCost);
                        _roadManager.UpgradeRoad(cell, targetData);
                        upgradedCount++;
                    }
                    else
                    {
                        _notificationManager.ShowNotification("Недостаточно ресурсов для апгрейда!");
                        break; // Кончились ресурсы, останавливаем цикл
                    }
                }
                if (upgradedCount > 0)
                    _notificationManager.ShowNotification($"Улучшено {upgradedCount} дорог.");
            }
            else if (buildings.Count > 0 && roads.Count == 0)
            {
                // СЛУЧАЙ 2: Только Здания (старая логика)
                _buildingManager.MassUpgrade(buildings);
            }
            else if (buildings.Count > 0 && roads.Count > 0)
            {
                _notificationManager.ShowNotification("Нельзя улучшать дороги и здания вместе!");
            }
            // (Если 0, и там, и там - это ОДИНОЧНЫЙ апгрейд дороги)
            else if (a == b && _gridSystem.GetRoadTileAt(a.x, a.y) != null)
            {
                // (Это был не драг, а клик по 1 дороге)
                RoadTile tile = _gridSystem.GetRoadTileAt(a.x, a.y);
                if (tile != null && tile.roadData != null && tile.roadData.upgradeTarget != null)
                {
                    RoadData targetData = tile.roadData.upgradeTarget;
                    if (_resourceManager.CanAfford(targetData.upgradeCost))
                    {
                        _resourceManager.SpendResources(targetData.upgradeCost);
                        _roadManager.UpgradeRoad(a, targetData);
                        _notificationManager.ShowNotification("Дорога улучшена!");
                    }
                    else
                    {
                        _notificationManager.ShowNotification("Недостаточно ресурсов!");
                    }
                }
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
        // Тут тоже ничего не удаляем в BuildingManager.
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}