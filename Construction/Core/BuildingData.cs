using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Building Data", menuName = "Building System/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Info")]
    public string buildingName;
    public string description;
    public Sprite icon;

    [Header("Building Properties")]
    public List<ResourceCost> costs;
    public int housingCapacity = 0;
    public GameObject buildingPrefab;
    public Vector2Int size = new Vector2Int(1, 1);
    
    [Tooltip("Это 'Модуль' (Поле, Пастбище), который 'принадлежит' другому зданию?")]
    public bool isModule = false; // <-- НОВАЯ СТРОКА

    [Header("Настройки Инструмента")]
    [Tooltip("Включить 'Инструмент Улица' (Т-3) для этого здания?")]
    public bool useMassBuildTool = false;
    [Header("Экономика")]
    [Tooltip("Стоимость постройки в золоте")]
    public float moneyCost = 0;
    [Tooltip("Стоимость содержания (золота в минуту)")]
    public float upkeepCostPerMinute = 1;
}