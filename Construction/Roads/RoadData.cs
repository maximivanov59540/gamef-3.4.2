// --- RoadData.cs ---
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RD_NewRoad", menuName = "Road System/Road Data")]
public class RoadData : ScriptableObject
{
    [Header("Инфо")]
    public string roadName = "Новая Дорога";

    [Header("Геймплей")]
    [Tooltip("Визуальный префаб этой дороги (PF_Road_Sand, PF_Road_Stone...)")]
    public GameObject roadPrefab;

    [Tooltip("Множитель скорости для тележек (1.0 = 100%, 1.5 = 150%)")]
    public float speedMultiplier = 1.0f;

    [Header("Апгрейд")]
    [Tooltip("Во что эта дорога улучшается (напр., RD_StoneRoad). Оставь пустым, если это 'финал'.")]
    public RoadData upgradeTarget;

    [Tooltip("Стоимость апгрейда ДО ЭТОЙ дороги (напр., Мощеная стоит 1 Камень)")]
    public List<ResourceCost> upgradeCost;
}