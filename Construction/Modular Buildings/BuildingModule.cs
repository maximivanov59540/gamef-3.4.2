using UnityEngine;

public class BuildingModule : MonoBehaviour
{
    public ModularBuilding parentBuilding;
    public Vector2Int gridPosition; // "Корень" (левый нижний угол) модуля
    public Vector2Int size;         // <-- НОВАЯ СТРОКА (напр., 1x1 или 3x3)

    void OnDestroy()
    {
        if (parentBuilding != null)
        {
            parentBuilding.UnregisterModule(this);
        }
    }
}