using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(BuildingIdentity))]
public class ZonedArea : MonoBehaviour
{
    [Tooltip("Список всех слотов для строительства внутри этой зоны")]
    public List<BuildSlot> buildSlots = new List<BuildSlot>();

    [Header("Визуализация Слотов")]
    [Tooltip("Префаб 'PF_SlotVisualizer' для подсветки слотов")]
    [SerializeField]
    private GameObject slotVisualPrefab;

    // "Хранилище" для "созданных" "подсветок"
    private readonly List<GameObject> _slotVisualizers = new List<GameObject>();

    private BuildingIdentity _identity;
    public enum ZonePivot { Center, BottomLeft }

    [SerializeField] private ZonePivot pivotOrigin = ZonePivot.Center;

    // Кэш сетки, чтобы знать реальный размер клетки (а не 1.0f по умолчанию)
    private GridSystem _grid;

    // Ленивая проверка и пересоздание визуализаторов (если список слотов поменяли в Инспекторе)
    private void EnsureVisuals()
    {
        if (_slotVisualizers.Count != buildSlots.Count)
        {
            foreach (var go in _slotVisualizers)
                if (go) Destroy(go);
            _slotVisualizers.Clear();

            if (isActiveAndEnabled)
                InitializeSlotVisuals();
        }
    }
    void Awake()
    {
        // В Awake() "получаем" "только" "внутренние" "ссылки"
        _identity = GetComponent<BuildingIdentity>();
        _grid = FindFirstObjectByType<GridSystem>();
        if (_identity == null)
        {
            Debug.LogError($"ZonedArea ({gameObject.name}) не может найти свой BuildingIdentity!", this);
        }

        // НЕ вызываем InitializeSlotVisuals() здесь!
    }

    /// <summary>
    /// "Вызываем" "создание" "визуализаторов" "в" "Start()", 
    /// "чтобы" "гарантировать", "что" "_identity.buildingData" "уже" "заполнено"
    /// "другими" "скриптами" (BuildingManager).
    /// </summary>
    void Start()
    {
        InitializeSlotVisuals();
    }

    /// <summary>
    /// "Создает" "визуализаторы" "для" "каждого" "слота" "при" "старте"
    /// </summary>
    private void InitializeSlotVisuals()
    {
        if (_identity.buildingData == null)
        {
            Debug.LogError($"ZonedArea ({gameObject.name}): buildingData не назначен ПЕРЕД Start()! " +
                           "Визуализаторы не будут созданы.", this);
            return;
        }
        if (slotVisualPrefab == null)
        {
            Debug.LogWarning("ZonedArea: Не назначен 'slotVisualPrefab', подсветка слотов работать не будет.");
            return;
        }

        float cell = (_grid != null ? _grid.GetCellSize() : 1f);
        Vector2Int footprint = _identity.buildingData.size;

        for (int i = 0; i < buildSlots.Count; i++)
        {
            BuildSlot slot = buildSlots[i];

            GameObject visual = Instantiate(slotVisualPrefab, this.transform);

            // ВАЖНО: растягиваем по XZ, Y — это «толщина»
            visual.transform.localScale = new Vector3(slot.size.x * cell, 1f, slot.size.y * cell);

            // Центр слота в локальных координатах «от (0,0)»
            float localCenterX = (slot.localPosition.x + slot.size.x * 0.5f) * cell;
            float localCenterZ = (slot.localPosition.y + slot.size.y * 0.5f) * cell;

            // Смещение, если pivot монастыря — по центру
            float offX = 0f, offZ = 0f;
            if (pivotOrigin == ZonePivot.Center)
            {
                offX = -(footprint.x * 0.5f) * cell;
                offZ = -(footprint.y * 0.5f) * cell;
            }

            visual.transform.localPosition = new Vector3(localCenterX + offX, 0.05f, localCenterZ + offZ);

            visual.SetActive(false);
            _slotVisualizers.Add(visual);
        }
    }
    /// <summary>
    /// "Показывает" "подсветку" (вызывается из UIManager).
    /// </summary>
    public void ShowSlotHighlights()
    {
        EnsureVisuals();
        int count = Mathf.Min(buildSlots.Count, _slotVisualizers.Count);
        for (int i = 0; i < count; i++)
        {
            bool empty = buildSlots[i].occupiedBy == null;
            _slotVisualizers[i].SetActive(empty);
        }
    }
    public void ShowSlotHighlights(BuildingData candidate)
    {
        EnsureVisuals();
        if (candidate == null)
        {
            ShowSlotHighlights();
            return;
        }

        int count = Mathf.Min(buildSlots.Count, _slotVisualizers.Count);
        for (int i = 0; i < count; i++)
        {
            var slot = buildSlots[i];
            bool empty = slot.occupiedBy == null;
            if (!empty) { _slotVisualizers[i].SetActive(false); continue; }

            bool sizeFits = (slot.size == candidate.size);
            bool allowed = (slot.allowedBuildings == null || slot.allowedBuildings.Count == 0
                            || slot.allowedBuildings.Contains(candidate));

            _slotVisualizers[i].SetActive(sizeFits && allowed);
        }
    }

    /// <summary>
    /// "Прячет" "подсветку" (вызывается из UIManager).
    /// </summary>
    public void HideSlotHighlights()
    {
        foreach (GameObject visual in _slotVisualizers)
        {
            visual.SetActive(false);
        }
    }
    // --- "Старая" "логика" "остается" "ниже" ---

    public BuildSlot GetSlotAtWorld(Vector2Int worldGridPos)
    {
        if (_identity == null) return null;
        Vector2Int zoneRoot = _identity.rootGridPosition;
        Vector2Int localPos = worldGridPos - zoneRoot;
        return buildSlots.FirstOrDefault(slot => slot.localPosition == localPos);
    }
    public void OccupySlot(BuildSlot slot, BuildingIdentity building)
    {
        if (slot != null)
        {
            slot.occupiedBy = building;
        }
    }
    public void ClearSlot(BuildSlot slot)
    {
        if (slot != null)
        {
            slot.occupiedBy = null;
        }
    }
    public BuildSlot GetSlotOccupiedBy(BuildingIdentity building)
    {
        if (building == null) return null;
        return buildSlots.FirstOrDefault(slot => slot.occupiedBy == building);
    }
}