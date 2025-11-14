using System.Collections.Generic;
using UnityEngine;

/// Состояния визуала здания
public enum VisualState
{
    Real,
    Ghost,
    Blueprint
}

/// Компонент вешается на ПРЕФАБ здания и сам управляет материалами
public class BuildingVisuals : MonoBehaviour
{
    [Header("Материалы")]
    [Tooltip("Зелёный призрак (валидное место)")]
    [SerializeField] private Material _ghostValidMaterial;
    [Tooltip("Красный призрак (некуда ставить)")]
    [SerializeField] private Material _ghostInvalidMaterial;
    [Tooltip("Синий проект (валидное место)")]
    [SerializeField] private Material _blueprintValidMaterial;
    [Tooltip("Красный проект (некуда ставить)")]
    [SerializeField] private Material _blueprintInvalidMaterial;

    // "Кэш" "оригинальных" "материалов" (ассетов)
    private readonly Dictionary<Renderer, Material[]> _realMaterials = new();
    private Renderer[] _renderers;
    
    // "Кэш" "массивов" "для" "призраков", "чтобы" "не" "создавать" "мусор" (GC)
    private readonly Dictionary<int, Material[]> _materialPool = new();

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in _renderers)
        {
            // --- ИСПРАВЛЕНИЕ #15 ---
            // "Кэшируем" "sharedMaterials" (ассеты), "а" "не" "materials" (копии)
            _realMaterials[r] = r.sharedMaterials;
            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
        }
    }

    /// Главный метод смены состояния
    public void SetState(VisualState state, bool isValid)
    {
        switch (state)
        {
            case VisualState.Real:
                foreach (var r in _renderers)
                {
                    if (_realMaterials.TryGetValue(r, out var mats))
                    {
                        // --- ИСПРАВЛЕНИЕ #15 ---
                        // "Восстанавливаем" "оригинальные" "ассеты"
                        r.sharedMaterials = mats;
                        // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
                    }
                }
                break;

            case VisualState.Ghost:
                ApplyMaterial(isValid ? _ghostValidMaterial : _ghostInvalidMaterial);
                break;

            case VisualState.Blueprint:
                ApplyMaterial(isValid ? _blueprintValidMaterial : _blueprintInvalidMaterial);
                break;
        }
    }

    private void ApplyMaterial(Material mat)
    {
        if (mat == null) return;

        foreach (var r in _renderers)
        {
            // --- ИСПРАВЛЕНИЕ #15 ---
            // "Берем" "длину" "массива" "из" "нашего" "кэша", "а" "не" "через" ".materials"
            int len = _realMaterials[r].Length;
            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

            // "Используем" "пул" "массивов", "чтобы" "не" "создавать" 'new Material[len]'
            if (!_materialPool.TryGetValue(len, out var dst))
            {
                dst = new Material[len];
                _materialPool[len] = dst;
            }
            
            for (int i = 0; i < dst.Length; i++) dst[i] = mat;
            
            // "Вот" "здесь" ".materials" (копии) "используется" "правильно" -
            // "мы" "создаем" "копии" "для" "этого" "конкретного" "здания"
            r.materials = dst;
        }
    }
}