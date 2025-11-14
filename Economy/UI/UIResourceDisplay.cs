using TMPro;
using UnityEngine;

public class UIResourceDisplay : MonoBehaviour
{
    // Твои старые ссылки
    public ResourceManager resourceManager;
    public PopulationManager populationManager;
    
    // +++ НАША НОВАЯ ССЫЛКА +++
    public MoneyManager moneyManager; // Ссылка на казну
    
    // Твои старые текстовые поля
    public TextMeshProUGUI woodText;
    public TextMeshProUGUI stoneText;
    public TextMeshProUGUI planksText;
    public TextMeshProUGUI populationText;
    
    // +++ НАШЕ НОВОЕ ТЕКСТОВОЕ ПОЛЕ +++
    public TextMeshProUGUI moneyText; // Поле для текста "Деньги"


    void Update()
    {
        // Твой старый код для ресурсов
        if (resourceManager != null)
        {
            woodText.text = "Дерево: " + Mathf.FloorToInt(resourceManager.GetResourceAmount(ResourceType.Wood));
            stoneText.text = "Камень: " + Mathf.FloorToInt(resourceManager.GetResourceAmount(ResourceType.Stone));
            planksText.text = "Доски: " + Mathf.FloorToInt(resourceManager.GetResourceAmount(ResourceType.Planks));
        }

        // Твой старый код для населения
        if (populationManager != null)
        {
            populationText.text = "Население: " + populationManager.currentPopulation + " / " + populationManager.maxPopulation;
        }
        
        // +++ НАШ НОВЫЙ КОД ДЛЯ ДЕНЕГ +++
        if (moneyManager != null)
        {
            // Берем деньги из MoneyManager, округляем и показываем
            moneyText.text = "Деньги: " + Mathf.FloorToInt(moneyManager.GetCurrentMoney());
        }
    }
}