using UnityEngine;
using System.Collections.Generic;

public class BuildUIManager : MonoBehaviour
{
    [Header("Ссылки на Менеджеры")]
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private PlayerInputController inputController;

    [Header("Элементы UI")]
    [SerializeField] private GameObject buildActionsPanel;

    private bool _isMasterBuildMode = false; 

    void Start()
    {
        if (buildActionsPanel != null)
        {
            buildActionsPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("BuildUIManager: Не задана 'Build Actions Panel' в инспекторе!");
        }

        if (buildingManager == null)
        {
            Debug.LogError("BuildUIManager: Не задан 'Building Manager' в инспекторе!");
        }
        if (inputController == null)
        {
            inputController = FindFirstObjectByType<PlayerInputController>();
            if (inputController == null)
            {
                Debug.LogError("BuildUIManager: Не найден 'PlayerInputController' в сцене!", this);
            }
        }
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК ---

    // 1. Для ГЛАВНОЙ кнопки "Режим строительства" (MasterBuildModeButton)
    public void ToggleMasterBuildMode()
    {
        _isMasterBuildMode = !_isMasterBuildMode;
        buildActionsPanel.SetActive(_isMasterBuildMode);

        if (_isMasterBuildMode)
        {
            buildingManager.ShowGrid(true);
        }
        else
        {
            inputController.SetMode(InputMode.None);
            buildingManager.ShowGrid(false);

#if UNITY_2022_2_OR_NEWER
            foreach (var zone in FindObjectsByType<ZonedArea>(FindObjectsSortMode.None))
                zone.HideSlotHighlights();
#else
    foreach (var zone in FindObjectsOfType<ZonedArea>())
        zone.HideSlotHighlights();
#endif
        }

    }

    private void ActivateBuildUI()
    {
        if (_isMasterBuildMode) return;
        _isMasterBuildMode = true;
        buildActionsPanel.SetActive(true);
        buildingManager.ShowGrid(true); 
    }
    
    // 2. Кнопка "Переместить"
    public void OnClickMoveButton()
    {
        if (inputController == null) return;
        ActivateBuildUI(); 
        inputController.SetMode(InputMode.Moving);
    }

    // 3. Кнопка "Удалить"
    public void OnClickDeleteButton()
    {
        if (inputController == null) return;
        ActivateBuildUI();
        inputController.SetMode(InputMode.Deleting);
    }

    // 4. Кнопка "Улучшить"
    public void OnClickUpgradeButton()
    {
        if (inputController == null) return;
        ActivateBuildUI();
        inputController.SetMode(InputMode.Upgrading);
    }

    // 5. Кнопка "Копировать"
    public void OnClickCopyButton()
    {
        if (inputController == null) return;
        ActivateBuildUI();
        inputController.SetMode(InputMode.Copying);
    }

    // --- ⬇️ НОВЫЙ МЕТОД ДЛЯ КНОПКИ "ДОРОГИ" (Шаг А4) ⬇️ ---

    /// <summary>
    /// Вызывается кнопкой "Строить Дорогу" из UI.
    /// </summary>
    public void OnClickRoadButton()
    {
        if (inputController == null) return;
        ActivateBuildUI(); // Активируем UI строительства (сетку и т.д.)

        // Переключаем контроллер в НОВЫЙ режим
        inputController.SetMode(InputMode.RoadBuilding);
    }
    // --- ⬆️ КОНЕЦ НОВОГО МЕТОДА ⬆️ ---
    public void OnClickBuildBuilding(BuildingData data)
    {
        if (PlayerInputController.CurrentInputMode != InputMode.Building)
        {
            inputController.SetMode(InputMode.Building);
        }
        buildingManager.EnterBuildMode(data);

        // ⬇️ ДОБАВЬ ЭТО: подсветить только подходящие слоты под выбранное здание
#if UNITY_2022_2_OR_NEWER
        foreach (var zone in FindObjectsByType<ZonedArea>(FindObjectsSortMode.None))
            zone.ShowSlotHighlights(data);
#else
    foreach (var zone in FindObjectsOfType<ZonedArea>())
        zone.ShowSlotHighlights(data);
#endif
    }
}