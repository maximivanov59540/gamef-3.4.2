using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SelectionAuraBridge : MonoBehaviour
{
    [SerializeField] private SelectionManager selection;
    [SerializeField] private AuraManager aura;

    private void Awake()
    {
        if (selection == null) selection = FindFirstObjectByType<SelectionManager>();
        if (aura == null) aura = FindFirstObjectByType<AuraManager>();
        if (selection != null) selection.SelectionChanged += OnSelectionChanged;
    }

    private void OnDestroy()
    {
        if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged(IReadOnlyCollection<BuildingIdentity> sel)
    {
        if (aura == null) return;

        if (sel != null && sel.Count == 1)
        {
            var b = sel.FirstOrDefault();
            if (b != null)
            {
                var e = b.GetComponent<AuraEmitter>();
                if (e != null && e.distributionType == AuraDistributionType.RoadBased)
                {
                    // если добавляли RefreshRootCell() в AuraEmitter — можно вызвать
                    // e.RefreshRootCell();
                    aura.ShowRoadAura(e);
                    return;
                }
            }
        }

        // иначе — прячем
        aura.HideRoadAuraOverlay();
    }
}
