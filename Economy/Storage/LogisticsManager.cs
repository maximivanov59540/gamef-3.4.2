using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class LogisticsManager : MonoBehaviour
{
    public static LogisticsManager Instance { get; private set; }

    // --- Ссылки на системы ---
    private GridSystem _gridSystem;
    private RoadManager _roadManager;
    
    // --- "Доска Заказов" ---
    private readonly List<ResourceRequest> _activeRequests = new List<ResourceRequest>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // "Хватаем" системы, нужные для поиска пути
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance;
    }

    /// <summary>
    /// Здание-потребитель (InputInventory) "вешает" свой заказ на доску.
    /// </summary>
    public void CreateRequest(ResourceRequest request)
    {
        if (!_activeRequests.Contains(request))
        {
            _activeRequests.Add(request);
            Debug.Log($"[LogisticsManager] Новый запрос на {request.RequestedType} от {request.Requester.name} (Приоритет: {request.Priority})");
        }
    }

    /// <summary>
    /// Здание-потребитель (InputInventory) "снимает" свой заказ (т.к. склад полон).
    /// </summary>
    public void FulfillRequest(ResourceRequest request)
    {
        if (_activeRequests.Contains(request))
        {
            _activeRequests.Remove(request);
            Debug.Log($"[LogisticsManager] Запрос на {request.RequestedType} от {request.Requester.name} выполнен/отменен.");
        }
    }
    public ResourceRequest GetBestRequest(Vector2Int cartGridPos, ResourceType resourceToDeliver, float roadRadius)
    {
        if (_activeRequests.Count == 0 || _roadManager == null || _gridSystem == null)
            return null;

        var roadGraph = _roadManager.GetRoadGraph();
        if (roadGraph == null || roadGraph.Count == 0) return null;

        // 1. Находим ВСЕ "выходы" тележки
        // ⬇️ ⬇️ ⬇️ ИЗМЕНЕНИЕ 1 ⬇️ ⬇️ ⬇️
        List<Vector2Int> cartRoadCells = LogisticsPathfinder.FindAllRoadAccess(cartGridPos, _gridSystem, roadGraph);
        if (cartRoadCells.Count == 0)
        // ⬆️ ⬆️ ⬆️ ИЗМЕНЕНИЕ 1 ⬆️ ⬆️ ⬆️
        {
            return null; // Тележка сама не у дороги?
        }
            
        // 2. Фильтруем запросы ... (без изменений)
        var matchingRequests = _activeRequests.Where(r => r.RequestedType == resourceToDeliver).ToList();
        if (matchingRequests.Count == 0)
            return null; 

        // 3. Считаем расстояния от ВСЕХ "выходов" тележки
        int maxSteps = Mathf.FloorToInt(roadRadius);
        // ⬇️ ⬇️ ⬇️ ИЗМЕНЕНИЕ 2 ⬇️ ⬇️ ⬇️
        var distancesFromCart = LogisticsPathfinder.Distances_BFS_Multi(cartRoadCells, maxSteps, roadGraph);
        // ⬆️ ⬆️ ⬆️ ИЗМЕНЕНИЕ 2 ⬆️ ⬆️ ⬆️

        // 4. Собираем список "валидных" запросов
        var validRequests = new List<(ResourceRequest request, int distance)>();

        foreach (var req in matchingRequests)
        {
            // Находим ВСЕ "входы" для "заказчика"
            // ⬇️ ⬇️ ⬇️ ИЗМЕНЕНИЕ 3 ⬇️ ⬇️ ⬇️
            List<Vector2Int> destRoadCells = LogisticsPathfinder.FindAllRoadAccess(req.DestinationCell, _gridSystem, roadGraph);
            if (destRoadCells.Count == 0) continue; // Заказчик не у дороги

            // Ищем ЛУЧШИЙ "вход" (ближайший к тележке)
            int minDistance = int.MaxValue;
            bool foundAccess = false;

            foreach (var destCell in destRoadCells)
            {
                if (distancesFromCart.TryGetValue(destCell, out int dist))
                {
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        foundAccess = true;
                    }
                }
            }

            // Если хотя бы один "вход" достижим
            if (foundAccess)
            {
                validRequests.Add((req, minDistance));
            }
            // ⬆️ ⬆️ ⬆️ ИЗМЕНЕНИЕ 3 ⬆️ ⬆️ ⬆️
        }

        // 5. Сортируем... (без изменений)
        var sortedRequests = validRequests
            .OrderByDescending(r => r.request.Priority)
            .ThenBy(r => r.distance);

        // 6. Возвращаем... (без изменений)
        return sortedRequests.FirstOrDefault().request;
    }
}