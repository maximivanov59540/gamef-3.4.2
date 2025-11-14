using UnityEngine;

[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;  // Тип ресурса (например, дерево, камень)
    public int amount;  // Количество ресурса

    // Конструктор для удобства создания объекта
    public ResourceCost(ResourceType type, int amount)
    {
        this.resourceType = type;
        this.amount = amount;
    }
}
