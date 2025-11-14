using UnityEngine;
using System.Collections.Generic;
public class ResourceProducer : MonoBehaviour
{
    [Tooltip("Данные о 'рецепте' (время, затраты, выход)")]
    public ResourceProductionData productionData;
    
    private BuildingInputInventory _inputInv;
    private BuildingOutputInventory _outputInv;
    
    [Header("Бонусы от Модулей")]
    [Tooltip("Производительность = База * (1.0 + (Кол-во модулей * X))")]
    public float productionPerModule = 0.25f;

    private float _currentModuleBonus = 1.0f; // (Множитель, 1.0 = 100%)
    
    [Header("Эффективность")]
    private float _efficiencyModifier = 1.0f; // 100% по дефолту
    
    [Header("Состояние цикла")]
    [SerializeField]
    [Tooltip("Внутренний таймер. Накапливается до 'cycleTimeSeconds'")]
    private float _cycleTimer = 0f;
    
    public bool IsPaused { get; private set; } = false;
    [Header("Логистика Склада")]
    [SerializeField] private Warehouse _assignedWarehouse; // Склад, к которому мы "приписаны"
    private bool _hasWarehouseAccess = false; // Наш "пропуск" к работе
    
    private BuildingIdentity _identity;
    private GridSystem _gridSystem;
    private RoadManager _roadManager;

