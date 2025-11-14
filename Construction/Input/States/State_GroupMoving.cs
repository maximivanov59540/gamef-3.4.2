using UnityEngine;

/// <summary>
/// Режим "Массовое Перемещение" (Держим группу зданий)
/// </summary>
public class State_GroupMoving : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly GroupOperationHandler _groupOperationHandler;
    
    // (Память не нужна, 'GroupOperationHandler' сам всё помнит)

    public State_GroupMoving(PlayerInputController controller, NotificationManager notificationManager, 
                             GroupOperationHandler groupOperationHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _groupOperationHandler = groupOperationHandler;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Массовое Перемещение (Группа)");
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleGroupMovingMode ---

        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        
        // "Сказать" "Обработчику Групп" "двигать" "живые" здания
        _groupOperationHandler.UpdateGroupMovePreview(gridPos);

        // "Слушать" Поворот 'R'
        if (Input.GetKeyDown(KeyCode.R))
        {
            _groupOperationHandler.RotateGroupPreview();
        }

        // "Слушать" "Вставку" (ЛКМ)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            _groupOperationHandler.PlaceGroupMove();
            // (Handler сам очистит и вызовет _controller.SetMode(InputMode.None))
        }

        // "Слушать" "Отмену" (ПКМ)
        if (Input.GetMouseButtonDown(1))
        {
            _groupOperationHandler.CancelAndExitMode();
            // (Handler сам очистит и вызовет _controller.SetMode(InputMode.None))
        }
    }

    public void OnExit()
    {
        // Как и в 'GroupCopying', 'GroupOperationHandler' 
        // сам управляет своей очисткой и выходом из режима.
        // 'OnExit' остается пустым.
    }
}