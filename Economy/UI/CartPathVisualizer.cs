using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Рисует (LineRenderer'ом) путь тележки, когда
/// "домашнее" здание этой тележки выбрано.
/// Вешается на префаб "домашнего" здания (напр. Лесопилка).
/// </summary>
[RequireComponent(typeof(BuildingIdentity))]
public class CartPathVisualizer : MonoBehaviour
{
    // --- Ссылки (должны быть в префабе) ---
    [SerializeField] private LineRenderer lineRenderer; // Ссылка на компонент LineRenderer
    [SerializeField] private CartAgent cartAgent; // Ссылка на "дочернюю" тележку

    // --- Системы ---
    private SelectionManager _selection;
    private BuildingIdentity _myIdentity;
    
    private bool _isSelected = false;

    private void Awake()
    {
        _myIdentity = GetComponent<BuildingIdentity>();
        
        // Авто-поиск, если не задано в инспекторе
        if (lineRenderer == null)
            lineRenderer = GetComponentInChildren<LineRenderer>();
            
        if (cartAgent == null)
            cartAgent = GetComponentInChildren<CartAgent>();
            
        if (lineRenderer == null || cartAgent == null)
        {
            Debug.LogWarning($"[CartPathVisualizer] на {gameObject.name} не нашел LineRenderer или CartAgent. Отключаюсь.");
            this.enabled = false;
            return;
        }
        
        // На старте линия не нужна
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        _selection = SelectionManager.Instance;
        if (_selection != null)
        {
            _selection.SelectionChanged += OnSelectionChanged;
            // Проверяем начальное выделение (на всякий случай)
            OnSelectionChanged(null); 
        }
    }

    private void OnDestroy()
    {
        if (_selection != null)
            _selection.SelectionChanged -= OnSelectionChanged;
    }
    
    /// <summary>
    /// Слушаем "глобальное" событие о смене выделения
    /// </summary>
    private void OnSelectionChanged(IReadOnlyCollection<BuildingIdentity> selection)
    {
        if (selection != null)
        {
            // Проверяем, есть ли МЫ в этом выделении
            _isSelected = selection.Contains(_myIdentity);
        }
        else
        {
            _isSelected = false;
        }

        // Если нас не выбрали - сразу прячем линию
        if (!_isSelected && lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// В Update() мы только рисуем, если "выбраны" И "тележка занята"
    /// </summary>
    private void Update()
    {
        if (!_isSelected) return;

        if (cartAgent.IsBusy())
        {
            // --- Рисуем путь ---
            if (!lineRenderer.enabled)
                lineRenderer.enabled = true;
                
            List<Vector3> path = cartAgent.GetRemainingPathWorld();
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
        }
        else
        {
            // --- Тележка "дома", прячем линию ---
            if (lineRenderer.enabled)
                lineRenderer.enabled = false;
        }
    }
}