/// <summary>
/// Интерфейс ("Контракт") для всех состояний ввода.
/// Каждый режим (None, Building, Deleting) будет реализовывать это.
/// </summary>
public interface IInputState
{
    /// <summary>
    /// Вызывается один раз, когда мы ВХОДИМ в это состояние.
    /// (Идеально для показа уведомлений, сброса флагов)
    /// </summary>
    void OnEnter();

    /// <summary>
    /// Вызывается каждый кадр (замена Update() в PlayerInputController).
    /// Вся логика режима (проверка кликов, драг) будет здесь.
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// Вызывается один раз, когда мы ВЫХОДИМ из этого состояния.
    /// (Идеально для очистки: CancelAllModes, HideSelectionVisuals)
    /// </summary>
    void OnExit();
}