// --- RoadTile.cs ---
using UnityEngine;

public class RoadTile : MonoBehaviour
{
    [Tooltip("Ссылка на 'чертеж' этой дороги (RD_SandRoad, RD_StoneRoad...)")]
    public RoadData roadData;
    
    // (Удали или закомментируй старое поле 'public float speedMultiplier;')
}