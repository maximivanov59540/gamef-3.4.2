using UnityEngine;

/// <summary>
/// Режим "Пристройки Модулей" (например, "Полей" к "Ферме").
/// (Версия 2.0: Поддерживает модули любого размера (1x1, 3x3...)
/// </summary>
public class State_PlacingModule : IInputState
{
    private readonly PlayerInputController _controller;
    private readonly GridSystem _gridSystem;
    private readonly BuildingManager _buildingManager;
    private readonly NotificationManager _notificationManager;
    
    private ModularBuilding _targetFarm; // "Ферма", к которой "строим"
    private BuildingData _moduleData; // "Чертеж" "Модуля" (1x1 или 3x3)
    
    private GameObject _moduleGhost;
    private bool _canPlaceModule;

    public State_PlacingModule(PlayerInputController controller, GridSystem gridSystem,
                               BuildingManager buildingManager, NotificationManager notificationManager)
    {
        _controller = controller;
        _gridSystem = gridSystem;
        _buildingManager = buildingManager;
        _notificationManager = notificationManager;
    }
    
    // "Пустой" OnEnter() (вызывается из SetMode)
    public void OnEnter()
    {
        // Очищаем "призрак" от *предыдущего* раза, если он остался
        if (_moduleGhost != null)
        {
            GameObject.Destroy(_moduleGhost);
            _moduleGhost = null;
        }
    }
    
    /// <summary>
    /// "Вход" "в" "режим" "с" "параметрами". Вызывается "сразу" "после" SetMode().
    /// </summary>
    public void OnEnter(ModularBuilding targetFarm, BuildingData moduleData)
    {
        _targetFarm = targetFarm;
        _moduleData = moduleData;
        _canPlaceModule = false;
        
        if (_moduleData == null || _targetFarm == null)
        {
            Debug.LogError("State_PlacingModule: Не получены 'targetFarm' или 'moduleData'!");
            _controller.SetMode(InputMode.None);
            return;
        }

        // "Создаем" "призрак" "модуля" (правильного размера)
        _moduleGhost = GameObject.Instantiate(_moduleData.buildingPrefab);
        // (BuildingManager отвечает за покраску и отключение физики)
        _buildingManager.SetBuildingVisuals(_moduleGhost, VisualState.Ghost, false);
        
        _notificationManager.ShowNotification($"Режим: Добавление '{_moduleData.buildingName}'");
    }

    public void OnUpdate()
    {
        if (_moduleGhost == null) return; // (Ждем OnEnter(params))

        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;

        if (gridPos.x == -1 || isOverUI)
        {
            _moduleGhost.SetActive(false);
            _canPlaceModule = false;
        }
        else
        {
            if (!_moduleGhost.activeSelf) _moduleGhost.SetActive(true);

            // --- ИЗМЕНЕНИЕ 2.0: Позиционирование (как в BuildingManager) ---
            Vector2Int size = _moduleData.size;
            Vector3 worldPos = _gridSystem.GetWorldPosition(gridPos.x, gridPos.y);
            float cellSize = _gridSystem.GetCellSize();
            worldPos.x += (size.x * cellSize) / 2f;
            worldPos.z += (size.y * cellSize) / 2f;
            _moduleGhost.transform.position = worldPos;
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            
            // "Проверяем" "валидность" "по" "трем" "правилам":
            // 1. "Клетки" "свободны"?
            bool isAreaClear = _gridSystem.CanBuildAt(gridPos, size);
            // 2. "Модуль" "примыкает" "к" "Ферме" "или" "другому" "Полю"?
            bool isAdjacent = CheckAdjacency(gridPos, size);
            // 3. "Лимит" "модулей" "не" "достигнут"?
            bool hasCap = _targetFarm.CanAddModule();

            _canPlaceModule = isAreaClear && isAdjacent && hasCap;
            
            _buildingManager.SetBuildingVisuals(_moduleGhost, VisualState.Ghost, _canPlaceModule);
            
            // (Показываем "красным" + "уведомление", если МЕСТО есть, но ЛИМИТ)
            if(isAreaClear && isAdjacent && !hasCap)
            {
                _notificationManager.ShowNotification("Достигнут лимит модулей для этого здания!");
            }
        }

        // "Обработка" "клика"
        if (Input.GetMouseButtonDown(0) && _canPlaceModule && !isOverUI)
        {
            PlaceModule(gridPos);
            // (Не выходим из режима, позволяем "штамповать" модули)
        }

        // "Выход" "из" "режима"
        if (Input.GetMouseButtonDown(1))
        {
            _controller.SetMode(InputMode.None);
        }
    }
    
