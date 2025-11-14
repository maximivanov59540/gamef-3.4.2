using UnityEngine;
[System.Serializable]
public class Need
{
    public ResourceType resourceType;
    public float amountPerMinute;
    
    [Header("Баланс")]
    [Tooltip("Бонус к счастью, если потребность УДОВЛЕТВОРЕНА")]
    public float happinessBonus = 0.2f;
    [Tooltip("Штраф к счастью, если потребность ПРОВАЛЕНА")]
    public float happinessPenalty = -0.5f;
    [Tooltip("Дополнительные деньги (налог), если потребность УДОВЛЕТВОРЕНА")]
    public float taxBonusPerCycle = 0.5f;
}