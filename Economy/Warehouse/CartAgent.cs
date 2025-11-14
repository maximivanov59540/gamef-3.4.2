// --- Файл: CartAgent.cs ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(BuildingIdentity))] // Для CartPathVisualizer
public class CartAgent : MonoBehaviour
{
    private enum State
    {
        Idle,
        Loading,
        FindingTarget,
        GoingToConsumer,
        GoingToWarehouse,
        Unloading,
        Returning,
        Queueing // <-- НОВОЕ СОСТОЯНИЕ
    }
    private State _state = State.Idle;

    [Header("Настройки Логистики")]
    [Tooltip("Скорость движения (юнитов/сек)")]
    public float moveSpeed = 5f;
    [Tooltip("Время (сек) на погрузку и разгрузку")]
    public float loadingTime = 2.0f;
    [Tooltip("Макс. радиус (в клетках дороги) для поиска 'Запроса'")]
    public float roadRadius = 50f;

    // --- Ссылки на системы ---
    private BuildingOutputInventory _homeOutputInv; // Наш "домашний" склад (продукция)
    private Transform _homeBase;                    // Transform "дома"
    private Vector2Int _homeBaseCell;               // Клетка "дома"
    
    private LogisticsManager _logistics;
    private ResourceManager _resourceManager;
    private GridSystem _gridSystem;
    private RoadManager _roadManager;
    
    // --- Данные о пути ---
    private List<Vector2Int> _currentPath;
    private int _pathIndex;
    private Vector3 _targetPosition; // Мировая позиция, к которой едем

    // --- Состояние Груза ---
    private float _amountCarrying = 0;
    private ResourceType _resourceCarrying;
    
    // --- Цель ---
    private ResourceRequest _targetRequest; // (Если едем к "заказчику")
    private Warehouse _targetWarehouse; // (Если едем на "склад")

    void Start()
    {
        // 1. Находим "дом" (родительский объект)
        _homeBase = transform.parent;
        if (_homeBase == null)
        {
            Debug.LogError("CartAgent должен быть дочерним!", this);
            enabled = false; return;
        }

        // 2. Находим "выходной" инвентарь на "доме"
        _homeOutputInv = _homeBase.GetComponent<BuildingOutputInventory>();
        if (_homeOutputInv == null)
        {
            Debug.LogError($"На базе {transform.parent.name} нет BuildingOutputInventory!", this);
            enabled = false; return;
        }

        // 3. Находим глобальные менеджеры
        _logistics = LogisticsManager.Instance;
        _resourceManager = ResourceManager.Instance;
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance;
        
        // 4. Запоминаем "адрес" дома
        _gridSystem.GetXZ(_homeBase.position, out int hx, out int hz);
        _homeBaseCell = new Vector2Int(hx, hz);

        transform.position = _homeBase.position;
    }

    void Update()
    {
        // Главный "мозг" FSM
        switch (_state)
        {
            case State.Idle:
                // Ждем, пока дома появится товар
                if (_homeOutputInv.HasAtLeastOneUnit())
                {
                    SetState(State.Loading);
                }
                break;
                
            case State.FindingTarget:
                // "Думаем", куда ехать (этот стейт срабатывает 1 раз)
                FindBestTarget();
                break;
                
            // --- ⬇️ НОВЫЙ CASE ⬇️ ---
            case State.Queueing:
                // Мы "стоим" у склада и "ждем"
                if (_targetWarehouse != null && _targetWarehouse.RequestUnload(this))
                {
                    // "Нас пропустили!"
                    SetState(State.Unloading);
                }
                // (Если не пропустили - просто ждем в этом стейте)
                break;
            // --- ⬆️ КОНЕЦ ⬆️ ---
                
            case State.GoingToConsumer:
            case State.GoingToWarehouse:
            case State.Returning:
                // В пути - просто едем
                FollowPath();
                break;
                
            // (Loading и Unloading управляются Корутинами,
            //  поэтому им не нужен код в Update)
        }
    }

