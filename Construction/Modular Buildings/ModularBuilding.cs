using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Компонент для "главного" здания (например, Фермы), которое
/// может "владеть" несколькими "модулями" (например, Полями).
/// </summary>
public class ModularBuilding : MonoBehaviour
{
    [Header("Настройки Модулей")]
    [Tooltip("Список 'Чертежей' (BuildingData) для модулей, которые строит это здание (напр., BD_Field, BD_Pasture)")]
    // БЫЛО: public BuildingData moduleData;
    public List<BuildingData> allowedModules = new List<BuildingData>(); // <-- ИЗМЕНЕНО

    [Tooltip("Максимальное кол-во модулей для этого здания")]
    public int maxModuleCount = 4; // <-- НОВАЯ СТРОКА
    
    [Header("Производство (Опционально)")]
    [Tooltip("Ссылка на Производителя на этом же здании (для бонусов)")]
    public ResourceProducer attachedProducer; // <-- НОВАЯ СТРОКА

    [SerializeField]
    private List<BuildingModule> _modules = new List<BuildingModule>();

    private void Awake()
    {
        // Попытка "схватить" продюсера, если не назначен в инспекторе
        if (attachedProducer == null)
            attachedProducer = GetComponent<ResourceProducer>();
    }

    /// <summary>
    /// Вызывается, когда мы "пристраиваем" новый модуль.
    /// </summary>
    public void RegisterModule(BuildingModule module)
    {
        if (!_modules.Contains(module))
        {
            _modules.Add(module);
            module.parentBuilding = this;
            
            // --- НОВЫЙ КОД ---
            // Сообщаем продюсеру, что кол-во модулей изменилось
            attachedProducer?.UpdateProductionRate(GetModuleCount());
            // --- КОНЕЦ НОВОГО КОДА ---
            
            Debug.Log($"Модуль добавлен к {gameObject.name}. Всего модулей: {GetModuleCount()} / {maxModuleCount}");
        }
    }

    /// <summary>
    /// Вызывается, когда модуль "уничтожается".
    /// </summary>
    public void UnregisterModule(BuildingModule module)
    {
        if (_modules.Contains(module))
        {
            _modules.Remove(module);
            
            // --- НОВЫЙ КОД ---
            // Сообщаем продюсеру, что кол-во модулей изменилось
            attachedProducer?.UpdateProductionRate(GetModuleCount());
            // --- КОНЕЦ НОВОГО КОДА ---
            
            Debug.Log($"Модуль удален из {gameObject.name}. Всего модулей: {GetModuleCount()} / {maxModuleCount}");
        }
    }
    
    // --- НОВЫЕ ПУБЛИЧНЫЕ МЕТОДЫ ---

    /// <summary>
    /// Возвращает текущее кол-во модулей (для UI и Продюсера).
    /// </summary>
    public int GetModuleCount() => _modules.Count;

    /// <summary>
    /// Проверяет, не достигнут ли лимит (для UI).
    /// </summary>
    public bool CanAddModule() => GetModuleCount() < maxModuleCount;
    
    // --- КОНЕЦ НОВЫХ МЕТОДОВ ---


    /// <summary>
    /// Возвращает все модули (напр., для GridSystem при сносе).
    /// </summary>
    public List<BuildingModule> GetRegisteredModules()
    {
        return new List<BuildingModule>(_modules);
    }
}