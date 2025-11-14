using UnityEngine;

/// <summary>
/// "Казна". Отдельная система для хранения и управления деньгами (Золотом).
/// </summary>
public class MoneyManager : MonoBehaviour
{
    // Синглтон для легкого доступа
    public static MoneyManager Instance { get; private set; }

    [Header("Казна")]
    [Tooltip("Текущее количество денег. Можно смотреть в инспекторе для отладки.")]
    [SerializeField] private float _currentMoney = 100f; // Дадим немного стартовых денег

    private void Awake()
    {
        // Настройка синглтона
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Возвращает текущий баланс. (Для UI)
    /// </summary>
    public float GetCurrentMoney()
    {
        return _currentMoney;
    }

    /// <summary>
    /// Добавляет деньги в казну (например, налоги).
    /// </summary>
    public void AddMoney(float amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Попытка добавить отрицательное кол-во денег. Используйте SpendMoney().");
            return;
        }
        _currentMoney += amount;
    }

    /// <summary>
    /// Проверяет, достаточно ли денег в казне.
    /// </summary>
    public bool CanAffordMoney(float amount)
    {
        return _currentMoney >= amount;
    }

    /// <summary>
    /// Пытается потратить деньги.
    /// Возвращает true, если оплата прошла успешно.
    /// Возвращает false, если денег не хватило.
    /// </summary>
    public bool SpendMoney(float amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Попытка потратить отрицательное кол-во денег.");
            return false;
        }

        if (CanAffordMoney(amount))
        {
            // Успех
            _currentMoney -= amount;
            return true;
        }
        else
        {
            // Провал, денег нет
            return false;
        }
    }
}