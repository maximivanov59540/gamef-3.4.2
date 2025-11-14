using System;
[Serializable]
public class StorageData
{
    public ResourceType resourceType;
    public float currentAmount;
    public float maxAmount;

    public StorageData(float initialAmount, float initialMax)
    {
        currentAmount = initialAmount;
        maxAmount = initialMax;
    }
}