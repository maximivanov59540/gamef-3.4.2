// --- EconomyDataTypes.cs ---
using System;
using System.Collections.Generic; // <--- ВАЖНО: Добавили это
using UnityEngine; // <--- ВАЖНО: Добавили это

[Serializable]
public class ResourceProductionData
{
    [Tooltip("Сколько секунд занимает ОДИН цикл производства")]
    public float cycleTimeSeconds = 10f;
    
    [Tooltip("Список того, ЧТО и СКОЛЬКО мы тратим за цикл (напр., 1 Дерево)")]
    public List<ResourceCost> inputCosts;
    
    [Tooltip("То, ЧТО и СКОЛЬКО мы производим за цикл (напр., 1 Доска)")]
    public ResourceCost outputYield; 
}