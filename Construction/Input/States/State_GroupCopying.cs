using UnityEngine;

/// <summary>
/// Режим "Массовое Копирование" (Держим группу зданий)
/// </summary>
public class State_GroupCopying : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly GroupOperationHandler _groupOperationHandler;

    // (Память не нужна, 'GroupOperationHandler' сам всё помнит)
    
    public State_GroupCopying(PlayerInputController controller, NotificationManager notificationManager, GroupOperationHandler groupOperationHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _groupOperationHandler = groupOperationHandler;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Массовое Копирование (Группа)");
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleGroupCopyingMode ---

        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        
        // "Сказать" "Обработчику Групп" "двигать" "призраков"
        _groupOperationHandler.UpdateGroupPreview(gridPos);

        // "Слушать" Поворот 'R'
        if (Input.GetKeyDown(KeyCode.R))
        {
            _groupOperationHandler.RotateGroupPreview();
        }

        // "Слушать" "Вставку" (ЛКМ) + "Shift-to-Stamp"
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            // 1. "Приказ" "Выполнить" "вставку"
            _groupOperationHandler.ExecutePlaceGroupCopy();

            // 2. "Проверяем", "зажат" "ли" Shift
            bool isStamping = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 3. "Если" "НЕ" "зажат" - "выходим" "из" "режима"
            if (!isStamping)
            {
                _groupOperationHandler.CancelAndExitMode();
                // (Handler сам вызовет _controller.SetMode(InputMode.None))
            }
            // (Если "зажат" - "просто" "остаемся" "в" "режиме")
        }

        // "Слушать" "Отмену" (ПКМ)
        if (Input.GetMouseButtonDown(1))
        {
            _groupOperationHandler.CancelAndExitMode();
            // (Handler сам вызовет _controller.SetMode(InputMode.None))
        }
    }

    public void OnExit()
    {
        // 'GroupOperationHandler.CancelGroupOperation()' - это и есть
        // "очистка" для этого режима. Он вызывается из 'OnUpdate',
        // поэтому здесь ничего делать не нужно, иначе будет 
        // двойной вызов или бесконечный цикл.
    }
}