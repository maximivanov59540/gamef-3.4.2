using UnityEngine;

/// <summary>
/// Показывает/прячет иконки состояния ("Zzz", "!")
/// над зданием в зависимости от его "мозгов" (Producer/Input).
/// </summary>
public class BuildingStatusVisualizer : MonoBehaviour
{
    [Header("Иконки (Префабы)")]
    [Tooltip("Префаб иконки 'Zzz' (склад полон)")]
    public GameObject ZzzIcon;
    [Tooltip("Префаб иконки '!' (нет сырья)")]
    public GameObject NoResourceIcon;

    // --- Ссылки на "мозги" ---
    private ResourceProducer _producer;
    private BuildingInputInventory _inputInv;

    private void Awake()
    {
        _producer = GetComponent<ResourceProducer>();
        _inputInv = GetComponent<BuildingInputInventory>();

        // Если у здания нет ни того, ни другого - скрипт не нужен
        if (_producer == null && _inputInv == null)
        {
            Destroy(this); // (Или 'this.enabled = false;')
            return;
        }
        
        // Прячем иконки на старте
        if (ZzzIcon) ZzzIcon.SetActive(false);
        if (NoResourceIcon) NoResourceIcon.SetActive(false);
    }

    private void Update()
    {
        // 1. Проверяем "Сон" (Zzz)
        if (ZzzIcon)
        {
            // Показываем "Zzz", если продюсер есть и он "спит"
            bool showZzz = (_producer != null && _producer.IsPaused);
            
            if (ZzzIcon.activeSelf != showZzz)
                ZzzIcon.SetActive(showZzz);
        }

        // 2. Проверяем "Запрос сырья" (!)
        if (NoResourceIcon)
        {
            // Показываем "!", если инвентарь есть и он "просит"
            bool showNoRes = (_inputInv != null && _inputInv.IsRequesting);
            
            // ВАЖНО: "Zzz" (Сон) имеет ПРИОРИТЕТ.
            // Если завод "спит", не показываем "!", даже если сырье кончилось.
            if (_producer != null && _producer.IsPaused)
                showNoRes = false; 
            
            if (NoResourceIcon.activeSelf != showNoRes)
                NoResourceIcon.SetActive(showNoRes);
        }
    }
}