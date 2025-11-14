using UnityEngine;

/// <summary>
/// Режим "Строительство" (Одиночное и Массовое)
/// </summary>
public class State_Building : IInputState
{
    // --- Ссылки на "Инструменты" ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly MassBuildHandler _massBuildHandler;
    private readonly AuraManager _auraManager;

    // --- "Память" этого режима (Переехали из PlayerInputController) ---
    private Vector2Int _dragStartPosition = new Vector2Int(-1, -1);
    private Vector2Int _lastMouseGridPos = new Vector2Int(-1, -1);
    private bool _isDragging = false;
    
    public State_Building(PlayerInputController controller, NotificationManager notificationManager, 
                          BuildingManager buildingManager, MassBuildHandler massBuildHandler, AuraManager auraManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _massBuildHandler = massBuildHandler;
        _auraManager = auraManager;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Строительство (Кисть)");
        _isDragging = false; // Сброс на всякий случай
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleBuildingMode ---
        
        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();

        // 1. Обновление "блуждающей тени"
        if (!_isDragging)
        {
            _buildingManager.UpdateGhostPosition(gridPos, worldPos);
        }
BuildingData currentData = _buildingManager.GetCurrentGhostData();
AuraEmitter emitter = _buildingManager.GetGhostAuraEmitter(); 
float rotation = _buildingManager.GetCurrentGhostRotation(); // <-- 1. ПОЛУЧАЕМ ПОВОРОТ

        if (emitter != null && emitter.distributionType == AuraDistributionType.RoadBased)
        {
            // 2. ВЫЗЫВАЕМ НОВУЮ "УМНУЮ" ВЕРСИЮ МЕТОДА
            _auraManager.ShowRoadAuraPreview(worldPos, gridPos, currentData.size, rotation, emitter.radius);
        }
        else
        {
            // Прячем превью, если у здания нет ауры
            _auraManager.HideRoadAuraPreview();
        }

        // 2. Вращение
        if (Input.GetKeyDown(KeyCode.R))
        {
            _buildingManager.RotateGhost();

            if (_isDragging)
            {
                _massBuildHandler.StartMassBuildPreview(_buildingManager.GetCurrentGhostData(), _buildingManager.GetCurrentGhostRotation());
                _massBuildHandler.UpdateMassBuildPreview(_dragStartPosition, gridPos);
            }
        }
        
        // 3. "Проверяем" "рецепт"
        if (currentData == null) return; // (Защита)

        if (currentData.useMassBuildTool)
        {
            // --- ЛОГИКА "ЗОНИРОВАНИЯ" (T-3) ---

            // 3а. "НАЧАЛО" (Клик)
            if (Input.GetMouseButtonDown(0) && !isOverUI && gridPos.x != -1)
            {
                _isDragging = true;
                _dragStartPosition = gridPos;
                _massBuildHandler.StartMassBuildPreview(currentData, _buildingManager.GetCurrentGhostRotation());
                _massBuildHandler.UpdateMassBuildPreview(_dragStartPosition, gridPos); // (Фикс одиночного клика)
                _buildingManager.ShowGhost(false); // "Прячем" "блуждающую тень"
            }

            // 3б. "ПЛАНИРОВАНИЕ" (Драг)
            if (Input.GetMouseButton(0) && _isDragging && gridPos != _lastMouseGridPos)
            {
                _massBuildHandler.UpdateMassBuildPreview(_dragStartPosition, gridPos);
            }

            // 3в. "ИСПОЛНЕНИЕ" (Отпускание)
            if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                _isDragging = false;
                _massBuildHandler.ExecuteMassBuild();
                _buildingManager.ShowGhost(true); // "Возвращаем" "блуждающую тень"
            }
        }
        else
        {
            // --- "ОБЫЧНАЯ" ЛОГИКА (Одиночный Клик) ---
            if (Input.GetMouseButtonDown(0) && !isOverUI)
            {
                _buildingManager.TryPlaceBuilding(gridPos);
            }
        }

        // 4. "Отмена" (Работает для ОБОИХ режимов)
        if (Input.GetMouseButtonDown(1))
        {
            // --- СМЕНА СОСТОЯНИЯ ---
            _buildingManager.CancelAllModes();
            
            _controller.SetMode(InputMode.None);
        }

        // 5. "Запоминаем" позицию
        _lastMouseGridPos = gridPos;
    }

    public void OnExit()
    {
        _massBuildHandler.ClearMassBuildPreview();
        _auraManager?.HideRoadAuraPreview();
        _isDragging = false;
    }
}