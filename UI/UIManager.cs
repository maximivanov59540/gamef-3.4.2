using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
// --- НУЖНО ДОБАВИТЬ ЭТО ---
using UnityEngine.UI; 

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TextMeshProUGUI buildingNameText;
    [Header("Панель Подтверждения")]
    [SerializeField] private GameObject _confirmationPanel; 
    [SerializeField] private TextMeshProUGUI _confirmationText; 
    
    [Header("Контекстные Кнопки (Модули)")]
    [Tooltip("КОНТЕЙНЕР (пустой GameObject), куда будут 'спавниться' кнопки модулей")]
    [SerializeField] private GameObject moduleButtonContainer; // <-- ИЗМЕНЕНО (Был 'addModuleButton')

    [Tooltip("ПРЕФАБ кнопки 'UI_ModuleButton'")]
    [SerializeField] private GameObject moduleButtonPrefab; // <-- НОВАЯ СТРОКА
    [Header("Панель Производства")]
    [Tooltip("Весь 'контейнер' со слайдером (панель)")]
    [SerializeField] private GameObject productionPanel;
    [Tooltip("Сам Слайдер")]
    [SerializeField] private Slider productivitySlider;
    [Tooltip("Текст для отображения % (напр. '100%')")]
    [SerializeField] private TextMeshProUGUI productivityText;
    // --- Приватные ссылки ---
    private ResourceProducer _selectedProducer; 
    private ModularBuilding _selectedFarm;
    private System.Action _onConfirmAction;
    private System.Action _onCancelAction;
    private ZonedArea _selectedZone;
    private NotificationManager _notificationManager;
    


    void Start()
    {
        HideInfo();
        _confirmationPanel.SetActive(false);

        // --- ИЗМЕНЕНО ---
        if (moduleButtonContainer != null)
        {
            moduleButtonContainer.SetActive(false);
        }
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---
        if (productionPanel) 
            productionPanel.SetActive(false);
        // Находим NotificationManager (для Шага 2.0, но он нужен и здесь)
        _notificationManager = FindFirstObjectByType<NotificationManager>();

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
        else
            Debug.LogError("UIManager не нашел SelectionManager!");
        _notificationManager = FindFirstObjectByType<NotificationManager>();
    }
    
    private void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;
    }
    
    private void OnSelectionChanged(IReadOnlyCollection<BuildingIdentity> selection)
    {
        HideInfo(); // Сначала всегда "прячем"
        
        if (selection.Count == 1)
        {
            BuildingIdentity selectedBuilding = selection.First();
            if (selectedBuilding == null) return;
            
            ShowInfo(selectedBuilding.buildingData); // 1. Общая инфо

            // --- 2. ПРОВЕРКА: "Ферма"? (Логика 2.0) ---
            ModularBuilding farm = selectedBuilding.GetComponent<ModularBuilding>();
            if (farm != null)
            {
                _selectedFarm = farm;
                ShowModuleButtons(farm); // <-- НОВЫЙ "УМНЫЙ" МЕТОД
            }
            // 3. "ИЛИ" "Монастырь"?
            else
            {
                ZonedArea zone = selectedBuilding.GetComponent<ZonedArea>();
                if (zone != null)
                {
                    _selectedZone = zone;
                    zone.ShowSlotHighlights();
                }
            }
            ResourceProducer producer = selectedBuilding.GetComponent<ResourceProducer>();
            if (producer != null)
            {
                _selectedProducer = producer;
                ShowProductionControls(producer); // "Включаем" слайдер
            }
        }
    }
    
    public void ShowInfo(BuildingData data)
    {
        if(data == null) return;
        infoPanel.SetActive(true);
        buildingNameText.text = data.buildingName;
    }

    public void HideInfo()
    {
        infoPanel.SetActive(false);
        buildingNameText.text = "";

        // --- ИЗМЕНЕНО ---
        // "Прячем" и "чистим" кнопки
        ClearModuleButtons(); 
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---
        if (productionPanel) 
            productionPanel.SetActive(false);
        if (productivitySlider) 
            productivitySlider.onValueChanged.RemoveAllListeners();

        _selectedFarm = null;
        
        if (_selectedZone != null)
        {
            _selectedZone.HideSlotHighlights();
            _selectedZone = null;
        }
    }
    
    // --- "Остальной" "код" (Confirmation) "без" "изменений" ---
    public void ShowConfirmation(string message, System.Action onConfirm, System.Action onCancel)
    {
        _onConfirmAction = onConfirm;
        _onCancelAction = onCancel;
        _confirmationText.text = message;
        _confirmationPanel.SetActive(true);
    }
    public void OnConfirmButton()
    {
        _confirmationPanel.SetActive(false);
        _onConfirmAction?.Invoke();
        _onConfirmAction = null; _onCancelAction = null;
    }
    public void OnCancelButton()
    {
        _confirmationPanel.SetActive(false);
        _onCancelAction?.Invoke();
        _onConfirmAction = null; _onCancelAction = null;
    }
    
    // --- ⬇️ НОВЫЕ МЕТОДЫ ДЛЯ КНОПОК МОДУЛЕЙ (Шаг 2.0) ⬇️ ---

    /// <summary>
    /// "Чистит" "контейнер" кнопок (вызывается из HideInfo).
    /// </summary>
    private void ClearModuleButtons()
    {
        if (moduleButtonContainer == null) return;
        
        foreach (Transform child in moduleButtonContainer.transform)
        {
            Destroy(child.gameObject);
        }
        moduleButtonContainer.SetActive(false);
    }
    
    /// <summary>
    /// "Создает" "и" "настраивает" "кнопки" "для" "выбранной" "Фермы".
    /// </summary>
    private void ShowModuleButtons(ModularBuilding farm)
    {
        if (moduleButtonContainer == null || moduleButtonPrefab == null) return;

        ClearModuleButtons(); // На всякий случай
        
        // "Показываем" "контейнер" (даже если кнопок не будет, 
        // он может быть "фоном" для текста "Нет модулей")
        moduleButtonContainer.SetActive(true); 

        foreach (BuildingData moduleBP in farm.allowedModules)
        {
            // 1. "Создаем" "кнопку" "из" "префаба"
            GameObject btnGO = Instantiate(moduleButtonPrefab, moduleButtonContainer.transform);

            // 2. "Настраиваем" "текст"
            TextMeshProUGUI txt = btnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (txt)
            {
                txt.text = $"{moduleBP.buildingName} ({moduleBP.size.x}x{moduleBP.size.y})";
            }

            // 3. "Настраиваем" "клик" "и" "доступность"
            Button btn = btnGO.GetComponent<Button>();
            if (btn)
            {
                // Проверяем лимит
                bool canBuild = farm.CanAddModule();
                btn.interactable = canBuild;
                
                // "Вешаем" "событие" "на" "клик"
                btn.onClick.AddListener(() => OnClick_BuildModule(moduleBP));
            }
        }
    }

    /// <summary>
    /// "Обработчик" "клика", "который" "вызывается" "динамической" "кнопкой".
    /// </summary>
    public void OnClick_BuildModule(BuildingData moduleToBuild)
    {
        if (_selectedFarm == null)
        {
            Debug.LogError("Кнопка 'Build Module' нажата, но _selectedFarm == null!");
            return;
        }
        
        if (!_selectedFarm.CanAddModule())
        {
            _notificationManager.ShowNotification("Достигнут лимит модулей!");
            return;
        }

        // "Отдаем" "приказ" "Контроллеру" "Ввода"
        PlayerInputController.Instance.EnterPlacingModuleMode(_selectedFarm, moduleToBuild);
    }

    // --- ⬆️ КОНЕЦ НОВЫХ МЕТОДОВ ⬆️ ---
