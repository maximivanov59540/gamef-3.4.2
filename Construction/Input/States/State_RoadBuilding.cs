using UnityEngine;

/// Режим "Строительство дорог: два клика"
/// 1) Первый ЛКМ — запоминаем точку A и включаем превью (тень).
/// 2) Движем мышь — превью обновляется A→курсор.
/// 3) Второй ЛКМ — строим A→B, затем A автоматически = B (можно сразу тянуть дальше).
/// ПКМ: если есть превью — отмена (сброс A); если уже пусто — выход из режима.
/// Esc: то же, что ПКМ (сначала отмена, повторно — выход).
public class State_RoadBuilding : IInputState
{
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly RoadBuildHandler _roadBuildHandler;

    private bool _hasStart = false;
    private Vector2Int _startCell = new Vector2Int(-1, -1);
    private Vector2Int _lastMouse = new Vector2Int(-1, -1);

    public State_RoadBuilding(PlayerInputController controller, NotificationManager notificationManager, RoadBuildHandler roadBuildHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _roadBuildHandler = roadBuildHandler;
    }

    public void OnEnter()
    {
        _hasStart = false;
        _startCell = new Vector2Int(-1, -1);
        _lastMouse = new Vector2Int(-1, -1);
        _roadBuildHandler.ClearRoadPreview();
        _notificationManager.ShowNotification("Режим: Дороги (2 клика)");
    }

    public void OnUpdate()
    {
        bool overUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;

        // ПКМ / Esc — «двухэтапный выход»: сначала отмена, потом выход
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (_hasStart || _roadBuildHandler.HasPreview())
            {
                // 1-е нажатие — просто очистить превью/точку A
                _hasStart = false;
                _startCell = new Vector2Int(-1, -1);
                _lastMouse = new Vector2Int(-1, -1);
                _roadBuildHandler.ClearRoadPreview();
                _notificationManager.ShowNotification("Отменено");
            }
            else
            {
                // 2-е нажатие — выйти из режима
                _controller.SetMode(InputMode.None);
            }
            return;
        }

        // Если мышь над UI или вне поля — только не рисуем превью,
        // но сохранённую A не сбрасываем: игрок может увезти мышь и вернуться.
        if (overUI || gridPos.x < 0)
            return;

        // Первый ЛКМ — ставим A и запускаем превью
        if (Input.GetMouseButtonDown(0) && !_hasStart)
        {
            _hasStart = true;
            _startCell = gridPos;
            _lastMouse = new Vector2Int(-1, -1); // чтобы принудительно обновить превью сразу
            _roadBuildHandler.StartRoadPreview(); // очищает и готовит пул призраков
            _roadBuildHandler.UpdateRoadPreview(_startCell, gridPos);
            return;
        }

        // Движение мыши при установленной A — обновляем превью
        if (_hasStart && gridPos != _lastMouse)
        {
            _roadBuildHandler.UpdateRoadPreview(_startCell, gridPos);
            _lastMouse = gridPos;
        }

        // Второй ЛКМ — строим A→B. После строительства A = B (не выходим из режима)
        if (Input.GetMouseButtonDown(0) && _hasStart)
        {
            // финальный апдейт и постройка
            _roadBuildHandler.UpdateRoadPreview(_startCell, gridPos);
            _roadBuildHandler.ExecuteRoadBuild();

            // продолжаем сеанс: новая A = текущая B
            _hasStart = true;
            _startCell = gridPos;
            _lastMouse = new Vector2Int(-1, -1);

            // готовим чистое превью для следующего сегмента (если игрок поведёт мышь)
            _roadBuildHandler.StartRoadPreview();
            return;
        }
    }

    public void OnExit()
    {
        _hasStart = false;
        _startCell = new Vector2Int(-1, -1);
        _lastMouse = new Vector2Int(-1, -1);
        _roadBuildHandler.ClearRoadPreview();
        _notificationManager.ShowNotification("Режим: Обычный");
    }
}
