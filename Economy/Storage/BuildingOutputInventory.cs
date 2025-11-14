// --- BuildingOutputInventory.cs ---
using UnityEngine;

public class BuildingOutputInventory : MonoBehaviour
{
    [Tooltip("Какой ресурс производим и его вместимость (настраивается в Инспекторе)")]
    public StorageData outputResource;

    public event System.Action OnFull;
    public event System.Action OnSpaceAvailable;
    
    private bool _wasFull = false;

    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД ⬇️ ---
    /// <summary>
    /// Проверяет, есть ли место (вызывается из ResourceProducer).
    /// </summary>
    public bool HasSpace(int amountToAdd)
    {
        // (Мы оставляем небольшой буфер '0.01f' на случай ошибок округления)
        return outputResource.currentAmount + amountToAdd <= outputResource.maxAmount + 0.01f;
    }

    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД ⬇️ ---
    /// <summary>
    /// Добавляет готовую продукцию (вызывается из ResourceProducer).
    /// (Меняем float на int)
    /// </summary>
    public void AddResource(int amount)
    {
        outputResource.currentAmount += amount;
        
        if (outputResource.currentAmount >= outputResource.maxAmount)
        {
            outputResource.currentAmount = outputResource.maxAmount;
            
            if (!_wasFull)
            {
                _wasFull = true;
                OnFull?.Invoke(); // Сообщаем: "Я ПОЛОН!"
            }
        }
    }

    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД ⬇️ ---
    /// <summary>
    /// Забирает продукцию (вызывается тележкой CartAgent).
    /// (Меняем float на int)
    /// </summary>
    /// <returns>Сколько РЕАЛЬНО удалось забрать.</returns>
    public int TakeResource(int amountToTake)
    {
        // Округляем ВНИЗ то, что лежит на складе, до целого
        int amountAvailable = Mathf.FloorToInt(outputResource.currentAmount);
        
        int amountTaken = Mathf.Min(amountToTake, amountAvailable);
        if (amountTaken <= 0) return 0;
        
        outputResource.currentAmount -= amountTaken;
        
        if (_wasFull && outputResource.currentAmount < outputResource.maxAmount)
        {
            _wasFull = false;
            OnSpaceAvailable?.Invoke(); // Сообщаем: "ЕСТЬ МЕСТО!"
        }
        
        return amountTaken;
    }

    /// <summary>
    /// Используется тележкой, чтобы решить, стоит ли ехать.
    /// (Проверяем, что есть хотя бы 1.0)
    /// </summary>
    public bool HasAtLeastOneUnit()
    {
        return outputResource.currentAmount >= 1f;
    }

    // --- ⬇️ ИЗМЕНЕННЫЙ МЕТОД ⬇️ ---
    /// <summary>
    /// Используется тележкой (старый метод от BuildingInventory).
    /// (Теперь возвращает int)
    /// </summary>
    public int TakeAllResources()
    {
        // Берем все, что есть, округляя до целого
        int amountToTake = Mathf.FloorToInt(outputResource.currentAmount);
        return TakeResource(amountToTake);
    }
    
    /// <summary>
    /// Используется тележкой (старый метод от BuildingInventory).
    /// </summary>
    public ResourceType GetResourceType()
    {
        return outputResource.resourceType;
    }
}