    private void PlaceModule(Vector2Int gridPos)
    {
        // 1. "Создаем" "реальный" "объект" "Модуля"
        Vector3 worldPos = _moduleGhost.transform.position; // (Позиция уже "отцентрована")
        GameObject newModuleObj = GameObject.Instantiate(_moduleData.buildingPrefab, worldPos, Quaternion.identity);
        newModuleObj.layer = LayerMask.NameToLayer("Buildings");
        
        BuildingModule moduleComponent = newModuleObj.GetComponent<BuildingModule>();
        if (moduleComponent == null)
        {
            Debug.LogError($"Префаб {_moduleData.name} не имеет компонента BuildingModule!");
            GameObject.Destroy(newModuleObj);
            return;
        }
        
        // --- ИЗМЕНЕНИЕ 2.0: "Регистрируем" "размер" "и" "корень" ---
        Vector2Int size = _moduleData.size;
        moduleComponent.size = size;            // Сообщаем модулю его размер
        moduleComponent.gridPosition = gridPos; // Сообщаем модулю его "корень"
        
        // 2. "Регистрируем" "в" "Ферме" (1 раз)
        _targetFarm.RegisterModule(moduleComponent); 
        
        // 3. "Регистрируем" "в" "Сетке" (N раз)
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cellPos = new Vector2Int(gridPos.x + x, gridPos.y + z);
                _gridSystem.SetModule(cellPos, moduleComponent);
            }
        }
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---
        
        // 4. "Красим" "в" "реальный" "цвет"
        _buildingManager.SetBuildingVisuals(newModuleObj, VisualState.Real, true);
    }

    /// <summary>
    /// "Проверяет" "ПЕРИМЕТР" "модуля" (gridPos, size) 
    /// "на" "соседство" "с" "_targetFarm" "или" "ее" "модулями".
    /// </summary>
    private bool CheckAdjacency(Vector2Int gridPos, Vector2Int size)
    {
        int minX = gridPos.x;
        int maxX = gridPos.x + size.x - 1;
        int minZ = gridPos.y;
        int maxZ = gridPos.y + size.y - 1;

        // "Проверяем" "Верхнюю" "и" "Нижнюю" "кромки" (и их соседей)
        for (int x = minX; x <= maxX; x++)
        {
            // "Клетка" "ПОД" "нижней" "кромкой"
            if (IsNeighborValid(new Vector2Int(x, minZ - 1))) return true; 
            // "Клетка" "НАД" "верхней" "кромкой"
            if (IsNeighborValid(new Vector2Int(x, maxZ + 1))) return true; 
        }

        // "Проверяем" "Левую" "и" "Правую" "кромки" (и их соседей)
        for (int z = minZ; z <= maxZ; z++)
        {
            // "Клетка" "СЛЕВА" "от" "левой" "кромки"
            if (IsNeighborValid(new Vector2Int(minX - 1, z))) return true; 
            // "Клетка" "СПРАВА" "от" "правой" "кромки"
            if (IsNeighborValid(new Vector2Int(maxX + 1, z))) return true; 
        }

        return false;
    }

    /// <summary>
    /// "Хелпер" "для" CheckAdjacency: "проверяет" "1" "клетку"
    /// </summary>
    private bool IsNeighborValid(Vector2Int pos)
    {
        GridCellData cell = _gridSystem.GetCellData(pos.x, pos.y);
            
        // "Это" "главная" "Ферма"?
        if (cell.building != null && cell.building.GetComponent<ModularBuilding>() == _targetFarm)
        {
            return true;
        }
        // "Это" "другое" "Поле" "ЭТОЙ" "Фермы"?
        if (cell.module != null && cell.module.parentBuilding == _targetFarm)
        {
            return true;
        }
        
        return false;
    }

    public void OnExit()
    {
        // "Уничтожаем" "временный" "призрак" "при" "выходе" "из" "режима"
        if (_moduleGhost != null)
        {
            GameObject.Destroy(_moduleGhost);
            _moduleGhost = null;
        }
    }
}