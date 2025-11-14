using UnityEngine;

public class BuildingIdentity : MonoBehaviour
{
    public BuildingData buildingData;
    public Vector2Int rootGridPosition; 
    
    // --- НОВЫЕ СТРОКИ ---
    public float yRotation = 0f;     
    public bool isBlueprint = false; 
    // --- КОНЕЦ ---
}