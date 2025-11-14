using UnityEngine;
public class BlueprintManager : MonoBehaviour
{
    // --- 1. Синглтон (Singleton Pattern) ---
    public static BlueprintManager Instance { get; private set; }

    // --- 2. Состояние (State) ---
    public bool IsBlueprintModeActive { get; private set; } = false;
public static bool IsActive
    {
        get
        {
            if (Instance == null)
            {
                return false;
            }
            return Instance.IsBlueprintModeActive;
        }
    }
    private NotificationManager _notificationManager;

    
    // --- 3. Инициализация Синглтона ---

    private void Awake()
    {
        // Классическая проверка синглтона:
        // "Если 'Instance' (наш статический ящик) уже существует И это не я..."
        if (Instance != null && Instance != this)
        {
            // "...тогда я лишний. Уничтожить меня."
            Debug.LogWarning("Обнаружен дубликат BlueprintManager. Самоуничтожаюсь.");
            Destroy(gameObject);
        }
        else
        {
            // "...иначе, я - первый. Я и буду 'Instance'."
            Instance = this;

            _notificationManager = FindFirstObjectByType<NotificationManager>();
            // (Опционально: не уничтожать при смене сцены, если потребуется)
            // DontDestroyOnLoad(gameObject); 
        }
    }

    // --- 4. Публичный Метод (Behavior) ---
    public void ToggleBlueprintMode()
    {
        IsBlueprintModeActive = !IsBlueprintModeActive;

        // Лог для отладки, чтобы мы видели в консоли, что происходит
        Debug.Log($"[BlueprintManager] Режим 'Чертежей' теперь: {IsBlueprintModeActive}");

        if (IsBlueprintModeActive)
        {
            _notificationManager?.ShowNotification("Режим: Проектирование");
        }
        else
        {
            _notificationManager?.ShowNotification("Режим: Проектирование выключено");
        }
    }
}