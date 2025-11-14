using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// (Новый struct) Временный "контейнер" для результата проверки 1 потребности.
/// </summary>
public struct NeedResult
{
    public Need need;  // Ссылка на "настройки" потребности (откуда брать бонусы)
    public bool isMet; // Результат (Да/Нет)
}


[RequireComponent(typeof(BuildingIdentity))]
public class Residence : MonoBehaviour
{
    [Header("Настройки Потребностей")]
    [Tooltip("Список того, что дом потребляет (напр. Рыба, Одежда)")]
    public List<Need> basicNeeds;
    [Tooltip("Как часто (в секундах) дом 'ходит в магазин'")]
    public float consumptionIntervalSeconds = 10f;

    [Header("Настройки Налогов")]
    [Tooltip("БАЗОВЫЙ налог (платится, даже если жители голодают)")]
    public float baseTaxAmount = 1f;
    
    // (Убраны 'happinessGain' и 'happinessPenalty', т.к. они теперь в Need.cs)

    // --- Ссылки на системы ---
    private BuildingIdentity _identity;
    private AuraManager _auraManager;
    private ResourceManager _resourceManager;
    private TaxManager _taxManager;
    private HappinessManager _happinessManager;

    private void Start()
    {
        // (Получаем ссылки... без изменений)
        _identity = GetComponent<BuildingIdentity>();
        _auraManager = AuraManager.Instance;
        _resourceManager = ResourceManager.Instance;
        _taxManager = TaxManager.Instance;
        _happinessManager = HappinessManager.Instance;
        
        if (_auraManager == null || _resourceManager == null || _taxManager == null || _happinessManager == null)
        {
            Debug.LogError($"[Residence] на {gameObject.name} не смог найти один из 'мозгов' (Managers). Экономика сломана.");
            this.enabled = false; 
            return;
        }

        StartCoroutine(ConsumeNeedsCoroutine());
    }

    /// <summary>
    /// Главный цикл жизни дома.
    /// </summary>
    private IEnumerator ConsumeNeedsCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(consumptionIntervalSeconds);

            if (_identity.isBlueprint)
            {
                continue; // "Проекты" не потребляют
            }

            // --- ЛОГИКА 3.0: Гранулярная проверка ---
            
            // 1. Проверяем доступ к Ауре (Сервисы)
            bool hasMarket = CheckServiceNeeds();
            
            // 2. Проверяем и "съедаем" Ресурсы (если есть доступ к Рынку)
            List<NeedResult> resourceResults;
            if (hasMarket)
            {
                // Если рынок есть - пытаемся "купить" (списать)
                resourceResults = CheckAndConsumeResourceNeeds();
            }
            else
            {
                // Если рынка нет - "проваливаем" ВСЕ потребности
                resourceResults = FailAllResourceNeeds();
            }

            // 3. Обрабатываем ВСЕ результаты (Счастье и Налоги)
            ProcessResults(resourceResults);
            
            // --- КОНЕЦ ЛОГИКИ 3.0 ---
        }
    }

    /// <summary>
    /// (Шаг А) Проверяет доступ к "сервисным" зданиям (Рынок).
    /// </summary>
    private bool CheckServiceNeeds()
    {
        // (Пока проверяем только Рынок, как и раньше)
        bool hasMarket = _auraManager.IsPositionInAura(transform.position, AuraType.Market);
        
        if (!hasMarket)
        {
            Debug.Log($"[Residence] {gameObject.name} не имеет доступа к Рынку!"); 
        }
        return hasMarket;
    }

    /// <summary>
    /// (Шаг Б - УСПЕХ) Пытается "купить" (списать) ресурсы и возвращает отчет.
    /// </summary>
    private List<NeedResult> CheckAndConsumeResourceNeeds()
    {
        float intervalAsFractionOfMinute = consumptionIntervalSeconds / 60f;
        var results = new List<NeedResult>();

        // --- ПЕРЕДЕЛКА: "По-предметная" проверка ---
        results.Clear();
        foreach (var need in basicNeeds)
        {
            float amountToConsume = need.amountPerMinute * intervalAsFractionOfMinute;

            // Пытаемся "купить" (списать) ЭТОТ ОДИН предмет
            bool success = _resourceManager.TakeFromStorage(need.resourceType, amountToConsume) > 0;

            results.Add(new NeedResult { need = need, isMet = success });

            if (!success)
                Debug.Log($"[Residence] {gameObject.name} не хватило {need.resourceType}!");
        }

        return results;
    }

    /// <summary>
    /// (Шаг Б - ПРОВАЛ) "Проваливает" все потребности (т.к. нет рынка).
    /// </summary>
    private List<NeedResult> FailAllResourceNeeds()
    {
        var results = new List<NeedResult>();
        foreach (var need in basicNeeds)
        {
            results.Add(new NeedResult { need = need, isMet = false });
        }
        return results;
    }

    /// <summary>
    /// (Шаг В) Обрабатывает гранулярные результаты.
    /// </summary>
    private void ProcessResults(List<NeedResult> resourceResults)
    {
        float totalHappinessChange = 0;
        float totalTax = baseTaxAmount; // Начинаем с "базового" налога

        // (Рынок мы уже проверили в 'CheckResourceNeeds')
        
        foreach (var result in resourceResults)
        {
            if (result.isMet)
            {
                // УСПЕХ (напр. "Рыба" куплена)
                totalHappinessChange += result.need.happinessBonus;
                totalTax += result.need.taxBonusPerCycle; // Платит БОЛЬШЕ (т.к. счастлив)
            }
            else
            {
                // ПРОВАЛ (напр. "Нет Рыбы" или "Нет Рынка")
                totalHappinessChange += result.need.happinessPenalty;
                // (Налог не увеличивается)
            }
        }

        // 3. "Отправляем" итоговые цифры в "мозги"
        _happinessManager.AddHappiness(totalHappinessChange);
        _taxManager.CollectTax(totalTax);
    }
}