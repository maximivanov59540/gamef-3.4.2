using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [SerializeField] private GridSystem _gridSystem;
    [SerializeField] private PlayerInputController _playerInputController;

    private HashSet<BuildingIdentity> _selectedBuildings = new HashSet<BuildingIdentity>();

    // --- НАЧАЛО: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---

    [SerializeField] private LineRenderer _selectionBoxRenderer; // Сюда мы "перетащим" префаб
    private Vector3 _startWorldPos; // Точка, где мы "кликнули"
    [SerializeField] private AuraManager _auraManager;
    public event System.Action<IReadOnlyCollection<BuildingIdentity>> SelectionChanged;
    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(_selectedBuildings);


    // --- КОНЕЦ: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---
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

        // --- НАЧАЛО: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---

        // Важно: выключаем рамку при старте игры, чтобы ее не было видно
        if (_selectionBoxRenderer != null)
        {
            _selectionBoxRenderer.gameObject.SetActive(false);
        }
        else
        {
            // "Подсказка" "для" "разработчика" "в" "консоли"
            Debug.LogWarning("SelectionManager: Не назначен '_selectionBoxRenderer' (рамка выделения). " +
                             "Выделение будет работать, но не будет видно.");

            // --- КОНЕЦ: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---
        }
        if (_auraManager == null) _auraManager = FindFirstObjectByType<AuraManager>();
    }
    public void ClearSelection()
    {
        _selectedBuildings.Clear();

        // На всякий случай спрячем дорожно-аурный оверлей,
        // если он вдруг остался висеть (например, при очистке извне)
        if (AuraManager.Instance != null)
            AuraManager.Instance.HideRoadAuraOverlay();
            RaiseSelectionChanged();

    }
    // --- НАЧАЛО: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---

    // Вызывается из PlayerInputController, когда мы "нажимаем" мышь
    public void StartSelection(Vector3 worldPos)
    {
        // Debug.Log("StartSelection"); // Для отладки

        // 1. Очищаем любое "старое" выделение
        ClearSelection();

        // 2. "Запоминаем" начальную точку
        _startWorldPos = worldPos;

        // 3. "Включаем" рамку
        if (_selectionBoxRenderer != null)
        {
            _selectionBoxRenderer.gameObject.SetActive(true);
        }
    }

    // Вызывается из PlayerInputController, когда мы "тянем" мышь
    public void UpdateSelection(Vector3 currentWorldPos)
    {

        if (_selectionBoxRenderer == null) return;

        // Debug.Log("UpdateSelection"); // Для отладки

        // Это "мозг" рисования рамки (Вариант 1Б)
        // Мы берем "начальную" и "текущую" точки
        // и рассчитываем 4 угла прямоугольника на "земле" (Y = 0)

        // Ставим Y в 0 (или чуть выше), чтобы рамка была на земле
        _startWorldPos.y = 0.1f;
        currentWorldPos.y = 0.1f;

        // Угол 0 (начало)
        _selectionBoxRenderer.SetPosition(0, _startWorldPos);

        // Угол 1
        Vector3 pos1 = new Vector3(currentWorldPos.x, 0.1f, _startWorldPos.z);
        _selectionBoxRenderer.SetPosition(1, pos1);

        // Угол 2 (текущий)
        _selectionBoxRenderer.SetPosition(2, currentWorldPos);

        // Угол 3
        Vector3 pos3 = new Vector3(_startWorldPos.x, 0.1f, currentWorldPos.z);
        _selectionBoxRenderer.SetPosition(3, pos3);

        // Угол 4 (возврат в начало, т.к. Loop = true)
        _selectionBoxRenderer.SetPosition(4, _startWorldPos);
    }

    // Вызывается из PlayerInputController, когда мы "отпускаем" мышь
    // (пока просто прячем рамку, логика будет в Задаче E)
    public void HideSelectionVisuals()
    {
        // Debug.Log("HideSelectionVisuals"); // Для отладки
        if (_selectionBoxRenderer != null)
        {
            _selectionBoxRenderer.gameObject.SetActive(false);
        }
    }

    // --- КОНЕЦ: НОВЫЙ КОД ДЛЯ ЗАДАЧИ B ---
    // --- НАЧАЛО: НОВЫЙ КОД (ЗАГЛУШКА ДЛЯ ЗАДАЧИ E) ---

    public HashSet<BuildingIdentity> FinishSelectionAndSelect(Vector3 endWorldPos)
    {
        // 1. Спрячь рамку
        HideSelectionVisuals();

        // 2. Переводим в координаты сетки
        _gridSystem.GetXZ(_startWorldPos, out int startX, out int startZ);
        _gridSystem.GetXZ(endWorldPos, out int endX, out int endZ);

        Vector2Int startGridPos = new Vector2Int(startX, startZ);
        Vector2Int endGridPos = new Vector2Int(endX, endZ);

        // 3. Собираем здания в области
        HashSet<BuildingIdentity> found = _gridSystem.GetBuildingsInRect(startGridPos, endGridPos);

        // 4. Запоминаем выделение
        _selectedBuildings = found;
        RaiseSelectionChanged();

        // 5. НОВОЕ: показываем дорожный оверлей для всех RoadBased-источников из выделения
        ShowRoadAurasForSelection();

        return _selectedBuildings;
    }
    /// Показывает дорожный оверлей для текущего выделения:
    /// берём максимум покрытия, если выбрано несколько рынков/служб.
    private void ShowRoadAurasForSelection()
    {
        var aura = AuraManager.Instance;
        if (aura == null)
            return;

        // Сначала очистим старый оверлей
        aura.HideRoadAuraOverlay();

        if (_selectedBuildings == null || _selectedBuildings.Count == 0)
            return;

        // Включаем источники для всех подходящих зданий
        foreach (var b in _selectedBuildings)
        {
            if (b == null) continue;
            var emitter = b.GetComponent<AuraEmitter>();
            if (emitter != null && emitter.distributionType == AuraDistributionType.RoadBased)
            {
                aura.ShowRoadAura(emitter);
            }
        }
    }

    /// Удобный метод для одиночного клика (если где-то выбираешь здание по одному):
    /// очистит прошлое, добавит текущее и отрисует оверлей.
    public void SelectSingle(BuildingIdentity building)
    {
        ClearSelection();
        if (building != null)
            _selectedBuildings.Add(building);
        ShowRoadAurasForSelection();
        RaiseSelectionChanged();

    }

    public void ShowRadius(BuildingIdentity building)
    {
        if (building == null) return;

        // 1) Старый круглый радиус (если есть визуализатор)
        RadiusVisualizer visualizer = building.GetComponentInChildren<RadiusVisualizer>();
        if (visualizer != null)
        {
            visualizer.Show();
        }

        // 2) НОВОЕ: если на здании есть AuraEmitter c типом распространения "RoadBased",
        // то просим AuraManager показать подсветку дорог
        var emitter = building.GetComponent<AuraEmitter>();
        if (emitter != null && emitter.distributionType == AuraDistributionType.RoadBased)
        {
            if (AuraManager.Instance != null)
            {
                AuraManager.Instance.ShowRoadAura(emitter);
            }
            else
            {
                Debug.LogWarning("[SelectionManager] AuraManager.Instance == null, не могу показать дорожно-аурный оверлей");
            }
        }
    }

    /// <summary>
    /// "Тупая" команда: "Найди 'художника' на этом здании и скажи ему 'Прячь!'"
    /// Вызывается из State_None.
    /// </summary>
    public void HideRadius(BuildingIdentity building)
    {
        if (building == null) return;

        // 1) Прячем круглый радиус (если был)
        RadiusVisualizer visualizer = building.GetComponentInChildren<RadiusVisualizer>();
        if (visualizer != null)
        {
            visualizer.Hide();
        }

        // 2) НОВОЕ: если здание имело дорожную ауру — просим AuraManager спрятать оверлей
        var emitter = building.GetComponent<AuraEmitter>();
        if (emitter != null && emitter.distributionType == AuraDistributionType.RoadBased)
        {
            if (AuraManager.Instance != null)
                AuraManager.Instance.HideRoadAuraOverlay();
        }
    }
    private void UpdateAuraForCurrentSelection()
    {
        if (_auraManager == null) return;

        // Если выбран ровно один объект — пробуем показать его ауру
        if (_selectedBuildings != null && _selectedBuildings.Count == 1)
        {
            var e = default(AuraEmitter);
            foreach (var b in _selectedBuildings) // возьмём единственный
            {
                if (b == null) break;
                e = b.GetComponent<AuraEmitter>();
                break;
            }

            if (e != null && e.distributionType == AuraDistributionType.RoadBased)
            {
                // На всякий случай — актуализируем корневую клетку (если есть метод)
                // e.RefreshRootCell(); // если мы уже добавляли его раньше
                _auraManager.ShowRoadAura(e);
                return;
            }
        }

        // Иначе — скрываем оверлей дорог
        _auraManager.HideRoadAuraOverlay();
    }
}