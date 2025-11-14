using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class GridSystem : MonoBehaviour
{
    // ... (все поля Awake, Update, GetWorldPosition, GetXZ, IsCellOccupied, GetCellSize, OccupyCells - БЕЗ ИЗМЕНЕНИЙ) ...
    public static Vector2Int MouseGridPosition { get; private set; } = new Vector2Int(-1, -1);
    public static Vector3 MouseWorldPosition { get; private set; } = Vector3.zero;
    public int GetGridWidth()  => gridWidth;
    public int GetGridHeight() => gridHeight;
    [SerializeField] private int gridWidth = 500;
    [SerializeField] private int gridHeight = 500;
    [SerializeField] private float cellSize = 1.0f;
    private GridCellData[,] gridCells;
    private float _groundYLevel = 0f;
    private BuildingManager buildingManager;
    private UIManager uiManager;
    private int groundLayerMask;

    void Awake()
    {
        gridCells = new GridCellData[gridWidth, gridHeight];
        groundLayerMask = 1 << LayerMask.NameToLayer("Ground");
        Ray ray = new Ray(new Vector3(10, 100, 10), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 200f, groundLayerMask))
        {
            _groundYLevel = hitInfo.point.y;
        }
        else
        {
            Debug.LogError("GridSystem НЕ МОЖЕТ НАЙТИ 'Ground'!");
        }
    }
    
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, 1000f, groundLayerMask);

        int currentX = -1, currentZ = -1;
        if (!isOverUI && hasHit)
        {
            GetXZ(hitInfo.point, out currentX, out currentZ);
            MouseWorldPosition = hitInfo.point;
            if (currentX < 0 || currentZ < 0 || currentX >= gridWidth || currentZ >= gridHeight)
            {
                currentX = -1;
                currentZ = -1;
            }
        }
        MouseGridPosition = new Vector2Int(currentX, currentZ);
    }
    
    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x * cellSize, _groundYLevel, z * cellSize);
    }

    public void GetXZ(Vector3 worldPosition, out int x, out int z)
    {
        x = Mathf.FloorToInt(worldPosition.x / cellSize);
        z = Mathf.FloorToInt(worldPosition.z / cellSize);
    }
    
    public bool IsCellOccupied(int x, int z)
    {
        if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight)
        {
            return true;
        }
        return gridCells[x, z].building != null ||
               gridCells[x, z].road != null ||
               gridCells[x, z].module != null;
    }

    public float GetCellSize() => cellSize;
    
    public void OccupyCells(BuildingIdentity identity, Vector2Int size)
    {
        Vector2Int root = identity.rootGridPosition;
        ZonedArea zone = identity.GetComponent<ZonedArea>();

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                int cellX = root.x + x;
                int cellZ = root.y + z;

                if (cellX >= 0 && cellZ >= 0 && cellX < gridWidth && cellZ < gridHeight)
                {
                    gridCells[cellX, cellZ].building = identity;
                    if (zone != null)
                    {
                        gridCells[cellX, cellZ].parentZone = zone;
                    }
                }
            }
        }
    }
    public List<Vector2Int> GetRoadsInRect(Vector2Int gridCornerA, Vector2Int gridCornerB)
    {
        List<Vector2Int> foundRoads = new List<Vector2Int>();
        int minX = Mathf.Min(gridCornerA.x, gridCornerB.x);
        int maxX = Mathf.Max(gridCornerA.x, gridCornerB.x);
        int minZ = Mathf.Min(gridCornerA.y, gridCornerB.y);
        int maxZ = Mathf.Max(gridCornerA.y, gridCornerB.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                // Проверяем .road, а не .building
                if (GetRoadTileAt(x, z) != null)
                {
                    foundRoads.Add(new Vector2Int(x, z));
                }
            }
        }
        return foundRoads;
    }


    /// <summary>
    /// "Умный" метод очистки ячейки (x, z).
    /// </summary>
    public void ClearCell(int x, int z)
    {
        GridCellData cell = GetCellData(x, z);
        BuildingIdentity identity = cell.building;
        BuildingModule module = cell.module;

        // --- СЦЕНАРИЙ 1: Клик на Модуль (Поле) ---
        if (module != null)
        {
            // --- ⬇️ ИЗМЕНЕНИЕ 2.0 (Удаление мульти-клеток) ⬇️ ---
            Vector2Int moduleRoot = module.gridPosition;
            Vector2Int moduleSize = module.size;

            // "Отсоединяем" "его" "от" "Фермы" (1 раз)
            if (module.parentBuilding != null)
            {
                module.parentBuilding.UnregisterModule(module);
            }
            
            // "Чистим" "ВСЕ" "клетки", "которые" "он" "занимал"
            for (int dx = 0; dx < moduleSize.x; dx++)
            {
                for (int dz = 0; dz < moduleSize.y; dz++)
                {
                    ClearCellModule(new Vector2Int(moduleRoot.x + dx, moduleRoot.y + dz));
                }
            }
            
            // "Уничтожаем" "объект" "Поля" (1 раз)
            Destroy(module.gameObject);
            return; 
            // --- ⬆️ КОНЕЦ ИЗМЕНЕНИЯ 2.0 ⬆️ ---
        }

        // --- СЦЕНАРИЙ 2: Клик на Здание (или пустоту) ---
        if (identity == null) return; 

        // (Расчет 'size' и 'root' для здания - без изменений)
        Vector2Int size = identity.buildingData.size;
        float yRotation = identity.yRotation; 
        if (Mathf.Abs(yRotation - 90f) < 1f || Mathf.Abs(yRotation - 270f) < 1f)
        {
            size = new Vector2Int(size.y, size.x);
        }
        Vector2Int root = identity.rootGridPosition;

        // (Логика для Зон, Ферм и Зданий в слотах - без изменений)
        ZonedArea zone = identity.GetComponent<ZonedArea>();
        if (zone != null)
        {
            ClearCells_Zone(root, size);
        }
        ModularBuilding farm = identity.GetComponent<ModularBuilding>();
        if (farm != null)
        {
            List<BuildingModule> modulesToClear = farm.GetRegisteredModules();
            foreach (var mod in modulesToClear)
            {
                // (Рекурсивный вызов ClearCell для каждого модуля)
                // (Это НЕПРАВИЛЬНО, т.к. UnregisterModule вызовется дважды)
                // (ПРОСТО чистим клетки и удаляем объект)
                Vector2Int modRoot = mod.gridPosition;
                Vector2Int modSize = mod.size;
                for (int dx = 0; dx < modSize.x; dx++)
                    for (int dz = 0; dz < modSize.y; dz++)
                        ClearCellModule(new Vector2Int(modRoot.x + dx, modRoot.y + dz));
                
                Destroy(mod.gameObject);
            }
        }
        if (cell.parentZone != null)
        {
            BuildSlot slot = cell.parentZone.GetSlotOccupiedBy(identity);
            if (slot != null) cell.parentZone.ClearSlot(slot);
        }

        // D. "Очищаем" "само" "здание"
        ClearCells_Building(root, size); 
        Destroy(identity.gameObject);
    }
    
    // (Приватные хелперы ClearCells_Building, ClearCells_Zone - БЕЗ ИЗМЕНЕНИЙ)
    private void ClearCells_Building(Vector2Int root, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                int cellX = root.x + x; int cellZ = root.y + z;
                if (cellX >= 0 && cellZ >= 0 && cellX < gridWidth && cellZ < gridHeight)
                    gridCells[cellX, cellZ].building = null;
            }
        }
    }
    private void ClearCells_Zone(Vector2Int root, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                int cellX = root.x + x; int cellZ = root.y + z;
                if (cellX >= 0 && cellZ >= 0 && cellX < gridWidth && cellZ < gridHeight)
                {
                    gridCells[cellX, cellZ].building = null;
                    gridCells[cellX, cellZ].parentZone = null;
                }
            }
        }
    }

    // (PickUpBuilding - БЕЗ ИЗМЕНЕНИЙ)
    public GameObject PickUpBuilding(int x, int z)
    {
        GridCellData cell = GetCellData(x, z);
        BuildingIdentity identity = cell.building;
        if (identity == null) return null;
        if (cell.parentZone != null && identity.GetComponent<ZonedArea>() == null) return null; 
        if (identity.GetComponent<ZonedArea>() != null) return null;
        if (identity.GetComponent<ModularBuilding>() != null) return null; 

        Vector2Int size = identity.buildingData.size;
        float yRotation = identity.yRotation; 
        if (Mathf.Abs(yRotation - 90f) < 1f || Mathf.Abs(yRotation - 270f) < 1f)
            size = new Vector2Int(size.y, size.x);
        
        ClearCells_Building(identity.rootGridPosition, size); 
        return identity.gameObject;
    }

    // (GetBuildingIdentityAt, GetBuildingsInRect, CanBuildAt - БЕЗ ИЗМЕНЕНИЙ)
    public BuildingIdentity GetBuildingIdentityAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight) return null;
        return gridCells[x, z].building;
    }
    public HashSet<BuildingIdentity> GetBuildingsInRect(Vector2Int gridCornerA, Vector2Int gridCornerB)
    {
        HashSet<BuildingIdentity> foundBuildings = new HashSet<BuildingIdentity>();
        int minX = Mathf.Min(gridCornerA.x, gridCornerB.x);
        int maxX = Mathf.Max(gridCornerA.x, gridCornerB.x);
        int minZ = Mathf.Min(gridCornerA.y, gridCornerB.y);
        int maxZ = Mathf.Max(gridCornerA.y, gridCornerB.y);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                BuildingIdentity id = GetBuildingIdentityAt(x, z);
                if (id != null) foundBuildings.Add(id);
            }
        }
        return foundBuildings;
    }
    public bool CanBuildAt(Vector2Int rootPos, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                if (IsCellOccupied(rootPos.x + x, rootPos.y + z))
                {
                    return false;
                }
            }
        }
        return true;
    }

    // (SetRoadTile, GetRoadTileAt - БЕЗ ИЗМЕНЕНИЙ)
    public void SetRoadTile(Vector2Int gridPos, RoadTile roadTileComponent)
    {
        if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridWidth || gridPos.y >= gridHeight) return;
        gridCells[gridPos.x, gridPos.y].road = roadTileComponent;
    }
    public RoadTile GetRoadTileAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight) return null;
        return gridCells[x, z].road;
    }
    
    // (GetCellData - БЕЗ ИЗМЕНЕНИЙ)
    public GridCellData GetCellData(int x, int z)
    {
        if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight)
            return new GridCellData(); 
        return gridCells[x, z];
    }

    // --- ⬇️ КРИТИЧЕСКИЙ ФИКС 2.0 (Удаляем 'gridPosition = gridPos') ⬇️ ---
    /// <summary>
    /// Помещает "модуль" (e.g., Поле) в сетку.
    /// (Вызывается из State_PlacingModule)
    /// </summary>
    public void SetModule(Vector2Int gridPos, BuildingModule module)
    {
        if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridWidth || gridPos.y >= gridHeight)
        {
            return;
        }
        
        gridCells[gridPos.x, gridPos.y].module = module;
        
        // (State_PlacingModule теперь сам отвечает за установку 'gridPosition'
        // в компоненте модуля. Эта строка УДАЛЕНА:)
        // if (module != null) { module.gridPosition = gridPos; }
    }
    // --- ⬆️ КОНЕЦ ФИКСА 2.0 ⬆️ ---
    
    // (GetModuleAt - БЕЗ ИЗМЕНЕНИЙ)
    public BuildingModule GetModuleAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight) return null;
        return gridCells[x, z].module;
    }

    // (ClearCellModule - БЕЗ ИЗМЕНЕНИЙ)
    private void ClearCellModule(Vector2Int gridPos)
    {
        if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridWidth || gridPos.y >= gridHeight)
            return;
        gridCells[gridPos.x, gridPos.y].module = null;
    }
}