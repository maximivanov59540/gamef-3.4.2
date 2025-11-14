using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BuildSlot
{
    [Tooltip("Имя слота (для удобства в Инспекторе)")]
    public string slotName;

    [Tooltip("Локальная позиция (X, Y) от 'корня' зоны")]
    public Vector2Int localPosition;

    [Tooltip("Размер (X, Y) здания, которое можно здесь построить")]
    public Vector2Int size = Vector2Int.one;

    [Tooltip("Какое здание сейчас занимает этот слот (null, если пусто)")]
    public BuildingIdentity occupiedBy = null;

    [Tooltip("Какие здания допускаются в этот слот (пусто = любые, если по размеру подходит).")]
    public List<BuildingData> allowedBuildings = new List<BuildingData>();
}
