using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AuraOnClick : MonoBehaviour
{
    private AuraEmitter emitter;
    private BuildingIdentity identity;

    private void Awake()
    {
        emitter  = GetComponent<AuraEmitter>();
        identity = GetComponent<BuildingIdentity>();
    }

    private void OnMouseDown()
    {
        // ВАЖНО: без проверок UI — кое-где у тебя EventSystem ложно «перекрывает» мир.
        if (emitter == null || emitter.distributionType != AuraDistributionType.RoadBased) return;

        Debug.Log("[AuraOnClick] Click → ShowRoadAura");
        AuraManager.Instance?.ShowRoadAura(emitter);

        // Чтобы выбор в игре оставался согласованным:
        SelectionManager.Instance?.SelectSingle(identity);
    }

    private void OnDisable()
    {
        AuraManager.Instance?.HideRoadAuraOverlay();
    }
}
