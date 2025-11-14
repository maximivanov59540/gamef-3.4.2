using UnityEngine;
using System.Collections.Generic;

// "Гарантируем", что на этом объекте есть MeshFilter и MeshRenderer
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridVisualizer : MonoBehaviour
{
    public Material lineMaterial; // Ты "задаешь" "этот" "материал" в "инспекторе"
    private GridSystem gridSystem;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Awake()
    {
        gridSystem = FindFirstObjectByType<GridSystem>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (gridSystem == null)
        {
            this.enabled = false;  // Если нет сетки, отключаем компонент
            return;
        }

        // --- 1. Настраиваем материал (как в твоем старом коде) ---
        if (lineMaterial == null)
        {
            // "Запасной" "материал", "если" "мы" "забыли" "его" "назначить"
            lineMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            lineMaterial.color = Color.white;
        }
        meshRenderer.material = lineMaterial; // "Применяем" "материал" "к" "MeshRenderer"
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // --- 2. Генерируем "Меш" ---
        GenerateGridMesh();
    }

    private void GenerateGridMesh()
    {
        int width = gridSystem.GetGridWidth();
        int height = gridSystem.GetGridHeight();
        
        // "Меш" "состоит" "из" "вершин" (точки) "и" "индексов" (линии)
        Mesh gridMesh = new Mesh();
        gridMesh.name = "GridMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        int vertexIndex = 0;

        // --- Рисуем линии по оси X (вертикальные) ---
        for (int x = 0; x <= width; x++)
        {
            // "Начальная" "точка" "линии" (низ)
            vertices.Add(gridSystem.GetWorldPosition(x, 0)); 
            // "Конечная" "точка" "линии" (верх)
            vertices.Add(gridSystem.GetWorldPosition(x, height)); 

            // "Говорим" "мешу": "Нарисуй" "линию" "между" "двумя" "точками", "что" "мы" "только" "что" "добавили"
            indices.Add(vertexIndex);     // "Индекс" "первой" "точки"
            indices.Add(vertexIndex + 1); // "Индекс" "второй" "точки"
            vertexIndex += 2; // "Мы" "добавили" 2 "вершины"
        }

        // --- Рисуем линии по оси Z (горизонтальные) ---
        for (int z = 0; z <= height; z++)
        {
            // "Начальная" "точка" "линии" (лево)
            vertices.Add(gridSystem.GetWorldPosition(0, z));
            // "Конечная" "точка" "линии" (право)
            vertices.Add(gridSystem.GetWorldPosition(width, z));

            // "Снова" "говорим": "Нарисуй" "линию" "между" "двумя" "новыми" "точками"
            indices.Add(vertexIndex);
            indices.Add(vertexIndex + 1);
            vertexIndex += 2;
        }

        // --- 3. "Собираем" "Меш" ---
        gridMesh.SetVertices(vertices);
        // "Мы" "говорим" "мешу", "что" "наши" "индексы" - "это" "ЛИНИИ" (а не треугольники)
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0); 

        // --- 4. "Отдаем" "Меш" "нашему" "MeshFilter"---
        meshFilter.mesh = gridMesh;
    }
}