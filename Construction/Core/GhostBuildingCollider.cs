using UnityEngine;

public class GhostBuildingCollider : MonoBehaviour
{
    private int collisionCount = 0;

    public bool IsColliding()
    {
        return collisionCount > 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Building"))
        {
            collisionCount++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Building"))
        {
            collisionCount--;
        }
    }
}