    /// <summary>
    /// Центральный метод смены состояний (для отладки)
    /// </summary>
    private void SetState(State newState)
    {
        if (_state == newState) return;
        
        // Debug.Log($"Cart {gameObject.name} перешел в: {newState}");
        _state = newState;

        // "Входные" действия для состояний
        switch (_state)
        {
            case State.Loading:
                StartCoroutine(LoadCoroutine());
                break;
            case State.Unloading:
                StartCoroutine(UnloadCoroutine());
                break;
        }
    }

    /// <summary>
    /// (КОРУТИНА) "Грузим" товар дома
    /// </summary>
    private IEnumerator LoadCoroutine()
    {
        yield return new WaitForSeconds(loadingTime);
        
        // "Забираем" все
        _amountCarrying = _homeOutputInv.TakeAllResources();
        _resourceCarrying = _homeOutputInv.GetResourceType();
        
        SetState(State.FindingTarget); // Переходим к "раздумьям"
    }

    /// <summary>
    /// (КОРУТИНА) "Разгружаем" товар в точке назначения
    /// </summary>
    private IEnumerator UnloadCoroutine()
    {
        // --- ⬇️ ИЗМЕНЕНИЕ: Время ожидания ⬇️ ---
        // Берем таймер по умолчанию (для "Заказчика")
        float waitTime = loadingTime; 
        
        // Если мы на Складе - берем таймер ИЗ Склада
        if (_targetWarehouse != null) 
            waitTime = _targetWarehouse.unloadTime;
            
        yield return new WaitForSeconds(waitTime);
        // --- ⬆️ КОНЕЦ ИЗМЕНЕНИЯ ⬆️ ---

        if (_targetRequest != null)
        {
            // --- Приехали к "Заказчику" ---␊
            _targetRequest.Requester.AddResource(_resourceCarrying, _amountCarrying);
            _targetRequest = null; // Забываем про этот запрос␊
        }
        else if (_targetWarehouse != null)
        {
            // --- Приехали на "Склад" ---␊
            _resourceManager.AddToStorage(_resourceCarrying, _amountCarrying);
            // (Не сбрасываем _targetWarehouse до FinishUnload)
        }
        
        _amountCarrying = 0; // Руки пусты

        // --- ⬇️ НОВОЕ: Сообщаем складу, что мы уехали ⬇️ ---
        if (_targetWarehouse != null)
        {
            _targetWarehouse.FinishUnload(this); // "Сообщаем складу, что мы уехали"
            _targetWarehouse = null; // Забываем про этот склад
        }
        // --- ⬆️ КОНЕЦ ⬆️ ---

        // Едем домой
        if (FindPathTo(_homeBaseCell))
        {
            // 99% случаев: дом на месте, едем
            SetState(State.Returning);
        }
        else
        {
            // --- Шаг 2: "Медленный путь" (старый адрес не сработал) ---
            Debug.LogWarning($"[CartAgent] {gameObject.name} не нашел путь к старому дому ({_homeBaseCell}). Ищу новый адрес...");
            
            Vector2Int newHomeCell = GetCurrentHomeCell();
            
            // Пробуем найти путь к "свежему" адресу
            if (newHomeCell.x != -1 && FindPathTo(newHomeCell))
            {
                // Успех! Дом переехал, и мы нашли его.
                Debug.Log($"[CartAgent] {gameObject.name} нашел новый дом в {newHomeCell}!");
                
                // ВАЖНО: Обновляем кэш на будущее
                _homeBaseCell = newHomeCell; 
                SetState(State.Returning);
            }
            else
            {
                // Провал: Дом не найден или к нему *действительно* нет дороги.
                Debug.LogError($"[CartAgent] {gameObject.name} не смог найти путь домой ДАЖЕ к {newHomeCell}. Аварийная телепортация.");
                GoHomeAndIdle(); // Аварийный телепорт
            }
        }
    }

