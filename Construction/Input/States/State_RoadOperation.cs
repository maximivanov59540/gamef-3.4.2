// --- State_RoadOperation.cs ---
using UnityEngine;

public class State_RoadOperation : IInputState
{
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly RoadOperationHandler _roadOpHandler;

    public State_RoadOperation(PlayerInputController controller, NotificationManager notificationManager, RoadOperationHandler roadOperationHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _roadOpHandler = roadOperationHandler;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Массовые операции с дорогами");
    }

    public void OnUpdate()
    {
        if (_controller.IsPointerOverUI())
        {
            _roadOpHandler.HideGhosts();
            return;
        }

        // 1. Двигать призраки
        _roadOpHandler.UpdatePreview(GridSystem.MouseGridPosition);

        // 2. Построить (ЛКМ)
        if (Input.GetMouseButtonDown(0))
        {
            _roadOpHandler.ExecutePlace();
        }

        // 3. Отмена (ПКМ)
        if (Input.GetMouseButtonDown(1))
        {
            _roadOpHandler.CancelAndExitMode();
        }
    }

    public void OnExit()
    {
        // "Тихо" чистим призраки, не меняя состояние
        _roadOpHandler.QuietCancel(); 
    }
}