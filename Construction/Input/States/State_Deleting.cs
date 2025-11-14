using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Режим "Уничтожение".
/// - Клик по зданию: сразу удаляем здание.
/// - Клик/прямоугольник, начатые на дороге: удаляем только дороги; одиночный клик по дороге = удалить одну плитку.
/// - Прямоугольник, начатый НЕ на дороге: удаляем и здания, и дороги в области (с подтверждением).
/// - ПКМ / Esc: выход из режима (рамка скрывается).
/// </summary>
public class State_Deleting : IInputState // <-- если у тебя интерфейс IInputState, замени на IInputState
{
    // --- Сервисы ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly GridSystem _gridSystem;
    private readonly UIManager _uiManager;
    private readonly SelectionManager _selectionManager;
    private readonly RoadManager _roadManager;

    // --- Локальное состояние ---
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;
    private bool _rectDeleteRoadsOnly = false; // true если прямоугольник начат на дороге

    // --- Ожидание подтверждения ---
    private bool _awaitingConfirm = false;
    private int _pendMinX, _pendMaxX, _pendMinZ, _pendMaxZ;
    private int _pendRoads, _pendBuildings;

    public State_Deleting(
        PlayerInputController controller,
        NotificationManager notificationManager,
        BuildingManager buildingManager,
        GridSystem gridSystem,
        UIManager uiManager,
        SelectionManager selectionManager,
        RoadManager roadManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _gridSystem = gridSystem;
        _uiManager = uiManager;
        _selectionManager = selectionManager;
        _roadManager = roadManager;
    }

    public void OnEnter()
    {
        _isDragging = false;
        _awaitingConfirm = false;
        _selectionManager.HideSelectionVisuals();
        _notificationManager.ShowNotification("Режим: Уничтожение");
    }

    public void OnUpdate()
    {
        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();

        // --- Фаза подтверждения массового удаления ---
        if (_awaitingConfirm)
        {
            if (Input.GetMouseButtonDown(0))
            {
                var uniqBuildings = new HashSet<BuildingIdentity>();
                for (int x = _pendMinX; x <= _pendMaxX; x++)
                for (int z = _pendMinZ; z <= _pendMaxZ; z++)
                {
                    if (_rectDeleteRoadsOnly)
                    {
                        if (_gridSystem.GetRoadTileAt(x, z) != null)
                            _roadManager.RemoveRoad(new Vector2Int(x, z));
                    }
                    else
                    {
                        var id = _gridSystem.GetBuildingIdentityAt(x, z);
                        if (id != null) uniqBuildings.Add(id);
                        if (_gridSystem.GetRoadTileAt(x, z) != null)
                            _roadManager.RemoveRoad(new Vector2Int(x, z));
                    }
                }
                foreach (var b in uniqBuildings)
                    _buildingManager.TryDeleteBuilding(b.rootGridPosition);

                _selectionManager.HideSelectionVisuals();
                _awaitingConfirm = false;
                _notificationManager.ShowNotification(_rectDeleteRoadsOnly
                    ? "Удалены дороги в области"
                    : "Удалены объекты и дороги в области");
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                _selectionManager.HideSelectionVisuals();
                _awaitingConfirm = false;
                _notificationManager.ShowNotification("Отменено");
                return;
            }

            return; // пока ждём подтверждения — больше ничего не делаем
        }

        // --- Начало действия ЛКМ ---
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            // Клик по ЗДАНИЮ — сразу удаляем поштучно
            var bld = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);
            if (bld != null)
            {
                _buildingManager.TryDeleteBuilding(gridPos);
                return;
            }

            // Старт прямоугольника. Если попали в ДОРОГУ — режим "только дороги".
            _dragStartPosition = gridPos;
            _rectDeleteRoadsOnly = (_gridSystem.GetRoadTileAt(gridPos.x, gridPos.y) != null);
            _isDragging = true;
            _selectionManager.StartSelection(worldPos);
            return;
        }

        // --- Обновление рамки ---
        if (Input.GetMouseButton(0) && _isDragging && !isOverUI)
        {
            _selectionManager.UpdateSelection(worldPos);
        }

        // --- Завершение рамки ---
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;

            Vector2Int a = _dragStartPosition;
            Vector2Int b = gridPos;
            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.y, b.y);
            int maxZ = Mathf.Max(a.y, b.y);

            // Если начато на дороге и выделена ровно 1 клетка — это одиночное удаление дороги.
            if (_rectDeleteRoadsOnly && minX == maxX && minZ == maxZ)
            {
                if (_gridSystem.GetRoadTileAt(minX, minZ) != null)
                    _roadManager.RemoveRoad(new Vector2Int(minX, minZ));

                _selectionManager.HideSelectionVisuals();
                return;
            }

            // Подсчёт объектов в области
            int roads = 0, buildings = 0;
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_gridSystem.GetRoadTileAt(x, z) != null) roads++;
                if (!_rectDeleteRoadsOnly && _gridSystem.GetBuildingIdentityAt(x, z) != null) buildings++;
            }

            if (roads + buildings == 0)
            {
                _selectionManager.HideSelectionVisuals();
                _notificationManager.ShowNotification("В области нет объектов для удаления");
                return;
            }

            // Сохраняем и ждём подтверждения (ЛКМ — да, ПКМ/Esc — отмена)
            _pendMinX = minX; _pendMaxX = maxX;
            _pendMinZ = minZ; _pendMaxZ = maxZ;
            _pendRoads = roads; _pendBuildings = buildings;
            _awaitingConfirm = true;

            string msg = _rectDeleteRoadsOnly
                ? $"Удалить дороги: {roads}? ЛКМ — подтвердить, ПКМ/Esc — отмена"
                : $"Удалить дороги: {roads} и здания: {buildings}? ЛКМ — подтвердить, ПКМ/Esc — отмена";
            _notificationManager.ShowNotification(msg);
            return;
        }

        // --- Выход из режима ---
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            _selectionManager.HideSelectionVisuals();
            _controller.SetMode(InputMode.None);
            return;
        }
    }

    public void OnExit()
    {
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
        _awaitingConfirm = false;
    }
}