    void Awake()
    {
        _inputInv = GetComponent<BuildingInputInventory>();
        _outputInv = GetComponent<BuildingOutputInventory>();

        _identity = GetComponent<BuildingIdentity>();

        if (_inputInv == null && productionData != null && productionData.inputCosts.Count > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingInputInventory', но рецепт требует сырье!", this);
            
        if (_outputInv == null && productionData != null && productionData.outputYield.amount > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingOutputInventory', но рецепт производит товар!", this);
        
        if (_outputInv != null)
        {
            _outputInv.OnFull += PauseProduction;
            _outputInv.OnSpaceAvailable += ResumeProduction;
        }
    }
    void Start()
    {
        // Хватаем менеджеры, нужные для поиска пути
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance;
        
        // Запускаем поиск
        FindWarehouseAccess();
    }
    
    private void OnDestroy()
    {
        if (_outputInv != null)
        {
            _outputInv.OnFull -= PauseProduction;
            _outputInv.OnSpaceAvailable -= ResumeProduction;
        }
    }

    void Update()
    {
        // 1. Проверка Паузы (старая)
        if (IsPaused || productionData == null)
            return;
            
        // 2. НОВАЯ ПРОВЕРКА: Есть ли "пропуск" от склада?
        if (!_hasWarehouseAccess)
        {
            // У нас нет доступа к складу.
            // Мы используем IsPaused = true, чтобы BuildingStatusVisualizer
            // автоматически показал иконку "Нет склада" (или "Zzz")
            PauseProduction(); 
            return;
        }

        // 3. Считаем время цикла
        // (Учитываем все бонусы: Модули * Эффективность)
        float currentCycleTime = productionData.cycleTimeSeconds / (_currentModuleBonus * _efficiencyModifier);
        
        // 4. Накапливаем таймер
        _cycleTimer += Time.deltaTime;

        // 5. Ждем, пока таймер "дозреет"
        if (_cycleTimer < currentCycleTime)
        {
            return; // Еще не время
        }
        
        // --- 6. ВРЕМЯ ПРИШЛО! (Таймер сработал) ---
        _cycleTimer -= currentCycleTime; // Сбрасываем таймер (с учетом "сдачи")

        // 7. Проверяем "Желудок" (Input)
        // (null-check на _inputInv, т.к. Лесопилка его не имеет)
        if (_inputInv != null && !_inputInv.HasResources(productionData.inputCosts))
        {
            // Debug.Log($"[Producer] {gameObject.name} не хватает сырья.");
            return; // Нет сырья, ждем следующего цикла
        }

        // 8. Проверяем "Кошелек" (Output)
        // (null-check, если здание ничего не производит)
        if (_outputInv != null && !_outputInv.HasSpace(productionData.outputYield.amount))
        {
            PauseProduction(); // Склад полон
            return;
        }
        
        // --- 9. ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ! ПРОИЗВОДИМ! ---
        
        // а) "Съедаем" сырье
        if (_inputInv != null)
        {
            _inputInv.ConsumeResources(productionData.inputCosts);
        }
        
        // б) "Производим" товар
        if (_outputInv != null)
        {
            _outputInv.AddResource(productionData.outputYield.amount);
        }
    }
    private void FindWarehouseAccess()
    {
        // 1. Проверка систем
        if (_identity == null || _gridSystem == null || _roadManager == null)
        {
            Debug.LogError($"[Producer] {gameObject.name} не хватает систем для поиска пути!");
            _hasWarehouseAccess = false;
            return;
        }
        
        var roadGraph = _roadManager.GetRoadGraph();

        // 2. Найти наши "выходы" к дороге
        List<Vector2Int> myAccessPoints = LogisticsPathfinder.FindAllRoadAccess(_identity.rootGridPosition, _gridSystem, roadGraph);
        if (myAccessPoints.Count == 0)
        {
            Debug.LogWarning($"[Producer] {gameObject.name} не имеет доступа к дороге.");
            _hasWarehouseAccess = false;
            return;
        }

        // 3. Найти все склады
        Warehouse[] allWarehouses = FindObjectsByType<Warehouse>(FindObjectsSortMode.None);
        if (allWarehouses.Length == 0)
        {
            Debug.LogWarning($"[Producer] {gameObject.name} не нашел НИ ОДНОГО склада на карте.");
            _hasWarehouseAccess = false;
            return;
        }

        // 4. Рассчитать ВСЕ дистанции от НАС (1000 = "бесконечный" радиус)
        var distancesFromMe = LogisticsPathfinder.Distances_BFS_Multi(myAccessPoints, 1000, roadGraph);

        // 5. Найти ближайший доступный склад
        Warehouse nearestWarehouse = null;
        int minDistance = int.MaxValue;

        foreach (var warehouse in allWarehouses)
        {
            var warehouseIdentity = warehouse.GetComponent<BuildingIdentity>();
            if (warehouseIdentity == null) continue;
            
            // 6. Найти "входы" к ЭТОМУ складу
            List<Vector2Int> warehouseAccessPoints = LogisticsPathfinder.FindAllRoadAccess(warehouseIdentity.rootGridPosition, _gridSystem, roadGraph);
            
            // 7. Найти ближайшую точку "входа" в этот склад
            foreach (var entryPoint in warehouseAccessPoints)
            {
                // Если мы "дотягиваемся" до этой точки входа...
                if (distancesFromMe.TryGetValue(entryPoint, out int dist) && dist < minDistance)
                {
                    // ...это наш новый "лучший" кандидат
                    minDistance = dist;
                    nearestWarehouse = warehouse;
                }
            }
        }

        // 8. ФИНАЛЬНАЯ ПРОВЕРКА: Мы нашли склад И он в радиусе?
        if (nearestWarehouse != null && minDistance <= nearestWarehouse.roadRadius)
        {
            _assignedWarehouse = nearestWarehouse;
            _hasWarehouseAccess = true;
            Debug.Log($"[Producer] {gameObject.name} приписан к {nearestWarehouse.name} (Дистанция: {minDistance})");
        }
        else
        {
            _hasWarehouseAccess = false;
            if (nearestWarehouse != null)
                Debug.LogWarning($"[Producer] {gameObject.name} нашел {nearestWarehouse.name}, но он СЛИШКОМ ДАЛЕКО (Дист: {minDistance} > Радиус: {nearestWarehouse.roadRadius})");
            else
                Debug.LogWarning($"[Producer] {gameObject.name} не нашел ни одного *доступного* склада.");
        }
    }

    public void UpdateProductionRate(int moduleCount)
    {
        _currentModuleBonus = 1.0f + (moduleCount * productionPerModule);
        Debug.Log($"[Producer] {gameObject.name} обновил бонус. Модулей: {moduleCount}, Множитель: {_currentModuleBonus}x");
    }
    
    public void SetEfficiency(float normalizedValue)
    {
        _efficiencyModifier = normalizedValue;
    }
    public float GetEfficiency() => _efficiencyModifier;
    
    
    private void PauseProduction()
    {
        if (IsPaused) return;
        IsPaused = true;
        // Debug.Log($"Производство {gameObject.name} на ПАУЗЕ (склад полон).");
    }

    private void ResumeProduction()
    {
        if (!IsPaused) return;
        IsPaused = false;
        // Debug.Log($"Производство {gameObject.name} ВОЗОБНОВЛЕНО (место появилось).");
    }
}