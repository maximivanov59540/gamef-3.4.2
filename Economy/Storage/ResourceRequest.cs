using UnityEngine;
public class ResourceRequest
{
    public BuildingInputInventory Requester; // Ссылка на компонент "входного" склада, который создал запрос
    public ResourceType RequestedType;       // Какой ресурс нужен
    public int Priority;                     // Приоритет (1-5)
    public Vector2Int DestinationCell;       // Координаты клетки "входа" (обычно root-клетка здания)

    public ResourceRequest(BuildingInputInventory requester, ResourceType requestedType, int priority, Vector2Int destinationCell)
    {
        Requester = requester;
        RequestedType = requestedType;
        Priority = priority;
        DestinationCell = destinationCell;
    }
}