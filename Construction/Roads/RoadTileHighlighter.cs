// RoadTileHighlighter.cs
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RoadTileHighlighter : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial; // задай в инспекторе (URP Unlit Color или простой Lit)
    private Material _originalMat;
    private Renderer _r;

    void Awake()
    {
        _r = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (_r != null) _originalMat = _r.sharedMaterial;
    }

    public void SetHighlight(bool on, Color? color = null)
    {
        if (_r == null) return;

        if (on)
        {
            if (highlightMaterial != null)
            {
                // создаём экземпляр, чтобы можно было красить индивидуально
                var inst = new Material(highlightMaterial);
                if (color.HasValue)
                {
                    inst.SetColor("_BaseColor", color.Value);
                    inst.SetColor("_Color",     color.Value);
                }
                _r.material = inst; // материал-инстанс только на время подсветки
            }
        }
        else
        {
            _r.sharedMaterial = _originalMat;
        }
    }
}
