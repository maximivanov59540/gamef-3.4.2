using UnityEngine;

/// <summary>
/// Режим "Выделение Рамкой" (Переходное состояние)
/// </summary>
public class State_Selecting : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly SelectionManager _selectionManager;
    
    // (Память не нужна, 'SelectionManager' сам всё помнит)

    public State_Selecting(PlayerInputController controller, NotificationManager notificationManager, 
                           SelectionManager selectionManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _selectionManager = selectionManager;
    }

    public void OnEnter()
    {
        // (Не спамим уведомлениями, это "мгновенный" режим)
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleSelectingMode ---

        Vector3 worldPos = _controller.GetMouseWorldPosition();
        
        // "Кнопка зажата"
        if (Input.GetMouseButton(0))
        {
            // "Скажи" SelectionManager'у "тянуть" рамку
            _selectionManager.UpdateSelection(worldPos);
        }

        // "Кнопку отпустили"
        if (Input.GetMouseButtonUp(0))
        {
            // "Скажи" SelectionManager'у "закончить" 
            _selectionManager.FinishSelectionAndSelect(worldPos);

            // "Вернись" в "свободный" режим
            _controller.SetMode(InputMode.None);
        }
        
        // (Этот режим нельзя отменить по ПКМ, он сам отменится)
    }

    public void OnExit()
    {
        // (SelectionManager сам "прячет" рамку, когда 'FinishSelection'
        //  вызывается, поэтому здесь очищать нечего)
    }
}