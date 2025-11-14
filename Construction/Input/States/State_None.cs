using UnityEngine;

/// <summary>
/// Режим "Ничего не делаем" (Осмотр, Выделение)
/// </summary>
public class State_None : IInputState
{
    // --- Ссылки на "Инструменты" ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly GridSystem _gridSystem;
    private readonly UIManager _uiManager;
    private readonly SelectionManager _selectionManager;
    private readonly BuildingManager _buildingManager;
    private BuildingIdentity _currentlySelectedBuilding = null;

    // --- "Память" этого режима ---
    // (Здесь нет)

    // --- Конструктор (получаем "инструменты" от "Мозга") ---
    public State_None(PlayerInputController controller, NotificationManager notificationManager, 
                      GridSystem gridSystem, UIManager uiManager, SelectionManager selectionManager,
                      BuildingManager buildingManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _gridSystem = gridSystem;
        _uiManager = uiManager;
        _selectionManager = selectionManager;
        _buildingManager = buildingManager;
    }

    public void OnEnter()
    {
        // ЭТО И ЕСТЬ КЛЮЧЕВОЙ ФИКС БАГА #1
        // Теперь, при входе в режим "Ничего", мы принудительно
        // "убираем" все "призраки" и "сбрасываем" BuildingManager.
        if (_buildingManager != null)
        {
            _buildingManager.CancelAllModes();

            // Также можно было бы прятать сетку здесь,
            // но BuildUIManager уже делает это при закрытии панели,
            // а при выборе другого режима (Move, Build) сетка
            // наоборот должна остаться. Так что эту строку оставляем
            // закомментированной или удаляем.
            // _buildingManager.ShowGrid(false); 
        }
        else
        {
            Debug.LogError("State_None: BuildingManager не был передан в конструкторе!");
        }
    }
    public void OnUpdate()
    {
        
        bool isOverUI = _controller.IsPointerOverUI();

        // ЛКМ и не над UI
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            Vector2Int gridPos = GridSystem.MouseGridPosition;

            // Промах по сетке / вне поля
            if (gridPos.x == -1)
            {
                // Спрятать панель и радиус, очистить выделение (уберёт и дорожный оверлей)
                if (_currentlySelectedBuilding != null)
                {
                    _uiManager.HideInfo();
                    _selectionManager.HideRadius(_currentlySelectedBuilding);
                    _currentlySelectedBuilding = null;
                }

                _selectionManager.ClearSelection(); // важно: прячет дорожный оверлей
                return;
            }

            // Попали в сетку: проверяем здание через GridSystem
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

            if (id != null)
            {
                // Тоггл: повторный клик по тому же — снять выделение
                if (_currentlySelectedBuilding == id)
                {
                    _uiManager.HideInfo();
                    _selectionManager.HideRadius(id);
                    _selectionManager.ClearSelection(); // убирает и дорожный оверлей
                    _currentlySelectedBuilding = null;
                }
                else
                {
                    // Показать инфо (как раньше)
                    _uiManager.ShowInfo(id.buildingData);

                    // Делегируем выделение в SelectionManager:
                    // 1) очистит прошлое, 2) выделит текущее, 3) покажет дорожный оверлей
                    _selectionManager.SelectSingle(id);
                    var emitter = id.GetComponent<AuraEmitter>();
                    if (emitter != null && emitter.distributionType == AuraDistributionType.RoadBased)
                    {
                        Debug.Log($"[State_None] Clicked building with RoadBased Aura at {emitter.GetRootPosition()}");
                        AuraManager.Instance?.ShowRoadAura(emitter);
                    }
                    // Сохраняем старое кольцо-радиус (если оно используется)
                    _selectionManager.ShowRadius(id);

                    _currentlySelectedBuilding = id;
                }
            }
            else
            {
                // Клик по пустой земле: закрыть инфо/радиус, очистить выделение, перейти к рамке
                if (_currentlySelectedBuilding != null)
                {
                    _uiManager.HideInfo();
                    _selectionManager.HideRadius(_currentlySelectedBuilding);
                    _currentlySelectedBuilding = null;
                }

                _selectionManager.ClearSelection(); // прячет дорожный оверлей
                _selectionManager.StartSelection(GridSystem.MouseWorldPosition);
                _controller.SetMode(InputMode.Selecting);
            }
        }
    }
    
    public void OnExit()
    {
        // (Если бы мы показывали инфо, здесь бы мы его прятали)
        // _uiManager.HideInfo(); // (Уже делается в OnUpdate)
    }
}