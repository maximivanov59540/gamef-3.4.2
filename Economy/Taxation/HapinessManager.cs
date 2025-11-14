using UnityEngine;
public class HappinessManager : MonoBehaviour
{
    // Синглтон для легкого доступа
    public static HappinessManager Instance { get; private set; }

    [Header("Настройки Счастья")]
    [Tooltip("Начальное значение счастья")]
    [SerializeField] private float initialHappiness = 50f;
    
    [Tooltip("Скорость, с которой текущее счастье 'догоняет' целевое")]
    [SerializeField] private float happinessLerpSpeed = 1.0f;

    private float _currentHappiness; 
    private float _targetHappiness;

    private void Awake()
    {
        // Настройка синглтона
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        _currentHappiness = initialHappiness;
        _targetHappiness = initialHappiness;
    }

    private void Update()
    {
        _currentHappiness = Mathf.Lerp(
            _currentHappiness, 
            _targetHappiness, 
            happinessLerpSpeed * Time.deltaTime
        );
    }

    public void AddHappiness(float amount)
    {
        // Меняем "цель"
        _targetHappiness += amount;
        
        // Ограничиваем цель в рамках 0-100
        _targetHappiness = Mathf.Clamp(_targetHappiness, 0f, 100f);
    }

    public float GetCurrentHappiness()
    {
        return _currentHappiness;
    }

    public float GetTargetHappiness()
    {
        return _targetHappiness;
    }
}