/// <summary>
    /// Показывает и настраивает панель слайдера.
    /// </summary>
    private void ShowProductionControls(ResourceProducer producer)
    {
        if (productionPanel == null || productivitySlider == null) return;
        
        productionPanel.SetActive(true);
        
        // "Считываем" "текущую" "эффективность" "из" "мозга" "завода"
        float currentEfficiency = producer.GetEfficiency();
        
        // "Выставляем" "слайдер" "в" "это" "значение"
        productivitySlider.value = currentEfficiency;
        
        // "Обновляем" "текст" ("125%")
        UpdateEfficiencyText(currentEfficiency);

        // "Подписываемся" "на" "движение" "слайдера"
        productivitySlider.onValueChanged.AddListener(OnEfficiencySliderChanged);
    }

    /// <summary>
    /// Вызывается, когда игрок "двигает" "слайдер".
    /// </summary>
    private void OnEfficiencySliderChanged(float value)
    {
        // (Предполагаем, что Min/Max слайдера в Unity = 0.5 и 1.5)
        
        if (_selectedProducer != null)
        {
            // 1. Отправляем "приказ" в "мозг"
            _selectedProducer.SetEfficiency(value);
            
            // 2. Обновляем текст
            UpdateEfficiencyText(value);
        }
    }

    /// <summary>
    /// Хелпер для текста (1.25 -> "125%")
    /// </summary>
    private void UpdateEfficiencyText(float value)
    {
        if (productivityText)
        {
            productivityText.text = $"{value * 100:F0}%"; // "F0" = 0 знаков после запятой
        }
    }
}