    /// <summary>
    /// (STATE) Ищем, куда везти наш груз
    /// </summary>
    private void FindBestTarget()
    {
        _gridSystem.GetXZ(transform.position, out int cx, out int cz);
        Vector2Int cartCell = new Vector2Int(cx, cz);

        // 1. ИЩЕМ "ЗАПРОС"
        _targetRequest = _logistics.GetBestRequest(cartCell, _resourceCarrying, roadRadius);

        if (_targetRequest != null)
        {
            // --- СЦЕНАРИЙ А: НАШЛИ "ЗАКАЗЧИКА" ---
            if (FindPathTo(_targetRequest.DestinationCell))
            {
                SetState(State.GoingToConsumer);
                return;
            }
            // (Если не нашли путь - игнорируем запрос)
        }
        
        // --- СЦЕНАРИЙ Б: НЕТ ЗАКАЗОВ (или нет пути) ---
        // Едем на ближайший склад
        _targetWarehouse = FindNearestWarehouse();
        if (_targetWarehouse != null)
        {
            // --- ⬇️ ИЗМЕНЕНИЕ: Используем BuildingIdentity склада ⬇️ ---
            var warehouseIdentity = _targetWarehouse.GetComponent<BuildingIdentity>();
            if (warehouseIdentity != null && FindPathTo(warehouseIdentity.rootGridPosition))
            {
                SetState(State.GoingToWarehouse);
                return;
            }
            // --- ⬆️ КОНЕЦ ⬆️ ---
        }
        
        // --- СЦЕНАРИЙ В: НЕТ НИ ЗАКАЗОВ, НИ СКЛАДОВ, НИ ПУТИ ---
        // (Остаемся в 'FindingTarget' и попробуем в след. кадре.
        // Или можно 'GoHomeAndIdle', но тогда тележка "застрянет" с товаром)
        // Давай пока останемся здесь и будем искать.
        Debug.LogWarning($"[CartAgent] {gameObject.name} не нашел ни запроса, ни склада. Жду...");
        // (Чтобы не спамить, можно добавить таймер, но пока так)
    }

