using UnityEngine;
using System.Collections.Generic;

// Это "магия" Unity:
// 1. Гарантирует, что на объекте будут эти компоненты
// 2. Добавит их автоматически, если их нет
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RadiusVisualizer : MonoBehaviour
{
    [Header("Настройки Линии")]
    [SerializeField]
    private Material lineMaterial; // Сюда мы перетащим M_Line_Green

    [SerializeField]
    [Range(10, 100)]
    private int segments = 50; // Качество круга (50 - отлично)
    
    [SerializeField]
    private float yOffset = 0.1f; // Чуть выше земли, чтобы не мерцало

    // Компоненты, которые мы "схватим"
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private AuraEmitter _auraEmitter;
    private Mesh _circleMesh;

    void Awake()
    {
// 1. "Хватаем" все нужные компоненты
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        // Эту строку мы уже чинили (GetComponentInParent)
        _auraEmitter = GetComponentInParent<AuraEmitter>(); 

        // --- ⬇️⬇️⬇️ ВОТ НОВЫЙ ФИКС ⬇️⬇️⬇️ ---
        
        // Слой "Ignore Raycast" в Unity по умолчанию имеет номер 2
        const int IGNORE_RAYCAST_LAYER = 2;

        // Проверяем: если наш родитель (Townhall) находится на слое "призраков"...
        if (transform.parent != null && transform.parent.gameObject.layer == IGNORE_RAYCAST_LAYER)
        {
            // ...то мы - часть "призрака", а не реальное здание.
            // Нам здесь делать нечего.
            _meshRenderer.enabled = false; // Выключаем рендер (на всякий случай)
            this.enabled = false;          // Выключаем *весь этот скрипт*
            return; // Немедленно выходим из Awake()
        }
        
        // 2. Создаем "пустой" меш, который будем наполнять
        _circleMesh = new Mesh();
        _circleMesh.name = "Procedural_Circle_Mesh";
        _meshFilter.mesh = _circleMesh;

        // 3. Настраиваем рендер (он будет использовать наш материал)
        // Важно: назначаем материал, который мы задали
        if (lineMaterial != null)
        {
            // Мы "копируем" материал, чтобы не изменить
            // оригинальный 'M_Line_Green' ассет
            _meshRenderer.material = new Material(lineMaterial);

            // А вот и "пропавший" 6-й пункт:
            // Мы принудительно задаем цвет нашей "копии" материала
            _meshRenderer.material.color = Color.blue;
        }
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 4. ГЛАВНОЕ: Прячем его при старте
        _meshRenderer.enabled = false;
    }

    /// <summary>
    /// Генерирует меш круга и включает рендер
    /// </summary>
    public void Show()
    {
        // Рисуем, только если аура "Радиальная"
        if (_auraEmitter.distributionType != AuraDistributionType.Radial)
        {
            return;
        }

        // Вызываем "мозг" - генератор меша
        GenerateCircleMesh(_auraEmitter.radius);
        _meshRenderer.enabled = true;
    }

    /// <summary>
    /// Прячет рендер
    /// </summary>
    public void Hide()
    {
        _meshRenderer.enabled = false;
    }

    /// <summary>
    /// "Мозг": Рисует круг из линий
    /// </summary>
    private void GenerateCircleMesh(float radius)
    {
        // Очищаем старые данные, если они были
        _circleMesh.Clear();

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        // Генерируем точки (вершины) по кругу
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
            
            // Важно: Vector3(x, y, z)
            // Мы "рисуем" по X и Z. Y - это наша высота 'yOffset'
            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius;
            
            // Добавляем точку.
            // Она будет "относительно" центра нашего здания,
            // т.к. этот скрипт "живет" на самом здании.
            vertices.Add(new Vector3(x, yOffset, z));
        }

        // Соединяем точки линиями
        // Нам нужно 50 линий: (0-1), (1-2), (2-3) ... (49-50)
        for (int i = 0; i < segments; i++)
        {
            indices.Add(i);
            indices.Add(i + 1);
        }

        // Применяем данные в меш
        _circleMesh.SetVertices(vertices);
        _circleMesh.SetIndices(indices, MeshTopology.Lines, 0); // <-- Магия здесь!
    }
}