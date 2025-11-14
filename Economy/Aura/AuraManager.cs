using UnityEngine;
using System.Collections.Generic;

public class AuraManager : MonoBehaviour
{
    [SerializeField] private RoadCoverageVisualizer _coverage;
    public static AuraManager Instance { get; private set; }

    private GridSystem _gridSystem;
    private RoadManager _roadManager;
    private BuildingIdentity _identity;

    // --- НОВОЕ: Массив для проверки соседей ---
    private readonly Vector2Int[] _neighborOffsets = 
    {
        new Vector2Int(0, 1), // Север
        new Vector2Int(0, -1), // Юг
        new Vector2Int(1, 0), // Восток
        new Vector2Int(-1, 0)  // Запад
    };
    // ------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance != null ? RoadManager.Instance : FindFirstObjectByType<RoadManager>();
        if (_coverage == null) _coverage = FindFirstObjectByType<RoadCoverageVisualizer>();
        
    }

    // ── регистрация эмиттеров ─────────────────────────────────
    private readonly List<AuraEmitter> _allEmitters = new List<AuraEmitter>();

    public void RegisterEmitter(AuraEmitter emitter)
    {
        if (!_allEmitters.Contains(emitter)) _allEmitters.Add(emitter);
    }

    public void UnregisterEmitter(AuraEmitter emitter)
    {
        if (_allEmitters.Contains(emitter)) _allEmitters.Remove(emitter);
    }

    // ── НОВОЕ: мост к логистике по дорогам ────────────────────
    public bool HasRoadAccess(Vector2Int startCell, Vector2Int endCell)
    {
        if (_roadManager == null) return false;
        return LogisticsPathfinder.HasPath_BFS(startCell, endCell, _roadManager.GetRoadGraph());
    }

    /// Единый метод проверки ауры.
    /// Radial — старая круговая логика.
    /// RoadBased — доступность через сеть дорог.
    /// Единый метод проверки ауры. (ОТЛАДОЧНАЯ ВЕРСИЯ)
    /// Единый метод проверки ауры. (ОТЛАДОЧНАЯ ВЕРСИЯ 2.0)
    public bool IsPositionInAura(Vector3 worldPos, AuraType type)
    {
        foreach (AuraEmitter emitter in _allEmitters)
        {
            if (emitter == null || emitter.type != type) continue;

            // --- Логика 'Radial' ---
            if (emitter.distributionType == AuraDistributionType.Radial)
            {
                float dist = Vector3.Distance(worldPos, emitter.transform.position);
                if (dist <= emitter.radius) return true;
            }
            // --- Логика 'RoadBased' ---
            else if (emitter.distributionType == AuraDistributionType.RoadBased)
            {
                if (_gridSystem == null || _roadManager == null) continue;

                // 1. Получаем 'BuildingIdentity' ЭМИТТЕРА (Рынка)
                BuildingIdentity emitterIdentity = emitter.GetIdentity();
                if (emitterIdentity == null) continue;

                Vector2Int emitterRoadCell = GetRoadAccessCell(emitterIdentity);
                if (emitterRoadCell.x == -1) continue; // Эмиттер не у дороги

                // 2. Получаем 'BuildingIdentity' ПРИЕМНИКА (Дома)
                _gridSystem.GetXZ(worldPos, out int gx, out int gz);
                Vector2Int receiverRoot = new Vector2Int(gx, gz);

                BuildingIdentity receiverIdentity = _gridSystem.GetBuildingIdentityAt(receiverRoot.x, receiverRoot.y);
                if (receiverIdentity == null) continue; // Это не здание

                Vector2Int receiverRoadCell = GetRoadAccessCell(receiverIdentity);
                if (receiverRoadCell.x == -1) continue; // Дом не у дороги

                // 3. Ищем путь
                if (emitterRoadCell == receiverRoadCell) return true;
                if (HasRoadAccess(emitterRoadCell, receiverRoadCell))
                {
                    return true; // Путь найден!
                }
            }
        }

        return false; // Ни один эмиттер не подошел
    }
    public void ShowRoadAura(AuraEmitter emitter)
    {
        Debug.Log($"[AuraManager] ShowRoadAura called. Emitter={emitter?.name} root={emitter?.GetRootPosition()} coverage={_coverage}");
        if (emitter == null) return;
        if (_coverage == null) _coverage = FindFirstObjectByType<RoadCoverageVisualizer>();
        emitter.RefreshRootCell();
        _coverage?.ShowForEmitter(emitter);
    }

    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД (Теперь "умный") ⬇️ ---
    public void ShowRoadAuraPreview(Vector3 worldPos, Vector2Int rootGridPos, Vector2Int baseSize, float rotation, float radius)
    {
        if (_coverage == null) _coverage = FindFirstObjectByType<RoadCoverageVisualizer>();

        // 1. Проверяем, касается ли "тень" дороги
        bool isTouchingRoad = CheckIfTouchingRoad(rootGridPos, baseSize, rotation);

        // 2. Если да — показываем превью. Если нет — прячем.
        if (isTouchingRoad)
        {
            // Используем 'worldPos' (центр), который нам передали,
            // т.к. визуализатор ожидает именно его.
            _coverage?.ShowPreview(worldPos, radius); 
        }
        else
        {
            // Прячем, если не касаемся (это решает баг "убирания" тени)
            _coverage?.HidePreview(); 
        }
    }
    // --- ⬆️ КОНЕЦ ИЗМЕНЕНИЙ ⬆️ ---


    public void HideRoadAuraOverlay()
    {
        Debug.Log("[AuraManager] HideRoadAuraOverlay");
        if (_coverage == null) _coverage = FindFirstObjectByType<RoadCoverageVisualizer>();
        _coverage?.HideAll();
    }

    public void HideRoadAuraPreview()
    {
        if (_coverage == null) _coverage = FindFirstObjectByType<RoadCoverageVisualizer>();
        _coverage?.HidePreview();
    }


    // --- ⬇️ НОВЫЕ ПРИВАТНЫЕ МЕТОДЫ ⬇️ ---

    /// <summary>
    /// Главная логика: проверяет, касается ли "тень" здания дороги.
    /// </summary>
    private bool CheckIfTouchingRoad(Vector2Int rootPos, Vector2Int baseSize, float rotation)
    {
        if (_roadManager == null) return false;
        if (rootPos.x == -1) return false; // Мышь за пределами сетки

        Vector2Int rotatedSize = GetRotatedSize(baseSize, rotation);

        // Итерация по ВСЕМ клеткам "футпринта" (основания) здания
        for (int x = 0; x < rotatedSize.x; x++)
        {
            for (int z = 0; z < rotatedSize.y; z++)
            {
                Vector2Int currentCell = new Vector2Int(rootPos.x + x, rootPos.y + z);
                
                // Проверяем 4-х соседей ЭТОЙ клетки
                foreach (var offset in _neighborOffsets)
                {
                    Vector2Int neighborCell = currentCell + offset;

                    // !!! ВАШЕ ДОПУЩЕНИЕ !!!
                    // Убедитесь, что у RoadManager есть метод IsRoadAt(Vector2Int)
                    // Если он называется иначе, замените "IsRoadAt" здесь:
                    if (_gridSystem.GetRoadTileAt(neighborCell.x, neighborCell.y) != null)
                    {
                        return true; // Нашли дорогу!
                    }
                }
            }
        }
        return false; // Дорог не найдено
    }

    /// <summary>
    /// Хелпер: вычисляет реальный размер на сетке с учетом поворота.
    /// </summary>
    private Vector2Int GetRotatedSize(Vector2Int size, float rotation)
    {
        // Проверяем, повернуто ли здание на 90 или 270 градусов
        if (Mathf.Abs(rotation - 90f) < 1f || Mathf.Abs(rotation - 270f) < 1f)
        {
            return new Vector2Int(size.y, size.x); // Инвертируем размер
        }
        return size; // Поворот 0 или 180, размер тот же
    }
    /// <summary>
    /// НОВЫЙ "УМНЫЙ" ПОИСК ДОРОГИ.
    /// Проверяет ВЕСЬ периметр здания на доступ к дороге.
    /// </summary>
    private Vector2Int GetRoadAccessCell(BuildingIdentity building)
    {
        var notFound = new Vector2Int(-1, -1);
        if (building == null) return notFound; // Здания нет
        if (_roadManager == null || _gridSystem == null) return notFound;

        Vector2Int rootPos = building.rootGridPosition;
        Vector2Int size = building.buildingData.size; // <-- Получаем реальный размер
        float rotation = building.yRotation; // <-- Получаем поворот

        // Вычисляем "повернутый" размер (этот метод у тебя уже есть)
        Vector2Int rotatedSize = GetRotatedSize(size, rotation);

        // Итерация по ВСЕМ клеткам "футпринта" (основания) здания
        for (int x = 0; x < rotatedSize.x; x++)
        {
            for (int z = 0; z < rotatedSize.y; z++)
            {
                Vector2Int currentCell = new Vector2Int(rootPos.x + x, rootPos.y + z);

                // Проверяем 4-х соседей ЭТОЙ клетки
                // (_neighborOffsets у тебя уже есть)
                foreach (var offset in _neighborOffsets)
                {
                    Vector2Int neighborCell = currentCell + offset;

                    if (_gridSystem.GetRoadTileAt(neighborCell.x, neighborCell.y) != null)
                    {
                        return neighborCell; // Нашли дорогу!
                    }
                }
            }
        }

        return notFound; // Дорог не найдено
    }
}