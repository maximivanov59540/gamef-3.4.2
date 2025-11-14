using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum InputMode
{
    None,
    Building,
    Moving,
    Deleting,
    Upgrading,
    Copying,
    Selecting,
    GroupCopying,
    GroupMoving,
    RoadBuilding,
    PlacingModule,
    RoadOperation
}

public class PlayerInputController : MonoBehaviour
{
    // ... (все твои [SerializeField] остаются без изменений) ...
    [Header("Ссылки на Менеджеры")]
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SelectionManager _selectionManager;
    [SerializeField] private MassBuildHandler _massBuildHandler;
    [SerializeField] private GroupOperationHandler _groupOperationHandler;
    [SerializeField] private RoadManager _roadManager;
    [SerializeField] private AuraManager _auraManager;
    [SerializeField] private RoadBuildHandler _roadBuildHandler;
    [SerializeField] private RoadOperationHandler _roadOperationHandler;
    
    private NotificationManager _notificationManager;

    private IInputState _currentState;
    private Dictionary<InputMode, IInputState> _states;

    public static InputMode CurrentInputMode { get; private set; } = InputMode.None;
    public static PlayerInputController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
        
        // ... (все твои FindFirstObjectByType... без изменений) ...
        if (buildingManager == null) buildingManager = FindFirstObjectByType<BuildingManager>();
        if (gridSystem == null) gridSystem = FindFirstObjectByType<GridSystem>();
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        if (_selectionManager == null) _selectionManager = FindFirstObjectByType<SelectionManager>();
        if (_massBuildHandler == null) _massBuildHandler = FindFirstObjectByType<MassBuildHandler>();
        if (_groupOperationHandler == null) _groupOperationHandler = FindFirstObjectByType<GroupOperationHandler>();
        if (_roadManager == null) _roadManager = RoadManager.Instance;
        if (_auraManager == null) _auraManager = AuraManager.Instance;
        if (_roadBuildHandler == null) _roadBuildHandler = FindFirstObjectByType<RoadBuildHandler>();
        _notificationManager = FindFirstObjectByType<NotificationManager>();
        if (_roadOperationHandler == null) _roadOperationHandler = FindFirstObjectByType<RoadOperationHandler>();


        // 2. ИНИЦИАЛИЗИРУЕМ "ФАБРИКУ СОСТОЯНИЙ"
        _states = new Dictionary<InputMode, IInputState>();
        _states[InputMode.None] = new State_None(this, _notificationManager, gridSystem, uiManager, _selectionManager, buildingManager);
        _states[InputMode.Building] = new State_Building(this, _notificationManager, buildingManager, _massBuildHandler, _auraManager);
        _states[InputMode.Moving] = new State_Moving(this, _notificationManager, buildingManager, _selectionManager, _groupOperationHandler, gridSystem);
        _states[InputMode.Deleting] = new State_Deleting(this, _notificationManager, buildingManager, gridSystem, uiManager, _selectionManager, _roadManager);
        _states[InputMode.Upgrading] = new State_Upgrading(this, _notificationManager, buildingManager, gridSystem, _selectionManager);
        _states[InputMode.Copying] = new State_Copying(this, _notificationManager, buildingManager, gridSystem, _selectionManager, _groupOperationHandler);
        _states[InputMode.Selecting] = new State_Selecting(this, _notificationManager, _selectionManager);
        _states[InputMode.GroupCopying] = new State_GroupCopying(this, _notificationManager, _groupOperationHandler);
        _states[InputMode.GroupMoving] = new State_GroupMoving(this, _notificationManager, _groupOperationHandler);
        _states[InputMode.RoadBuilding] = new State_RoadBuilding(this, _notificationManager, _roadBuildHandler);
        _states[InputMode.PlacingModule] = new State_PlacingModule(this, gridSystem, buildingManager, _notificationManager);
        _states[InputMode.RoadOperation] = new State_RoadOperation(this, _notificationManager, _roadOperationHandler);
        
        _currentState = _states[InputMode.None];
        _currentState.OnEnter();
    }

    void Update()
    {
        _currentState?.OnUpdate();
    }

    public void SetMode(InputMode newMode)
    {
        // (Метод SetMode остается без изменений)
        _currentState?.OnExit();

        if (_states.ContainsKey(newMode))
        {
            _currentState = _states[newMode];
            CurrentInputMode = newMode;
        }
        else
        {
            Debug.LogError($"!!! ОШИБКА: Попытка переключиться в режим {newMode}!");
            _currentState = _states[InputMode.None];
            CurrentInputMode = InputMode.None;
        }
        
        if (CurrentInputMode != InputMode.Building && CurrentInputMode != InputMode.PlacingModule)
        {
            // (Логика скрытия 'ZonedArea' highlights)
            #if UNITY_2022_2_OR_NEWER
            foreach (var zone in FindObjectsByType<ZonedArea>(FindObjectsSortMode.None))
                zone.HideSlotHighlights();
            #else
            foreach (var zone in FindObjectsOfType<ZonedArea>())
                zone.HideSlotHighlights();
            #endif
        }

        _currentState.OnEnter(); // <-- ВАЖНО: Вызываем OnEnter() *БЕЗ* параметров
        BuildOrchestrator.Instance?.OnModeChanged(CurrentInputMode);
    }
    
    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД (Шаг 2.0) ⬇️ ---
    /// <summary>
    /// "Публичная" "точка" "входа" "для" "UI", "чтобы" "активировать" "режим" "постройки" "модулей".
    /// </summary>
    /// <param name="targetFarm">"Ферма", "к" "которой" "будем" "строить"</param>
    /// <param name="moduleToBuild">"Чертеж" (BuildingData) "модуля", "который" "строим"</param>
    public void EnterPlacingModuleMode(ModularBuilding targetFarm, BuildingData moduleToBuild)
    {
        if (targetFarm == null || moduleToBuild == null)
        {
            Debug.LogError("EnterPlacingModuleMode: 'targetFarm' или 'moduleToBuild' не заданы!");
            return;
        }
        
        if (!moduleToBuild.isModule)
        {
             Debug.LogError($"EnterPlacingModuleMode: {moduleToBuild.name} не помечен как 'isModule = true'!");
            return;
        }

        // 1. "Переключаем" "состояние"
        SetMode(InputMode.PlacingModule);

        // 2. "Получаем" "инстанс" "этого" "состояния" "из" "словаря"
        State_PlacingModule moduleState = _states[InputMode.PlacingModule] as State_PlacingModule;
        
        if (moduleState != null)
        {
            // 3. "Вызываем" "его" "OnEnter" "С" "ПАРАМЕТРАМИ"
            // (SetMode() уже вызвал OnEnter() БЕЗ параметров, 
            // поэтому наш OnEnter(params) должен быть "умным")
            moduleState.OnEnter(targetFarm, moduleToBuild);
        }
    }
    // --- ⬆️ КОНЕЦ ИЗМЕНЕНИЙ ⬆️ ---

    // --- ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ (Без изменений) ---
    public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();
    public Vector3 GetMouseWorldPosition() => GridSystem.MouseWorldPosition;
    public void EnterDeletingMode() => SetMode(InputMode.Deleting);
}