    /// <summary>
    /// (STATE) Логика движения по точкам пути
    /// </summary>
    private void FollowPath()
    {
        if (_currentPath == null)
        {
            GoHomeAndIdle(); // Путь потерялся, паника!
            return;
        }
        // 1. Узнаем, на какой "клетке" мы сейчас
        // (Мы берем клетку, из которой "вышли", т.к. _pathIndex указывает,
        // куда мы "идем", а мы едем "по" клетке)
        Vector2Int currentCell;
        if (_pathIndex > 0 && _pathIndex <= _currentPath.Count)
            currentCell = _currentPath[_pathIndex - 1]; // Клетка, из которой вышли
        else
            currentCell = _currentPath[0]; // Самая первая клетка (старт)

        // 2. Проверяем "тип" дороги в этой клетке
        RoadTile currentTile = _gridSystem.GetRoadTileAt(currentCell.x, currentCell.y);

        // (1.0 = скорость по "земле" (если вдруг сошли) или обычная дорога)
        float currentMultiplier = 1.0f;

        if (currentTile != null && currentTile.roadData != null) // <-- (1) Добавили .roadData != null
        {
            currentMultiplier = currentTile.roadData.speedMultiplier; // <-- (2) Читаем из .roadData
        }
        Vector3 newPos = Vector3.MoveTowards(transform.position, _targetPosition, moveSpeed * currentMultiplier * Time.deltaTime);

        transform.position = newPos;
        
        Vector3 direction = (_targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        // Проверяем, достигли ли мы текущей точки
        if (Vector3.Distance(transform.position, _targetPosition) < 0.1f)
        {
            // Мы достигли точки _pathIndex. Ищем следующую.
            _pathIndex++;

            if (_pathIndex >= _currentPath.Count)
            {
                // --- МЫ ДОСТИГЛИ КОНЦА ПУТИ ---
                OnPathFinished();
            }
            else
            {
                // --- Устанавливаем следующую точку ---
                SetNewTargetNode();
            }
        }
    }

    /// <summary>
    /// Вызывается, когда мы дошли до конца _currentPath
    /// </summary>
    private void OnPathFinished()
    {
        // --- ⬇️ ИЗМЕНЕННАЯ ЛОГИКА ⬇️ ---
        if (_state == State.GoingToConsumer)
        {
            // Приехали к "Заказчику" -> Всегда сразу разгружаемся
            SetState(State.Unloading);
        }
        else if (_state == State.GoingToWarehouse)
        {
            // Приехали на "Склад" -> Пробуем "запросить" место
            bool canUnload = _targetWarehouse.RequestUnload(this);
            if (canUnload)
            {
                // "Место есть, проезжай"
                SetState(State.Unloading);
            }
            else
            {
                // "Мест нет, встань в очередь"
                SetState(State.Queueing);
            }
        }
        else if (_state == State.Returning)
        {
            // Вернулись ПУСТЫМ -> Идем отдыхать
            SetState(State.Idle);
        }
        // --- ⬆️ КОНЕЦ ⬆️ ---
    }

    /// <summary>
    /// Сбрасывает состояние в "Idle" и телепортирует домой.
    /// </summary>
    private void GoHomeAndIdle()
    {
        SetState(State.Idle);
        transform.position = _homeBase.position;
        _currentPath = null;
        _pathIndex = 0;
        _amountCarrying = 0;
        _targetRequest = null;
        _targetWarehouse = null;
    }
    
    // --- ХЕЛПЕРЫ ПОИСКА ПУТИ ---

    /// <summary>
    /// Главный "прокладчик" маршрута. Находит путь от "сейчас" до "цели".
    /// </summary>
    /// <returns>True, если путь найден и сохранен в _currentPath</returns>
    private bool FindPathTo(Vector2Int destinationCell)
    {
        var roadGraph = _roadManager.GetRoadGraph();
        if (roadGraph == null || roadGraph.Count == 0) return false;

        _gridSystem.GetXZ(transform.position, out int sx, out int sz);
        Vector2Int startBuildingCell = new Vector2Int(sx, sz); // Где мы сейчас

        // 1. Находим ВСЕ "выходы" из нашей текущей точки
        // ⬇️ ⬇️ ⬇️ ИЗМЕНЕНИЕ 1 ⬇️ ⬇️ ⬇️
        List<Vector2Int> startAccessPoints = LogisticsPathfinder.FindAllRoadAccess(startBuildingCell, _gridSystem, roadGraph);
        if (startAccessPoints.Count == 0)
        {
            Debug.LogWarning($"[CartAgent] {gameObject.name} (в {startBuildingCell}) не стоит у дороги.");
            return false;
        }

        // 2. Находим ВСЕ "входы" в нашу цель
        List<Vector2Int> endAccessPoints = LogisticsPathfinder.FindAllRoadAccess(destinationCell, _gridSystem, roadGraph);
        if (endAccessPoints.Count == 0)
        {
            Debug.LogWarning($"[CartAgent] {gameObject.name} не может найти дорогу к цели {destinationCell}.");
            return false;
        }

        // 3. Запускаем "широкий" поиск от ВСЕХ наших "выходов"
        var distances = LogisticsPathfinder.Distances_BFS_Multi(startAccessPoints, 1000, roadGraph); // 1000 = "бесконечный" радиус

        // 4. Ищем "лучший" (ближайший) "вход" в цель, который мы можем достичь
        Vector2Int bestEndCell = new Vector2Int(-1, -1);
        int minDistance = int.MaxValue;

        foreach (var endCell in endAccessPoints)
        {
            if (distances.TryGetValue(endCell, out int dist))
            {
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestEndCell = endCell;
                }
            }
        }

        // 5. Если мы не нашли ни одного "входа" (все на других островах)
        if (bestEndCell.x == -1)
        {
            Debug.LogWarning($"[CartAgent] {gameObject.name} нашел {endAccessPoints.Count} дорог у {destinationCell}, но ни одна не достижима с {startBuildingCell}. 'Острова'.");
            return false;
        }
        
        // 6. Мы нашли лучший путь! Теперь строим его.
        _currentPath = null;
        foreach(var startCell in startAccessPoints)
        {
             // Пытаемся построить путь от этой точки старта до нашей лучшей точки финиша
             var path = LogisticsPathfinder.FindActualPath(startCell, bestEndCell, roadGraph);
             if (path != null)
             {
                 _currentPath = path; // Нашли!
                 break; // Выходим из цикла, нам достаточно одного пути
             }
        }
        // ⬆️ ⬆️ ⬆️ КОНЕЦ ИЗМЕНЕНИЙ ⬆️ ⬆️ ⬆️

        if (_currentPath != null && _currentPath.Count > 0)
        {
            // "Достраиваем" путь (от здания до дороги и от дороги до здания)
            if (startBuildingCell != _currentPath[0])
                _currentPath.Insert(0, startBuildingCell);

            if (destinationCell != _currentPath[_currentPath.Count - 1])
                _currentPath.Add(destinationCell);

            _pathIndex = 0;
            SetNewTargetNode();
            return true;
        }

        Debug.LogError($"[CartAgent] {gameObject.name} нашел 'bestEndCell' {bestEndCell}, но FindActualPath не смог построить маршрут? Этого не должно было случиться.");
        return false;
    }
    private Warehouse FindNearestWarehouse()
    {
        Warehouse[] allWarehouses = FindObjectsByType<Warehouse>(FindObjectsSortMode.None);
        if (allWarehouses.Length == 0) return null;
        
        Warehouse nearest = null;
        float minDst = float.MaxValue;
        foreach(var wh in allWarehouses)
        {
            float dst = Vector3.Distance(transform.position, wh.transform.position);
            if(dst < minDst)
            {
                minDst = dst;
                nearest = wh;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Устанавливает _targetPosition на основе текущего _pathIndex
    /// </summary>
    private void SetNewTargetNode()
    {
        Vector2Int targetCell = _currentPath[_pathIndex];
        _targetPosition = _gridSystem.GetWorldPosition(targetCell.x, targetCell.y);

float offset = _gridSystem.GetCellSize() / 2f;
    _targetPosition.x += offset;
    _targetPosition.z += offset;

        // (Поднимаем чуть-чуть, чтобы не ехать "в" земле)
        _targetPosition.y += 0.1f; 
    }
    
    // (Этот код скопирован из LogisticsManager)
    
    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ 'CartPathVisualizer' ---

    public bool IsBusy()
    {
        return _state != State.Idle && _state != State.Loading;
    }

    /// <summary>
    /// Возвращает оставшийся путь в мировых координатах.
    /// </summary>
    public List<Vector3> GetRemainingPathWorld()
    {
        var pathPoints = new List<Vector3>();
        if (!IsBusy() || _currentPath == null) return pathPoints;

        // 1. Текущая позиция тележки
        pathPoints.Add(transform.position);
        
        // 2. Текущая цель (куда едем сейчас)
        pathPoints.Add(_targetPosition);
        
        // 3. Все оставшиеся точки
        for (int i = _pathIndex + 1; i < _currentPath.Count; i++)
        {
            var cell = _currentPath[i];
            var pos = _gridSystem.GetWorldPosition(cell.x, cell.y);
            pos.y += 0.1f; // (Тот же 'yOffset')
            pathPoints.Add(pos);
        }

        return pathPoints;
    }
    private Vector2Int GetCurrentHomeCell()
    {
        if (_homeBase == null) return new Vector2Int(-1, -1);
        
        var identity = _homeBase.GetComponent<BuildingIdentity>();
        if (identity != null)
        {
            return identity.rootGridPosition;
        }

        // Аварийный фолбэк (старая логика), если identity вдруг нет
        _gridSystem.GetXZ(_homeBase.position, out int hx, out int hz);
        return new Vector2Int(hx, hz);
    }
}