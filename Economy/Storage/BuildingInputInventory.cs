// --- BuildingInputInventory.cs ---
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // <--- ВАЖНО: Добавили это

/// <summary>
/// "Входной" инвентарь (буфер сырья) для производственного здания.
/// </summary>
[RequireComponent(typeof(BuildingIdentity))]
public class BuildingInputInventory : MonoBehaviour
{
    [Tooltip("Список требуемого сырья и его вместимость (настраивается в Инспекторе)")]
    public List<StorageData> requiredResources;
    
    [Header("Логистика Запросов")]
    [Tooltip("Приоритет доставки (1-5). Тележки выберут того, у кого '5'")]
    [Range(1, 5)] public int priority = 3;
    
    [Tooltip("Создать 'Запрос', когда склад опустеет до этого % (0.0 - 1.0)")]
    [Range(0f, 1f)] public float requestThresholdPercent = 0.25f; // 25%

    [Tooltip("Снять 'Запрос', когда склад заполнится до этого % (0.0 - 1.0)")]
    [Range(0f, 1f)] public float fulfillThresholdPercent = 0.8f; // 80%

    private Dictionary<ResourceType, ResourceRequest> _activeRequests = new Dictionary<ResourceType, ResourceRequest>();
    
    private BuildingIdentity _identity;
    private LogisticsManager _logistics;
    public bool IsRequesting { get; private set; } = false;

    private void Start()
    {
        _identity = GetComponent<BuildingIdentity>();
        _logistics = LogisticsManager.Instance;
        
        if (_logistics == null)
        {
            Debug.LogError($"[InputInv] {gameObject.name} не нашел LogisticsManager.Instance!");
        }
    }

    private void Update()
    {
        if (_logistics == null) return;
        
        // Проверяем КАЖДЫЙ слот сырья
        foreach (var slot in requiredResources)
        {
            // --- НОВЫЙ КОД: Защита от деления на ноль ---
            if (slot.maxAmount <= 0) continue; 
            // --- КОНЕЦ НОВОГО КОДА ---

            bool isRequestActive = _activeRequests.ContainsKey(slot.resourceType);
            float fillRatio = slot.currentAmount / slot.maxAmount;

            // 1. ЛОГИКА СОЗДАНИЯ ЗАПРОСА
            if (!isRequestActive && fillRatio <= requestThresholdPercent)
            {
                CreateRequest(slot);
            }
            // 2. ЛОГИКА ОТМЕНЫ ЗАПРОСА
            else if (isRequestActive && fillRatio >= fulfillThresholdPercent)
            {
                FulfillRequest(slot);
            }
        }
    }

    private void CreateRequest(StorageData slot)
    {
        var newRequest = new ResourceRequest(
            this, 
            slot.resourceType, 
            priority,
            _identity.rootGridPosition
        );
        
        _logistics.CreateRequest(newRequest);
        _activeRequests[slot.resourceType] = newRequest;
        UpdateIsRequesting();
    }

    private void FulfillRequest(StorageData slot)
    {
        if (_activeRequests.TryGetValue(slot.resourceType, out ResourceRequest request))
        {
            _logistics.FulfillRequest(request);
            _activeRequests.Remove(slot.resourceType);
            UpdateIsRequesting();
        }
    }


    // --- ⬇️ НОВЫЕ МЕТОДЫ ⬇️ ---

    /// <summary>
    /// Проверяет, достаточно ли сырья для ОДНОГО цикла по "рецепту".
    /// Вызывается из ResourceProducer.
    /// </summary>
    public bool HasResources(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return true; // Если рецепт не требует сырья (напр. Лесопилка)
        }
        
        foreach (var cost in costs)
        {
            // Ищем нужный "слот" на нашем складе
            StorageData slot = GetSlotForResource(cost.resourceType);
            if (slot == null || slot.currentAmount < cost.amount)
            {
                return false; // Не нашли слот ИЛИ в нем не хватает
            }
        }
        return true; // Все нашли, всего хватает
    }

    /// <summary>
    /// "Съедает" ресурсы за ОДИН цикл по "рецепту".
    /// Вызывается из ResourceProducer.
    /// </summary>
    public void ConsumeResources(List<ResourceCost> costs)
    {
        if (costs == null) return;
        
        foreach (var cost in costs)
        {
            StorageData slot = GetSlotForResource(cost.resourceType);
            if (slot != null)
            {
                slot.currentAmount -= cost.amount;
            }
        }
    }

    /// <summary>
    /// Добавляет сырье, привезенное тележкой.
    /// (Мы меняем 'float amount' на 'int amount' для совместимости)
    /// </summary>
    public float AddResource(ResourceType type, float amount) // Оставляем float для CartAgent
    {
        StorageData slot = GetSlotForResource(type);
        if (slot == null)
        {
            return 0; // Этот завод не принимает такой тип ресурса
        }
        
        float spaceAvailable = slot.maxAmount - slot.currentAmount;
        if (spaceAvailable <= 0) return 0;

        float amountToAdd = Mathf.Min(amount, spaceAvailable);
        slot.currentAmount += amountToAdd;

        return amountToAdd;
    }
    
    /// <summary>
    /// Хелпер: находит слот по типу ресурса
    /// </summary>
    private StorageData GetSlotForResource(ResourceType type)
    {
        // Linq-магия: "Найди первый (FirstOrDefault) слот (s)
        // у которого тип (s.resourceType) равен типу (type), который мы ищем"
        return requiredResources.FirstOrDefault(s => s.resourceType == type);
    }
    
    // --- ⬆️ КОНЕЦ НОВЫХ МЕТОДОВ ⬆️ ---

    private void UpdateIsRequesting()
    {
        IsRequesting = _activeRequests.Count > 0;
    }
}