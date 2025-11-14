using UnityEngine;
using System.Collections.Generic;
[RequireComponent(typeof(BuildingIdentity))]
public class Warehouse : MonoBehaviour
{
    [Tooltip("На сколько этот склад увеличивает глобальный лимит хранения")]
    public float limitIncrease = 100f;
    [Header("Логистика")]
    [Tooltip("Радиус 'покрытия' этого склада по дорогам")]
    public float roadRadius = 20f;
    
    [Tooltip("Макс. кол-во тележек, разгружаемых ОДНОВРЕМЕННО (Уровень склада)")]
    public int maxCartQueue = 1;
    
    [Tooltip("Время (сек) на полную разгрузку ОДНОЙ тележки")]
    public float unloadTime = 15.0f;

    // Список тех, кто СЕЙЧАС разгружается
    private List<CartAgent> _cartQueue = new List<CartAgent>();

    void Start()
    {
        // При постройке - увеличиваем лимит
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.IncreaseGlobalLimit(limitIncrease);
        }
    }

    void OnDestroy()
    {
        // При сносе - уменьшаем лимит
        // (Проверяем Instance на случай, если выходим из игры)
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.IncreaseGlobalLimit(-limitIncrease);
        }
    }
    public bool RequestUnload(CartAgent cart)
    {
        if (_cartQueue.Count < maxCartQueue)
        {
            _cartQueue.Add(cart);
            Debug.Log($"[Warehouse] {cart.name} начал разгрузку. В очереди: {_cartQueue.Count}/{maxCartQueue}");
            return true; // "Добро пожаловать, проезжай"
        }
        return false; // "Мест нет, стой в очереди"
    }

    public void FinishUnload(CartAgent cart)
    {
        _cartQueue.Remove(cart);
        Debug.Log($"[Warehouse] {cart.name} закончил разгрузку. В очереди: {_cartQueue.Count}/{maxCartQueue}");
    }
}