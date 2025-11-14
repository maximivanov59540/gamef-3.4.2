using UnityEngine;

/// <summary>
/// "Мозг" экономики, отвечающий за сбор налогов (денег).
/// </summary>
public class TaxManager : MonoBehaviour
{
    public static TaxManager Instance { get; private set; }

    // Ссылка на нашу КАЗНУ
    private MoneyManager _moneyManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // На старте находим нашу казну.
        _moneyManager = MoneyManager.Instance;
        if (_moneyManager == null)
        {
            Debug.LogError("TaxManager не смог найти MoneyManager.Instance!");
        }
    }

    /// <summary>
    /// Главный метод, который вызывают дома для уплаты налога (деньгами).
    /// </summary>
    /// <param name="amount">Сколько денег платим</param>
    public void CollectTax(float amount)
    {
        if (amount <= 0 || _moneyManager == null) return;

        // Просто передаем деньги в казну
        _moneyManager.AddMoney(amount);
    }
}