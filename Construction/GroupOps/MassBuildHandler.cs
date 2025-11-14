using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MassBuildHandler : MonoBehaviour
{
    public static MassBuildHandler Instance { get; private set; }

    [Header("Ссылки на Отделы")]
    [SerializeField] private GridSystem _gridSystem;
    [SerializeField] private BuildingManager _buildingManager;
    // [SerializeField] private BlueprintManager _blueprintManager;

    private List<GameObject> _ghostPool = new List<GameObject>();
    private int _ghostPoolIndex = 0;

    // "Память" о "Заказе"
    private BuildingData _currentData;
    private float _currentRotation;
    private Vector2Int _currentRotatedSize;

    // --- НОВОЕ: "Память" для "Исполнения" (Т-12) ---
    private List<Vector2Int> _currentBuildCells = new List<Vector2Int>();
    private List<bool> _currentBuildValidity = new List<bool>();
    // --- КОНЕЦ НОВОГО ---

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void StartMassBuildPreview(BuildingData data, float rotation)
    {
        if (data == null) return;

        _currentData = data;
        _currentRotation = rotation;

        _currentRotatedSize = data.size;
        if (Mathf.Abs(_currentRotation - 90f) < 1f || Mathf.Abs(_currentRotation - 270f) < 1f)
        {
            _currentRotatedSize = new Vector2Int(data.size.y, data.size.x);
        }
    }

    // --- ИЗМЕНЕНО: Теперь "запоминает" "заказ" ---
    // --- ЗАМЕНИТЬ ВЕСЬ МЕТОД ---
    public void UpdateMassBuildPreview(Vector2Int startPos, Vector2Int endPos)
    {
        if (_currentData == null) return; 

        _ghostPoolIndex = 0;
        _currentBuildCells.Clear();
        _currentBuildValidity.Clear();
        
        float cellSize = _gridSystem.GetCellSize();
        
        // --- ЛОГИКА "УЛИЦЫ" (Т-3) + "Фикс 'Съезжания'" ---
        
        // 1. "Определяем" направление "драга" (E-W или N-S)
        int dx = Mathf.Abs(endPos.x - startPos.x);
        int dz = Mathf.Abs(endPos.y - startPos.y);

        if (dx == 0 && dz == 0)
        {
            // Это не "драг", а "одиночный клик".
            // "Обрабатываем" "только" ОДИН "призрак" "в" "начальной" "точке".
            ProcessGhostPreview(startPos, cellSize);
            HideUnusedGhosts();
            return; // "Выходим", "чтобы" "не" "рисовать" "улицу"
        }

        // 2. "Определяем" направление "шага" (вперед или назад)
        int x_dir = (endPos.x >= startPos.x) ? 1 : -1;
        int z_dir = (endPos.y >= startPos.y) ? 1 : -1;

        if (dx > dz) // --- Улица "Восток-Запад" ---
        {
            // "Шаг" по X (длина)
            int stepX = _currentRotatedSize.x; 
            // "Шаг" по Z (ширина/ряды)
            int stepZ = _currentRotatedSize.y; 

            // "Длина" улицы
            for (int i = 0; i <= (dx / stepX); i++)
            {
                // "Корень" X "жестко" "якорится" в startPos
                int x = startPos.x + (i * stepX * x_dir);

                // "Ширина" (Ряд 1)
                int z1 = startPos.y;
                ProcessGhostPreview(new Vector2Int(x, z1), cellSize);
                
                // "Ширина" (Ряд 2)
                int z2 = startPos.y + (stepZ * z_dir); // (или +3)
                ProcessGhostPreview(new Vector2Int(x, z2), cellSize);
            }
        }
        else // --- Улица "Север-Юг" ---
        {
            // "Шаг" по X (ширина/ряды)
            int stepX = _currentRotatedSize.x;
            // "Шаг" по Z (длина)
            int stepZ = _currentRotatedSize.y; 

            // "Длина" улицы
            for (int i = 0; i <= (dz / stepZ); i++)
            {
                // "Корень" Z "жестко" "якорится" в startPos
                int z = startPos.y + (i * stepZ * z_dir);

                // "Ширина" (Ряд 1)
                int x1 = startPos.x;
                ProcessGhostPreview(new Vector2Int(x1, z), cellSize);

                // "Ширина" (Ряд 2)
                int x2 = startPos.x + (stepX * x_dir); // (или +3)
                ProcessGhostPreview(new Vector2Int(x2, z), cellSize);
            }
        }
        
        HideUnusedGhosts();
    }

    // --- ИЗМЕНЕНО: Теперь "чистит" и "память" ---
    public void ClearMassBuildPreview()
    {
        _ghostPoolIndex = 0;
        HideUnusedGhosts();
        _currentData = null;
        _currentBuildCells.Clear();
        _currentBuildValidity.Clear();
    }

    // --- НОВЫЙ МЕТОД: "ИСПОЛНЕНИЕ" (Т-12) ---
    public void ExecuteMassBuild()
    {
        if (_currentData == null) return;

        int successCount = 0;

        // "Запускаем" "Конвейер"
        for (int i = 0; i < _currentBuildCells.Count; i++)
        {
            // "Фильтр Т-13 ('Дырки')":
            if (_currentBuildValidity[i] == true)
            {
                // "Конвейер Т-12 ('Ресурсы')":
                // "Стучим" в "Прораба". Он сам проверит ресурсы.
                bool success = _buildingManager.TryPlaceBuilding_MassBuild(_currentBuildCells[i]);

                if (!success)
                {
                    // "СТОП" (Ресурсы кончились).
                    // (BuildingManager сам покажет "Недостаточно ресурсов")
                    break;
                }
                successCount++;
            }
        }

        Debug.Log($"Построено: {successCount} зданий.");
        ClearMassBuildPreview(); // "Убираем" за собой
    }

    // --- (Хелперы "Пула" GetGhostFromPool/HideUnusedGhosts остаются без изменений) ---
    private GameObject GetGhostFromPool()
    {
        GameObject ghost;
        if (_ghostPoolIndex < _ghostPool.Count)
        {
            ghost = _ghostPool[_ghostPoolIndex];
        }
        else
        {
            // --- НАЧАЛО ФИКСА #7 (Это и есть решение бага #2) ---
            ghost = Instantiate(_currentData.buildingPrefab, transform);

            // 1. Выключаем "мозги" (логику)
            var producer = ghost.GetComponent<ResourceProducer>();
            if (producer != null) producer.enabled = false;

            var identity = ghost.GetComponent<BuildingIdentity>();
            if (identity != null) identity.enabled = false;

            // 2. Настраиваем "физику" (чтобы не мешал)
            ghost.layer = LayerMask.NameToLayer("Ghost");
            ghost.tag = "Untagged"; // Снимаем тег "Building"

            // 3. Убеждаемся, что коллайдеры - триггеры
            var colliders = ghost.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.isTrigger = true;
            }

            // 4. Добавляем Rigidbody (если его нет), чтобы триггеры работали
            var rb = ghost.GetComponent<Rigidbody>();
            if (rb == null) rb = ghost.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            _ghostPool.Add(ghost);
            // --- КОНЕЦ ФИКСА #7 ---
        }

        ghost.SetActive(true);
        _ghostPoolIndex++;
        return ghost;
    }

    private void HideUnusedGhosts()
    {
        for (int i = _ghostPoolIndex; i < _ghostPool.Count; i++)
        {
            _ghostPool[i].SetActive(false);
        }
    }
    private void ProcessGhostPreview(Vector2Int currentCell, float cellSize)
    {
        GameObject ghost = GetGhostFromPool();
        ghost.transform.rotation = Quaternion.Euler(0, _currentRotation, 0);

        // "Позиционируем" 3х3 призрак
        Vector3 worldPos = _gridSystem.GetWorldPosition(currentCell.x, currentCell.y);
        worldPos.x += (_currentRotatedSize.x * cellSize) / 2f;
        worldPos.z += (_currentRotatedSize.y * cellSize) / 2f;
        ghost.transform.position = worldPos;

        // "ЛОГИКА T-13 ('Дырки')"
        bool canBuild = _gridSystem.CanBuildAt(currentCell, _currentRotatedSize);

        // "ЗАПОМИНАЕМ" "Заказ"
        _currentBuildCells.Add(currentCell);
        _currentBuildValidity.Add(canBuild);

        // "Красим" 3х3 призрак
        var visuals = ghost.GetComponent<BuildingVisuals>();
        if (visuals != null)
        {
            // "Проверяем", "строим" "ли" "мы" "Проект" "или" "Реал"
            bool isBlueprintMode = BlueprintManager.IsActive;
            if (isBlueprintMode)
                visuals.SetState(VisualState.Blueprint, canBuild);
            else
                visuals.SetState(VisualState.Ghost, canBuild);
        }
    }
    // --- КОНЕЦ: НОВОГО КОДА ---
}