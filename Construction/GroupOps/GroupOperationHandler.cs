using System.Collections.Generic;
using UnityEngine;

public class GroupOperationHandler : MonoBehaviour
{
    public static GroupOperationHandler Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private GridSystem _gridSystem;
    [SerializeField] private PlayerInputController _inputController;
    [SerializeField] private BuildingManager _buildingManager;

    private NotificationManager _notificationManager;

    // Пул призраков для предпросмотра копирования
    private readonly List<GameObject> _ghostPool = new();
    private int _ghostPoolIndex = 0;

    private struct GroupOffset
    {
        public BuildingData data;
        public Vector2Int offset;
        public float yRotationDelta;
        public bool isBlueprint;
    }

    private readonly List<GroupOffset> _currentGroupOffsets = new();
    private Vector2Int _anchorGridPos;
    private float _anchorRotation;
    private float _currentGroupRotation = 0f;
    private bool _canPlaceGroup = true;

    // Для массового перемещения «живых» зданий
    private readonly List<GameObject> _liftedBuildings = new();
    private readonly List<Vector2Int> _originalPositions = new();
    private readonly List<float> _originalRotations = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        _notificationManager = FindFirstObjectByType<NotificationManager>();
    }

    // --- Вспомогательные математики ---

    private static Vector2Int RotateVector(Vector2Int v, float angle)
    {
        int x = v.x, y = v.y;
        if (Mathf.Abs(angle - 90f) < 1f)  return new Vector2Int(-y,  x);
        if (Mathf.Abs(angle - 180f) < 1f) return new Vector2Int(-x, -y);
        if (Mathf.Abs(angle - 270f) < 1f) return new Vector2Int( y, -x);
        return v;
    }

    private static Vector2Int GetRotatedSize(Vector2Int size, float angle)
    {
        if (Mathf.Abs(angle - 90f) < 1f || Mathf.Abs(angle - 270f) < 1f)
            return new Vector2Int(size.y, size.x);
        return size;
    }

    // --------- Массовое копирование (D/E/F) ---------

    public void StartMassCopy(HashSet<BuildingIdentity> selection)
    {
        _currentGroupOffsets.Clear();
        _currentGroupRotation = 0f;

        // Находим «якорь»
        BuildingIdentity anchorId = null;
        int minPosSum = int.MaxValue;
        foreach (var id in selection)
        {
            int sum = id.rootGridPosition.x + id.rootGridPosition.y;
            if (sum < minPosSum) { minPosSum = sum; anchorId = id; }
        }
        if (anchorId == null) return;

        _anchorGridPos = anchorId.rootGridPosition;
        _anchorRotation = anchorId.yRotation;

        foreach (var id in selection)
        {
            _currentGroupOffsets.Add(new GroupOffset
            {
                data = id.buildingData,
                offset = id.rootGridPosition - _anchorGridPos,
                yRotationDelta = id.yRotation - _anchorRotation,
                isBlueprint = id.isBlueprint
            });
        }

        _inputController.SetMode(InputMode.GroupCopying);
    }

    public void UpdateGroupPreview(Vector2Int mouseGridPos)
    {
        _ghostPoolIndex = 0;
        _canPlaceGroup = true;

        float cellSize = _gridSystem.GetCellSize();

        foreach (var entry in _currentGroupOffsets)
        {
            Vector2Int rotatedOffset = RotateVector(entry.offset, _currentGroupRotation);
            Vector2Int finalPos = mouseGridPos + rotatedOffset;
            float finalRot = (_currentGroupRotation + entry.yRotationDelta) % 360f;
            Vector2Int finalSize = GetRotatedSize(entry.data.size, finalRot);

            GameObject ghost = GetGhostFromPool(entry.data);
            ghost.transform.rotation = Quaternion.Euler(0, finalRot, 0);

            Vector3 worldPos = _gridSystem.GetWorldPosition(finalPos.x, finalPos.y);
            worldPos.x += (finalSize.x * cellSize) / 2f;
            worldPos.z += (finalSize.y * cellSize) / 2f;
            ghost.transform.position = worldPos;

            bool canPlace = _gridSystem.CanBuildAt(finalPos, finalSize);

            var visuals = ghost.GetComponent<BuildingVisuals>();
            if (visuals != null)
            {
                visuals.SetState(entry.isBlueprint ? VisualState.Blueprint : VisualState.Ghost, canPlace);
            }

            if (!canPlace) _canPlaceGroup = false;
        }

        HideUnusedGhosts();
    }

    public void RotateGroupPreview()
    {
        _currentGroupRotation = (_currentGroupRotation + 90f) % 360f;
    }

    public void ExecutePlaceGroupCopy()
    {
        if (!_canPlaceGroup)
        {
            _notificationManager?.ShowNotification("Место занято!");
            return; // "Выходим", "не" "строя" "частично"
        }
        Vector2Int anchorPos = GridSystem.MouseGridPosition;
        if (anchorPos.x == -1) return;

        int success = 0;
        bool blueprintMode = BlueprintManager.IsActive;

        foreach (var entry in _currentGroupOffsets)
        {
            Vector2Int rotatedOffset = RotateVector(entry.offset, _currentGroupRotation);
            Vector2Int finalPos = anchorPos + rotatedOffset;
            float finalRot = (_currentGroupRotation + entry.yRotationDelta) % 360f;

            Vector2Int finalSize = GetRotatedSize(entry.data.size, finalRot);
            if (!_gridSystem.CanBuildAt(finalPos, finalSize))
                continue;

            if (_buildingManager.PlaceBuildingFromOrder(entry.data, finalPos, finalRot, blueprintMode))
                success++;
            else
                break; // закончились ресурсы
        }

        if (success > 0)
            _notificationManager?.ShowNotification($"Скопировано {success} зданий.");

    }

    // НОВЫЙ "ТИХИЙ" МЕТОД ОЧИСТКИ (не меняет режим)
    private void QuietCancel()
    {
        if (_liftedBuildings.Count > 0)
        {
            // Отмена перемещения живых зданий
            for (int i = 0; i < _liftedBuildings.Count; i++)
            {
                GameObject go = _liftedBuildings[i];
                if (go == null) continue; // Пропускаем, если объект null
                
                var id = go.GetComponent<BuildingIdentity>();
                if (id == null) continue; // Пропускаем, если нет Identity

                Vector2Int origPos = _originalPositions[i];
                float origRot = _originalRotations[i];
                Vector2Int origSize = GetRotatedSize(id.buildingData.size, origRot);

                id.rootGridPosition = origPos;
                id.yRotation = origRot;

                float cellSize = _gridSystem.GetCellSize();
                Vector3 worldPos = _gridSystem.GetWorldPosition(origPos.x, origPos.y);
                worldPos.x += (origSize.x * cellSize) / 2f;
                worldPos.z += (origSize.y * cellSize) / 2f;
                go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0, origRot, 0));

                _gridSystem.OccupyCells(id, origSize);

                _buildingManager.SetBuildingVisuals(go, id.isBlueprint ? VisualState.Blueprint : VisualState.Real, true);
                if (!id.isBlueprint)
                {
                    BuildOrchestrator.Instance?.PauseProduction(go, false);
                }
            }
        }
        else
        {
            // Отмена призраков (копирование)
            _ghostPoolIndex = 0;
            HideUnusedGhosts();
        }

        ClearAllLists();
    }

    /// <summary>
    /// СТАРЫЙ МЕТОД, ВЫЗЫВАЕМЫЙ ИЗ "ОРКЕСТРАТОРА".
    /// Теперь он "тихий" и только чистит, не меняя режим.
    /// </summary>
    public void CancelGroupOperation()
    {
        QuietCancel();
    }

    /// <summary>
    /// НОВЫЙ МЕТОД, ВЫЗЫВАЕМЫЙ ИЗ InputStates (State_GroupMoving/Copying).
    /// Этот метод чистит И выходит в Mode.None.
    /// </summary>
    public void CancelAndExitMode()
    {
        QuietCancel();
        _inputController.SetMode(InputMode.None);
    }

    // --------- Массовое перемещение (C/D) ---------

    public void StartMassMove(HashSet<BuildingIdentity> selection)
    {
        ClearAllLists();

        // Якорь
        BuildingIdentity anchorId = null;
        int minPosSum = int.MaxValue;
        foreach (var id in selection)
        {
            int sum = id.rootGridPosition.x + id.rootGridPosition.y;
            if (sum < minPosSum) { minPosSum = sum; anchorId = id; }
        }
        if (anchorId == null) return;

        _anchorGridPos = anchorId.rootGridPosition;
        _anchorRotation = anchorId.yRotation;

        foreach (var id in selection)
        {
            // 1. Сначала ПЫТАЕМСЯ поднять здание
            GameObject lifted = _gridSystem.PickUpBuilding(id.rootGridPosition.x, id.rootGridPosition.y);

            // 2. Если GridSystem вернул НЕ null (т.е. это здание можно перемещать)
            if (lifted != null)
            {
                // 3. Только ТЕПЕРЬ мы добавляем его во ВСЕ списки,
                // чтобы сохранить синхронизацию индексов.
                _currentGroupOffsets.Add(new GroupOffset
                {
                    data = id.buildingData,
                    offset = id.rootGridPosition - _anchorGridPos,
                    yRotationDelta = id.yRotation - _anchorRotation,
                    isBlueprint = id.isBlueprint
                });
                _originalPositions.Add(id.rootGridPosition);
                _originalRotations.Add(id.yRotation);
                _liftedBuildings.Add(lifted);

                // 4. И настраиваем визуал/паузу
                _buildingManager.SetBuildingVisuals(lifted, VisualState.Ghost, true);
                BuildOrchestrator.Instance?.PauseProduction(lifted, true);
            }
            // (Если lifted == null, мы просто игнорируем это здание (напр. Ферму),
            // не добавляя его ни в один из списков).
        }
        _inputController.SetMode(InputMode.GroupMoving);
    }

    public void UpdateGroupMovePreview(Vector2Int mouseGridPos)
    {
        _canPlaceGroup = true;

        float cellSize = _gridSystem.GetCellSize();

        for (int i = 0; i < _liftedBuildings.Count; i++)
        {
            GameObject go = _liftedBuildings[i];
            GroupOffset entry = _currentGroupOffsets[i];

            Vector2Int rotatedOffset = RotateVector(entry.offset, _currentGroupRotation);
            Vector2Int finalPos = mouseGridPos + rotatedOffset;
            float finalRot = (_currentGroupRotation + entry.yRotationDelta) % 360f;
            Vector2Int finalSize = GetRotatedSize(entry.data.size, finalRot);

            go.transform.rotation = Quaternion.Euler(0, finalRot, 0);

            Vector3 worldPos = _gridSystem.GetWorldPosition(finalPos.x, finalPos.y);
            worldPos.x += (finalSize.x * cellSize) / 2f;
            worldPos.z += (finalSize.y * cellSize) / 2f;
            go.transform.position = worldPos;

            bool canPlace = _gridSystem.CanBuildAt(finalPos, finalSize);
            _buildingManager.CheckPlacementValidity(go, entry.data, finalPos);
            if (!canPlace) _canPlaceGroup = false;
        }
    }

    public void PlaceGroupMove()
    {
        if (!_canPlaceGroup)
        {
            _notificationManager?.ShowNotification("Место занято!");
            return;
        }

        Vector2Int anchorPos = GridSystem.MouseGridPosition;
        if (anchorPos.x == -1) return;

        for (int i = 0; i < _liftedBuildings.Count; i++)
        {
            GameObject go = _liftedBuildings[i];
            GroupOffset entry = _currentGroupOffsets[i];
            var id = go.GetComponent<BuildingIdentity>();
            bool wasBlueprint = id.isBlueprint;

            Vector2Int rotatedOffset = RotateVector(entry.offset, _currentGroupRotation);
            Vector2Int finalPos = anchorPos + rotatedOffset;
            float finalRot = (_currentGroupRotation + entry.yRotationDelta) % 360f;
            Vector2Int finalSize = GetRotatedSize(entry.data.size, finalRot);

            id.rootGridPosition = finalPos;
            id.yRotation = finalRot;

            _gridSystem.OccupyCells(id, finalSize);

            _buildingManager.SetBuildingVisuals(go, wasBlueprint ? VisualState.Blueprint : VisualState.Real, true);
            if (!wasBlueprint)
            {
                BuildOrchestrator.Instance?.PauseProduction(go, false);
            }
        }

        _notificationManager?.ShowNotification("Группа перемещена.");

        ClearAllLists();
        _inputController.SetMode(InputMode.None);
    }

    // --------- Пул и очистка ---------

    private GameObject GetGhostFromPool(BuildingData data)
    {
        GameObject ghost;
        if (_ghostPoolIndex < _ghostPool.Count)
        {
            ghost = _ghostPool[_ghostPoolIndex];
        }
        else
        {
            // --- НАЧАЛО ФИКСА #7 (Это и есть решение бага #2) ---
            ghost = Instantiate(data.buildingPrefab, transform);
            
            // 1. Выключаем "мозги"
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
            _ghostPool[i].SetActive(false);
    }

    private void ClearAllLists()
    {
        _ghostPoolIndex = 0;
        _currentGroupOffsets.Clear();
        _liftedBuildings.Clear();
        _originalPositions.Clear();
        _originalRotations.Clear();
        _currentGroupRotation = 0f;
    }
}
