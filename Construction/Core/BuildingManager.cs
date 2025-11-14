using UnityEngine;
using System.Collections.Generic;
public class BuildingManager : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public GameObject gridVisual;

    // --- Ссылки на другие системы ---
    private ResourceManager _resourceManager;
    private PopulationManager _populationManager;
    private GridSystem _gridSystem;
    [SerializeField] private PlayerInputController _inputController;
    private NotificationManager _notificationManager;

    // --- Внутреннее состояние ---
    private BuildingData _selectedBuildingData;
    private GameObject _ghostBuilding;
    private GhostBuildingCollider _ghostCollider;

    private GameObject _buildingToMove = null;
    private Vector2Int _originalMovePosition;

    private bool _canPlace = false;
    // private bool _gridVisible = true; // <-- УДАЛЕНО (Фикс #2)

    private float _currentYRotation = 0f;
    private Vector2Int _currentRotatedSize;
    private float _originalMoveRotation;
    private bool? _copiedBuildingState = null;
    private AuraEmitter _ghostAuraEmitter = null;

    // (УДАЛЕНЫ ПОЛЯ STATE_... И _CURRENTSTATE - Фикс #1)


    void Awake()
    {
        _resourceManager = FindFirstObjectByType<ResourceManager>();
        _populationManager = FindFirstObjectByType<PopulationManager>();
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _notificationManager = FindFirstObjectByType<NotificationManager>();

        if (_inputController == null) _inputController = FindFirstObjectByType<PlayerInputController>();
        if (_resourceManager == null) Debug.LogError("BuildingManager: Не найден ResourceManager в сцене!", this);
        if (_populationManager == null) Debug.LogError("BuildingManager: Не найден PopulationManager в сцене!", this);
        if (_gridSystem == null) Debug.LogError("BuildingManager: Не найден GridSystem в сцене!", this);
        if (_notificationManager == null) Debug.LogWarning("BuildingManager: Не найден NotificationManager в сцене.", this);

    }
    /// <summary>Хелпер для State_Building, чтобы тот мог рисовать превью.</summary>
    public AuraEmitter GetGhostAuraEmitter()
    {
        return _ghostAuraEmitter;
    }
    // --- 1. ПУБЛИЧНЫЕ КОМАНДЫ (из BuildUIManager) ---

    public void EnterBuildMode(BuildingData buildingData)
    {
        if (buildingData == null)
        {
            Debug.LogError("!!! ОШИБКА: 'buildingData' (чертеж) пришел в BuildingManager как NULL!");
            return;
        }
        if (buildingData.buildingPrefab == null)
        {
            Debug.LogError($"!!! ОШИБКА: 'buildingData' ('{buildingData.name}') получен, НО 'buildingPrefab' внутри него -- NULL!");
            return;
        }

        CancelGhostOnly();
        _copiedBuildingState = null;

        _selectedBuildingData = buildingData;
        _currentYRotation = 0f;
        _currentRotatedSize = buildingData.size;

        _ghostBuilding = Instantiate(buildingData.buildingPrefab);
        _ghostBuilding.layer = LayerMask.NameToLayer("Ghost");

        var producers = _ghostBuilding.GetComponentsInChildren<ResourceProducer>();
        foreach (var p in producers) p.enabled = false;
        var identity = _ghostBuilding.GetComponent<BuildingIdentity>();
        if (identity != null) identity.enabled = false;

        _ghostAuraEmitter = _ghostBuilding.GetComponent<AuraEmitter>();
        SetupGhostCollider(buildingData.size);
        SetBuildingVisuals(_ghostBuilding, VisualState.Ghost, true);

        // ShowGrid(true); // <-- УДАЛЕНО (Фикс #2)
        _inputController.SetMode(InputMode.Building);
    }

    // --- НАЧАЛО: НОВЫЙ КОД (Хелперы для Задачи B) ---
    public BuildingData GetCurrentGhostData()
    {
        return _selectedBuildingData;
    }

    public float GetCurrentGhostRotation()
    {
        return _currentYRotation;
    }

    // --- КОНЕЦ: НОВОГО КОДА ---

    public void EnterMoveMode()
    {
        CancelAllModes();
        // ShowGrid(true); // <-- УДАЛЕНО (Фикс #2)
        _inputController.SetMode(InputMode.Moving);
    }

    public void EnterDeleteMode()
    {
        CancelAllModes();
        // ShowGrid(true); // <-- УДАЛЕНО (Фикс #2)
        _inputController.SetMode(InputMode.Deleting);
    }

    public void EnterUpgradeMode()
    {
        CancelAllModes();
        // ShowGrid(true); // <-- УДАЛЕНО (Фикс #2)
        _inputController.SetMode(InputMode.Upgrading);
    }

    public void EnterCopyMode()
    {
        CancelAllModes();
        // ShowGrid(true); // <-- УДАЛЕНО (Фикс #2)
        _inputController.SetMode(InputMode.Copying);
    }
    public void ShowGhost(bool show)
    {
        // "Выбираем", "что" "мы" "прячем": "новый" "призрак" "или" "перемещаемое" "здание"
        GameObject objectToShow = _buildingToMove != null ? _buildingToMove : _ghostBuilding;
        if (objectToShow != null)
        {
            objectToShow.SetActive(show);
        }
    }
    // --- 2. КОМАНДЫ "В ПОЛЕ" (из PlayerInputController) ---

    public void UpdateGhostPosition(Vector2Int gridPos, Vector3 worldPos)
    {
        GameObject objectToMove = _buildingToMove != null ? _buildingToMove : _ghostBuilding;
        BuildingData data = _buildingToMove != null ? _buildingToMove.GetComponent<BuildingIdentity>().buildingData : _selectedBuildingData;

        if (objectToMove == null || data == null) return;

        if (gridPos.x == -1)
        {
            objectToMove.SetActive(false);
            _canPlace = false;
            return;
        }

        if (!objectToMove.activeSelf) objectToMove.SetActive(true);

        Vector3 finalPos = _gridSystem.GetWorldPosition(gridPos.x, gridPos.y);
        float cellSize = _gridSystem.GetCellSize();
        Vector2Int size = _currentRotatedSize; // Было: data.size

        // --- ВОТ ИСПРАВЛЕНИЕ: Добавляем объявление и расчет ---
        // Теперь рассчитываем центр (1.5), а не смещение угла (1.0).
        float offsetX = (size.x * cellSize) / 2f;
        float offsetZ = (size.y * cellSize) / 2f;

        finalPos.x += offsetX;
        finalPos.z += offsetZ;
        finalPos.y = worldPos.y;

        objectToMove.transform.position = finalPos;
        objectToMove.transform.rotation = Quaternion.Euler(0, _currentYRotation, 0);

        CheckPlacementValidity(objectToMove, data, gridPos);
    }
    /// <summary>
    /// Выполняет приказ о "массовом" удалении списка зданий.
    /// </summary>
    /// <param name="selection">Список зданий, полученный от SelectionManager.</param>
    public void MassDelete(HashSet<BuildingIdentity> selection)
    {
        int totalRefundedBuildings = 0;

        // "Пробегаемся" по "умному" списку
        foreach (BuildingIdentity id in selection)
        {
            if (id != null)
            {
                // Вызываем наш "умный" метод для КАЖДОГО здания
                DeleteBuilding(id);
                totalRefundedBuildings++;
            }
        }
        // Показываем ОДНО общее сообщение
        if (totalRefundedBuildings > 0)
        {
            _notificationManager?.ShowNotification($"Уничтожено {totalRefundedBuildings} зданий. Вернулось 50% ресурсов.");
        }
    }
    public void MassUpgrade(HashSet<BuildingIdentity> selection)
    {
        int upgradedCount = 0;
        foreach (BuildingIdentity id in selection)
        {
            // (Пропускаем, если это НЕ "проект")
            if (id == null || !id.isBlueprint)
            {
                continue;
            }

            // "Вызываем" наш "движок"
            bool success = ExecuteUpgrade(id);

            if (success)
            {
                upgradedCount++;
            }
            else
            {
                // "СТОП" (ExecuteUpgrade сам показал "Нет Ресурсов")
                // (Мы "выходим" из цикла, т.к. ресурсы кончились)
                continue;
            }
        }

        // (Финальное "общее" уведомление)
        if (upgradedCount > 0)
        {
            _notificationManager?.ShowNotification($"Улучшено {upgradedCount} зданий.");
        }
    }

    public void TryPickUpBuilding(Vector2Int gridPos)
    {
        GameObject pickedBuilding = _gridSystem.PickUpBuilding(gridPos.x, gridPos.y);
        if (pickedBuilding != null)
        {
            StartMovingBuilding(pickedBuilding, gridPos.x, gridPos.y);
        }
    }
    private void CancelGhostOnly()
    {
        if (_ghostBuilding != null)
        {
            Destroy(_ghostBuilding);
            _ghostBuilding = null;
            _ghostCollider = null;
        }
    }

    public void TryDeleteBuilding(Vector2Int gridPos)
    {
        // 1. Находим, что удаляем
        BuildingIdentity identity = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

        // 2. Если там что-то есть...
        if (identity != null)
        {
            // 3. Вызываем наш новый "умный" метод
            DeleteBuilding(identity);
            _notificationManager?.ShowNotification("Вернулось 50% ресурсов");
        }
    }

    // --- ИЗМЕНЕНИЕ (Баг 3): Теперь мы принимаем "rootGridPos" от "Диспетчера" ---
    public void TryPlaceBuilding(Vector2Int rootGridPos)
    {
        // 1. Главная проверка "Можно ли строить?"
        if (!_canPlace)
        {
            _notificationManager?.ShowNotification("Место занято!");
            return;
        }

        // 2. "Развилка" (которая теперь будет работать)
        bool buildAsBlueprint;

        if (_copiedBuildingState != null)
        {
            // СЛУЧАЙ 1: Мы "копируем". Состояние "копии" "важнее" "режима".
            buildAsBlueprint = _copiedBuildingState.Value; // (true если скопировали проект, false если реальное)
        }
        else if (BlueprintManager.IsActive && _buildingToMove == null)
        {
            // СЛУЧАЙ 2: Мы "строим" "новый" "объект" в "режиме" "проекта".
            buildAsBlueprint = true;
        }
        else
        {
            // СЛУЧАЙ 3: Мы "строим" "новый" "объект" "по-настоящему".
            buildAsBlueprint = false;
        }


        if (buildAsBlueprint)
        {
            // PlaceBlueprint() не "тратит" "ресурсы". Это "правильно" для "проекта".
            PlaceBlueprint(rootGridPos);
        }
        else
        {
            // PlaceRealBuilding() "проверит" "ресурсы" и "построит".
            PlaceRealBuilding(rootGridPos);
        }
        if (_copiedBuildingState != null)
        {
            _copiedBuildingState = null;
        }
    }
    public void TryUpgradeBuilding(Vector2Int gridPos)
    {
        BuildingIdentity identity = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);
        ExecuteUpgrade(identity); // (Нам не важен 'bool' в "поштучном" режиме)
    }
    private bool ExecuteUpgrade(BuildingIdentity identity)
    {
        // 1. Проверка: Мы вообще по чему-то попали?
        if (identity == null)
        {
            _notificationManager?.ShowNotification("Здесь ничего нет");
            return false;
        }

        // 2. ГЛАВНАЯ ПРОВЕРКА: Это "Проект"?
        if (!identity.isBlueprint)
        {
            _notificationManager?.ShowNotification("Это уже реальное здание");
            return false;
        }
        // --- НОВЫЕ ПРОВЕРКИ (Шаг 3.0) ---
        if (EconomyManager.Instance.IsInDebt)
        {
            _notificationManager?.ShowNotification("Мы в долгах! Улучшение невозможно.");
            return false;
        }
        
        BuildingData data = identity.buildingData;
        if (!MoneyManager.Instance.SpendMoney(data.moneyCost))
        {
            _notificationManager?.ShowNotification("Недостаточно золота для улучшения!");
            return false;
        }
        // --- КОНЕЦ ПРОВЕРОК ---

        // 3. Проверка: Хватает ли ресурсов?
        if (!_resourceManager.CanAfford(data))
        {
            _notificationManager?.ShowNotification("Недостаточно ресурсов!");
            // ВАЖНО: Возвращаем золото
            MoneyManager.Instance.AddMoney(data.moneyCost);
            return false; 
        }

        // 4. ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ! ВЫПОЛНЯЕМ "УЛУЧШЕНИЕ":

        // а) Тратим ресурсы
        _resourceManager.SpendResources(data);

        // б) Добавляем жилье (если это дом)
        if (data.housingCapacity > 0)
        {
            _populationManager.AddHousingCapacity(data.housingCapacity);
        }

        // в) Включаем производство (если это фабрика)
        var producer = identity.GetComponent<ResourceProducer>();
        if (producer != null)
        {
            producer.enabled = true;
        }

        // г) Снимаем флаг "Проекта"
        identity.isBlueprint = false;

        // д) Меняем материал на "Реальный"
        SetBuildingVisuals(identity.gameObject, VisualState.Real, true);

        _notificationManager?.ShowNotification($"{data.buildingName} построен!");

        return true; // "УСПЕХ!"
    }
    // --- КОНЕЦ: НОВОГО КОДА ---
    public void TryCopyBuilding(Vector2Int gridPos)
    {
        // Шаг 1 (Найти):
        BuildingIdentity identity = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

        // Шаг 2 (Проверить):
        if (identity == null)
        {
            // Кликнули по пустой земле, ничего не делаем
            return;
        }

        // --- Шаг 3 ("Украсть Рецепт"): ---
        BuildingData dataToCopy = identity.buildingData;
        float rotationToCopy = identity.yRotation;
        _copiedBuildingState = identity.isBlueprint;

        // --- Шаг 4 (Переключить Режим): ---

        // 1. "Заряжаем" режим строительства (это сбросит поворот в 0)
        EnterBuildMode(dataToCopy);

        // 2. "Перезаписываем" поворот "украденным"
        _currentYRotation = rotationToCopy;

        // 3. (ВАЖНЫЙ ФИКС) Синхронизируем "логический" размер
        //    с "украденным" поворотом, т.к. EnterBuildMode()
        //    не знает о нашем "хаке" с поворотом.
        if (Mathf.Abs(_currentYRotation - 90f) < 1f || Mathf.Abs(_currentYRotation - 270f) < 1f)
        {
            // Если здание повернуто, инвертируем размер
            _currentRotatedSize = new Vector2Int(dataToCopy.size.y, dataToCopy.size.x);
        }
    }

    public void CancelAllModes()
    {
        if (_ghostBuilding != null)
        {
            Destroy(_ghostBuilding);
            _ghostBuilding = null;
            _ghostCollider = null;
            _selectedBuildingData = null;
        }
        else if (_buildingToMove != null)
        {
            BuildingIdentity identity = _buildingToMove.GetComponent<BuildingIdentity>();
            Vector2Int size = identity.buildingData.size;
            float cellSize = _gridSystem.GetCellSize();
            Vector3 originalWorldPos = _gridSystem.GetWorldPosition(_originalMovePosition.x, _originalMovePosition.y);
            if (Mathf.Abs(_originalMoveRotation - 90f) < 1f || Mathf.Abs(_originalMoveRotation - 270f) < 1f)
            {
                size = new Vector2Int(size.y, size.x);
            }

            // Добавляем расчёт смещения для правильного позиционирования
            float offsetX = (size.x * cellSize) / 2f;
            float offsetZ = (size.y * cellSize) / 2f;

            originalWorldPos.x += offsetX;
            originalWorldPos.z += offsetZ;
            originalWorldPos.y = _buildingToMove.transform.position.y;

            _buildingToMove.transform.position = originalWorldPos;
            _buildingToMove.transform.rotation = Quaternion.Euler(0, _originalMoveRotation, 0);
            identity.rootGridPosition = _originalMovePosition;
            _gridSystem.OccupyCells(identity, size);

            if (identity.isBlueprint)
            {
                SetBuildingVisuals(_buildingToMove, VisualState.Blueprint, true);
            }
            else
            {
                SetBuildingVisuals(_buildingToMove, VisualState.Real, true);
            }
            GhostifyBuilding(identity.gameObject, false);
            if (!identity.isBlueprint)
            {
                var producer = identity.GetComponent<ResourceProducer>();
                if (producer != null) producer.enabled = true;
            }

            _buildingToMove = null;
        }

        _selectedBuildingData = null;
        _copiedBuildingState = null;
        _ghostAuraEmitter = null;
    }

    public bool IsHoldingBuilding()
    {
        return _buildingToMove != null;
    }

    // --- НОВЫЙ "ГЛУПЫЙ" МЕТОД (Фикс #2) ---
    /// <summary>
    /// "Глупый" метод, который просто выполняет приказ
    /// </summary>
    public void ShowGrid(bool show)
    {
        if (gridVisual != null)
        {
            gridVisual.SetActive(show);
        }
    }

    // --- 3. ВНУТРЕННИЕ (ПРИВАТНЫЕ) МЕТОДЫ-ХЕЛПЕРЫ ---
    // (Тут нет изменений)

    private void SetupGhostCollider(Vector2Int size)
    {
        var collider = _ghostBuilding.GetComponent<BoxCollider>();
        if (collider == null) collider = _ghostBuilding.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        float cellSize = _gridSystem.GetCellSize();

        collider.center = new Vector3(
            0, // БЫЛО: (size.x * cellSize) / 2f - (cellSize / 2f)
        0.5f,
        0  // БЫЛО: (size.y * cellSize) / 2f - (cellSize / 2f)
        );
        collider.size = new Vector3(
            size.x * cellSize * 0.9f, 1f, size.y * cellSize * 0.9f
        );

        var rb = _ghostBuilding.GetComponent<Rigidbody>();
        if (rb == null) rb = _ghostBuilding.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        _ghostCollider = _ghostBuilding.AddComponent<GhostBuildingCollider>();
    }

    // --- ПОЛНОСТЬЮ ЗАМЕНИ СТАРЫЙ МЕТОД НА ЭТОТ ---

    /// <summary>
    /// Универсальный метод для смены материалов на здании (реальный, призрак, проект).
    /// </summary>
    /// <summary>
    /// Универсальный метод для смены материалов на здании (реальный, призрак, проект).
    /// </summary>
    public void SetBuildingVisuals(GameObject building, VisualState state, bool isValid)
    {
        if (building == null) return;

        var visuals = building.GetComponent<BuildingVisuals>();
        if (visuals != null)
        {
            visuals.SetState(state, isValid);
        }
        // (Если 'visuals == null', "значит", "мы" "забыли" "добавить" "его" "на" "префаб")
    }
    // --- НОВЫЙ ПРИВАТНЫЙ МЕТОД (для реальной постройки) ---
    private bool PlaceRealBuilding(Vector2Int rootGridPos)
    {
        // --- ПРОВЕРКА ДОЛГА (в самом верху) ---
        if (EconomyManager.Instance.IsInDebt)
        {
            _notificationManager?.ShowNotification("Мы в долгах! Строительство невозможно.");
            return false;
        }
        // --- КОНЕЦ ПРОВЕРКИ ---
        
        if (_buildingToMove != null)
        {
            // --- СЛУЧАЙ 1: Мы "ставим" ПЕРЕМЕЩАЕМОЕ здание ---
            // (Перемещение бесплатное, проверка золота не нужна)
            int rootX = rootGridPos.x;
            int rootZ = rootGridPos.y;

            BuildingIdentity identity = _buildingToMove.GetComponent<BuildingIdentity>();
            _buildingToMove.transform.rotation = Quaternion.Euler(0, _currentYRotation, 0);

            identity.rootGridPosition = new Vector2Int(rootX, rootZ);
            identity.yRotation = _currentYRotation;

            _gridSystem.OccupyCells(identity, _currentRotatedSize);

            if (identity.isBlueprint)
                SetBuildingVisuals(_buildingToMove, VisualState.Blueprint, true);
            else
                SetBuildingVisuals(_buildingToMove, VisualState.Real, true);

            GhostifyBuilding(identity.gameObject, false);
            if (!identity.isBlueprint)
            {
                var producer = identity.GetComponent<ResourceProducer>();
                if (producer != null) producer.enabled = true;
            }

            _buildingToMove = null;
            return true; // "УСПЕХ": Здание построено
        }
        else if (_ghostBuilding != null)
        {
            // --- СЛУЧАЙ 2: Мы "ставим" НОВОЕ здание ---

            // --- ПРОВЕРКИ ЗОЛОТА И РЕСУРСОВ (3.0) ---
            if (!MoneyManager.Instance.SpendMoney(_selectedBuildingData.moneyCost))
            {
                _notificationManager?.ShowNotification("Недостаточно золота!");
                return false;
            }
            if (!_resourceManager.CanAfford(_selectedBuildingData))
            {
                _notificationManager?.ShowNotification("Недостаточно ресурсов!");
                // ВАЖНО: Возвращаем золото, т.к. стройка не удалась
                MoneyManager.Instance.AddMoney(_selectedBuildingData.moneyCost);
                return false;
            }
            // --- КОНЕЦ ПРОВЕРОК ---

            // --- Если мы здесь, значит, все проверки пройдены ---
            int rootX = rootGridPos.x;
            int rootZ = rootGridPos.y;
            Vector3 worldPosition = _gridSystem.GetWorldPosition(rootGridPos.x, rootGridPos.y);
            float cellSize = _gridSystem.GetCellSize();
            Vector2Int size = _currentRotatedSize;

            worldPosition.x += (size.x * cellSize) / 2f;
            worldPosition.z += (size.y * cellSize) / 2f;

            GameObject newBuilding = Instantiate(_selectedBuildingData.buildingPrefab,
                worldPosition,
                Quaternion.Euler(0, _currentYRotation, 0));

            SetBuildingVisuals(newBuilding, VisualState.Real, true);
            newBuilding.layer = LayerMask.NameToLayer("Buildings");
            newBuilding.tag = "Building";

            var identity = newBuilding.GetComponent<BuildingIdentity>();
            if (identity == null) identity = newBuilding.AddComponent<BuildingIdentity>();

            identity.buildingData = _selectedBuildingData;
            identity.rootGridPosition = new Vector2Int(rootX, rootZ);
            identity.yRotation = _currentYRotation;
            identity.isBlueprint = false;

            SetBuildingVisuals(newBuilding, VisualState.Real, true);

            var producers = newBuilding.GetComponentsInChildren<ResourceProducer>();
            foreach (var p in producers) p.enabled = true;
            var id_comp = newBuilding.GetComponent<BuildingIdentity>();
            if (id_comp != null) id_comp.enabled = true;

            _resourceManager.SpendResources(_selectedBuildingData);
            if (_selectedBuildingData.housingCapacity > 0)
            {
                _populationManager.AddHousingCapacity(_selectedBuildingData.housingCapacity);
            }

            var producer = newBuilding.GetComponent<ResourceProducer>();
            if (producer != null) producer.enabled = true;

            _gridSystem.OccupyCells(identity, _currentRotatedSize);
            return true;
        }

        // "Аварийный" выход
        return false;
    }
    // --- НАЧАЛО: НОВЫЙ КОД (Рефакторинг) ---

    /// <summary>
    /// Главный приватный метод, который обрабатывает ПОЛНОЕ удаление здания.
    /// Он возвращает ресурсы, а затем приказывает GridSystem'у все очистить.
    /// </summary>
    private void DeleteBuilding(BuildingIdentity identity)
    {
        if (identity == null) return;

        // --- 1. ЛОГИКА ВОЗВРАТА РЕСУРСОВ (50%) ---
        if (!identity.isBlueprint)
        {
            BuildingData data = identity.buildingData;
            if (data != null && data.costs.Count > 0)
            {
                // "Пробегаемся" по всем ресурсам, которые стоило здание
                foreach (var resourceCost in data.costs)
                {
                    // "Рассчитываем" 50% (с округлением ВНИЗ)
                    int refundAmount = Mathf.FloorToInt(resourceCost.amount * 0.5f);

                    if (refundAmount > 0)
                    {
                        // "Возвращаем" на склад
                        _resourceManager.AddToStorage(resourceCost.resourceType, refundAmount);
                    }
                }
            }
        }
        if (!identity.isBlueprint && identity.buildingData.housingCapacity > 0)
        {
            // Убираем жилье. (Проверяем _populationManager на null на всякий случай)
            _populationManager?.RemoveHousingCapacity(identity.buildingData.housingCapacity);
        }

        // --- 2. ЛОГИКА ОЧИСТКИ (старый код) ---
        // (Этот метод уже умеет убирать жилье и вызывать Destroy)
        _gridSystem.ClearCell(identity.rootGridPosition.x, identity.rootGridPosition.y);
    }

    // --- КОНЕЦ: НОВОГО КОДА ---
    // --- НОВЫЙ ПРИВАТНЫЙ МЕТОД (для "чертежа") ---
    private void PlaceBlueprint(Vector2Int rootGridPos)
    {
        // "Проект" можно поставить только для НОВОГО здания (из _ghostBuilding)
        // Мы не можем "переместить" здание в виде проекта
        if (_ghostBuilding == null)
        {
            Debug.LogWarning("Попытка разместить 'проект' во время перемещения. Отменено.");
            return;
        }

        // 1. Получаем "корень" (уже есть из rootGridPos)
        int rootX = rootGridPos.x;
        int rootZ = rootGridPos.y;

        // 2. Получаем позицию в мире (призрак уже там)
        Vector3 worldPosition = _gridSystem.GetWorldPosition(rootGridPos.x, rootGridPos.y);
        float cellSize = _gridSystem.GetCellSize();
        // _currentRotatedSize "Прораб" УЖЕ "знает"
        Vector2Int size = _currentRotatedSize;

        worldPosition.x += (size.x * cellSize) / 2f;
        worldPosition.z += (size.y * cellSize) / 2f;

        // 3. Создаем здание (как и в PlaceRealBuilding)
        GameObject newBuilding = Instantiate(_selectedBuildingData.buildingPrefab, worldPosition, Quaternion.Euler(0, _currentYRotation, 0));
        newBuilding.layer = LayerMask.NameToLayer("Buildings");
        newBuilding.tag = "Building";

        // 4. Настраиваем BuildingIdentity
        var identity = newBuilding.GetComponent<BuildingIdentity>();
        if (identity == null) identity = newBuilding.AddComponent<BuildingIdentity>();

        identity.buildingData = _selectedBuildingData;
        identity.rootGridPosition = new Vector2Int(rootX, rootZ);

        // 5. САМОЕ ВАЖНОЕ (Отличия от PlaceRealBuilding):

        // --- ЗАКОММЕНТИРОВАНО, пока мы не обновим 'BuildingIdentity.cs' ---
        identity.yRotation = _currentYRotation;
        identity.isBlueprint = true; // <-- ПОМЕЧАЕМ ЕГО КАК "ПРОЕКТ"
                                     // ---

        // Мы НЕ списываем ресурсы
        // Мы НЕ добавляем жилье

        // Убеждаемся, что производство ВЫКЛЮЧЕНО
        var producer = newBuilding.GetComponent<ResourceProducer>();
        if (producer != null) producer.enabled = false;

        // 6. "РЕЗЕРВИРУЕМ" КЛЕТКИ (как и в PlaceRealBuilding)
        _gridSystem.OccupyCells(identity, _currentRotatedSize);

        // 7. TODO: Нам нужно будет применить "синий" материал "Проекта"
        SetBuildingVisuals(newBuilding, VisualState.Blueprint, true);

        Debug.Log("Проект размещен!");
    }

    private void StartMovingBuilding(GameObject buildingToMove, int x, int z)
    {
        _buildingToMove = buildingToMove;
        _originalMovePosition = new Vector2Int(x, z);

        _originalMoveRotation = buildingToMove.transform.eulerAngles.y; // Запоминаем оригинал
        _currentYRotation = _originalMoveRotation; // Начинаем с него

        BuildingIdentity identity = _buildingToMove.GetComponent<BuildingIdentity>();
        _currentRotatedSize = identity.buildingData.size;

        if (Mathf.Abs(_currentYRotation - 90f) < 1f || Mathf.Abs(_currentYRotation - 270f) < 1f)
        {
            _currentRotatedSize = new Vector2Int(_currentRotatedSize.y, _currentRotatedSize.x);
        }

        // Проверяем, что мы подобрали
        if (identity.isBlueprint)
        {
            // Если это проект - красим в "синий"
            SetBuildingVisuals(_buildingToMove, VisualState.Blueprint, true);
        }
        else
        {
            // Если это реал - красим в "зеленый призрак"
            SetBuildingVisuals(_buildingToMove, VisualState.Ghost, true);
        }

        ShowGrid(true);
        // --- НАЧАЛО ФИКСА #8 (Выключаем "мозги") ---
        // "Прячем" "визуал" - это хорошо, но надо
        // "выключить" "и" "логику", "пока" "здание" "в" "руках".
        var producer = _buildingToMove.GetComponent<ResourceProducer>();
        if (producer != null) producer.enabled = false;
        // (BuildingIdentity трогать не надо, он нужен для флага .isBlueprint)
        // --- КОНЕЦ ФИКСА #8 ---
        GhostifyBuilding(_buildingToMove, true);
    }

    public void CheckPlacementValidity(GameObject objectToCheck, BuildingData data, Vector2Int rootPos)
    {
        if (objectToCheck == null)
        {
            _canPlace = false;
            return;
        }

        Vector2Int size = _currentRotatedSize;

        bool canPlaceLogically = true;
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                if (_gridSystem.IsCellOccupied(rootPos.x + x, rootPos.y + z))
                {
                    canPlaceLogically = false;
                    break;
                }
            }
            if (!canPlaceLogically) break;
        }

        var ghostCol = objectToCheck.GetComponent<GhostBuildingCollider>();
        bool canPlacePhysically = true;
        if (ghostCol != null)
        {
            canPlacePhysically = !ghostCol.IsColliding();
        }

        _canPlace = canPlaceLogically && canPlacePhysically;

        GameObject objectToUpdate = _buildingToMove != null ? _buildingToMove : _ghostBuilding;

        if (objectToUpdate != null && objectToUpdate.activeSelf)
        {
            // ЗАМЕНИ СТАРЫЙ БЛОК 'if (_ghostBuilding != null)' НА ЭТОТ
            if (_ghostBuilding != null)
            {
                // --- СЛУЧАЙ 1: Мы "держим" НОВЫЙ "призрак" ---
                bool showAsBlueprint;

                if (_copiedBuildingState != null)
                {
                    // "Копия" "важнее" "режима"
                    showAsBlueprint = _copiedBuildingState.Value;
                }
                else
                {
                    // "Обычная" "стройка" "зависит" от "режима"
                    showAsBlueprint = BlueprintManager.IsActive;
                }

                if (showAsBlueprint)
                {
                    SetBuildingVisuals(objectToUpdate, VisualState.Blueprint, _canPlace);
                }
                else
                {
                    SetBuildingVisuals(objectToUpdate, VisualState.Ghost, _canPlace);
                }
            }
            else if (_buildingToMove != null)
            {
                // --- СЛУЧАЙ 2: Мы "держим" ПЕРЕМЕЩАЕМОЕ здание ---
                // (Эта логика УЖЕ ПРАВИЛЬНАЯ, не трогаем ее)
                bool isMovingBlueprint = _buildingToMove.GetComponent<BuildingIdentity>().isBlueprint;
                if (isMovingBlueprint)
                {
                    SetBuildingVisuals(objectToUpdate, VisualState.Blueprint, _canPlace);
                }
                else
                {
                    SetBuildingVisuals(objectToUpdate, VisualState.Ghost, _canPlace);
                }
            }
        }

        // Мы "красим" "призрака", ТОЛЬКО если он "активен" (видим)
        // (т.е. мы НЕ "тянем" "Улицу")
        /*if (objectToUpdate != null && objectToUpdate.activeSelf)
        {
            bool isBlueprintMode = BlueprintManager.Instance.IsBlueprintModeActive;
            bool isMovingBlueprint = (_buildingToMove != null && _buildingToMove.GetComponent<BuildingIdentity>().isBlueprint);

            if ((isBlueprintMode && _ghostBuilding != null) || isMovingBlueprint)
            {
                // "Красим" как "Проект" (Синий/Красный)
                SetBuildingVisuals(objectToUpdate, VisualState.Blueprint, _canPlace);
            }
            else
            {
                // "Красим" как "Призрак" (Зеленый/Красный)
                // (Раньше эта логика была "сломана")
                SetBuildingVisuals(objectToUpdate, VisualState.Ghost, _canPlace);
            }
        } */
    }
    public void RotateGhost()
    {
        // Мы можем вращать только если держим "призрак" или перемещаемое здание
        GameObject objectToRotate = _buildingToMove != null ? _buildingToMove : _ghostBuilding;
        if (objectToRotate == null) return; // Ничего не держим, выходим

        // (Весь остальной код из 'Update' просто скопирован сюда)

        // 1. Обновляем угол
        _currentYRotation = (_currentYRotation + 90f) % 360f;

        // 2. Обновляем "логический" размер (меняем X и Z местами)
        _currentRotatedSize = new Vector2Int(_currentRotatedSize.y, _currentRotatedSize.x);

        // 3. Применяем визуальный поворот (UpdateGhostPosition подхватит это)
        objectToRotate.transform.rotation = Quaternion.Euler(0, _currentYRotation, 0);

        // 4. Важно: Нам нужно немедленно пересчитать валидность
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        if (gridPos.x != -1 && _inputController != null)
        {
            // Получаем 'worldPos' так же, как это делает InputController
            // (Это может быть неточным, но для простоты сойдет)
            Vector3 worldPos = _gridSystem.GetWorldPosition(gridPos.x, gridPos.y);
            UpdateGhostPosition(gridPos, worldPos);
        }
    }
    public bool TryPlaceBuilding_MassBuild(Vector2Int gridPos)
    {
        // Проверяем, в режиме "Проектов" ли мы
        if (BlueprintManager.IsActive && _buildingToMove == null)
        {
            // "Проекты" не тратят ресурсы, они всегда "успех"
            PlaceBlueprint(gridPos);
            return true;
        }
        else
        {
            // "Реальная" постройка.
            // PlaceRealBuilding сам проверит CanAfford/Золото и вернет 'false', если денег нет.
            return PlaceRealBuilding(gridPos);
        }
    }
    public bool PlaceBuildingFromOrder(BuildingData data, Vector2Int gridPos, float rotation, bool isBlueprint)
    {
        // 1. Проверка Ресурсов (если это НЕ "Проект")
        if (!isBlueprint)
        {
            if (!_resourceManager.CanAfford(data))
            {
                _notificationManager?.ShowNotification($"Недостаточно ресурсов для: {data.buildingName}");
                return false; // "СТОП" (Конвейер Т-12)
            }
        }

        // 2. "Вычисляем" размер
        Vector2Int size = data.size;
        if (Mathf.Abs(rotation - 90f) < 1f || Mathf.Abs(rotation - 270f) < 1f)
        {
            size = new Vector2Int(data.size.y, data.size.x);
        }

        // 3. "Вычисляем" Мировую Позицию (как в PlaceBlueprint)
        Vector3 worldPos = _gridSystem.GetWorldPosition(gridPos.x, gridPos.y);
        float cellSize = _gridSystem.GetCellSize();
        worldPos.x += (size.x * cellSize) / 2f;
        worldPos.z += (size.y * cellSize) / 2f;

        // 4. "Спавним" и "Настраиваем"
        GameObject newBuilding = Instantiate(data.buildingPrefab, worldPos, Quaternion.Euler(0, rotation, 0));
        newBuilding.layer = LayerMask.NameToLayer("Buildings");
        newBuilding.tag = "Building";

        var identity = newBuilding.GetComponent<BuildingIdentity>();
        if (identity == null) identity = newBuilding.AddComponent<BuildingIdentity>();

        identity.buildingData = data;
        identity.rootGridPosition = gridPos;
        identity.yRotation = rotation;
        identity.isBlueprint = isBlueprint;

        // 5. "Обрабатываем" экономику и производство
        if (!isBlueprint)
        {
            // "Тратим" ресурсы
            _resourceManager.SpendResources(data);

            // "Добавляем" жилье
            if (data.housingCapacity > 0)
            {
                _populationManager.AddHousingCapacity(data.housingCapacity);
            }

            // "Включаем" производство
            var producer = newBuilding.GetComponent<ResourceProducer>();
            if (producer != null) producer.enabled = true;
        }
        else
        {
            // "Выключаем" производство (для "Проектов")
            var producer = newBuilding.GetComponent<ResourceProducer>();
            if (producer != null) producer.enabled = false;
        }

        // 6. "Занимаем" сетку
        _gridSystem.OccupyCells(identity, size);

        // 7. "Красим" (в "реальный" или "синий")
        SetBuildingVisuals(newBuilding, isBlueprint ? VisualState.Blueprint : VisualState.Real, true);

        return true; // "УСПЕХ"
    }
    // --- КОНЕЦ: НОВОГО КОДА ---
    
    // (УДАЛЕНЫ МЕТОДЫ SETMODENONE() И SWITCHSTATE())
    private void GhostifyBuilding(GameObject building, bool makeGhost)
    {
        if (building == null) return;

        // 1. Настраиваем ВСЕ коллайдеры (включая дочерние)
        var colliders = building.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            // "Призрак" = триггер, "Живой" = не триггер
            col.isTrigger = makeGhost;
        }

        if (makeGhost)
        {
            // --- ПРЕВРАЩАЕМ В ПРИЗРАКА ---

            // 2. Добавляем Rigidbody (обязателен для OnTrigger... событий)
            var rb = building.GetComponent<Rigidbody>();
            if (rb == null) rb = building.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Он не должен падать

            // 3. Добавляем скрипт-детектор коллизий
            if (building.GetComponent<GhostBuildingCollider>() == null)
            {
                building.AddComponent<GhostBuildingCollider>();
            }
        }
        else
        {
            // --- ВОЗВРАЩАЕМ В "ЖИВОЕ" СОСТОЯНИЕ ---

            // 2. Убираем скрипт-детектор
            var ghostCol = building.GetComponent<GhostBuildingCollider>();
            if (ghostCol != null)
            {
                Destroy(ghostCol);
            }
                
            // 3. Убираем Rigidbody (зданиям он не нужен)
            var rb = building.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb); 
            }
        }
    }
}
