// RoadTintDebug.cs
using System.Collections.Generic;
using UnityEngine;

public class RoadTintDebug : MonoBehaviour
{
    private readonly Dictionary<Renderer, MaterialPropertyBlock> mpb = new();
    private bool toggled;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.H)) return;

        var roads = FindObjectsByType<RoadTile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var tile in roads)
        {
            var r = tile.GetComponent<Renderer>() ?? tile.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            if (!mpb.TryGetValue(r, out var block))
            {
                block = new MaterialPropertyBlock();
                mpb[r] = block;
            }
            else block.Clear();

            if (!toggled)
            {
                var c = new Color(0.35f, 0.75f, 1f, 1f);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color",     c);
                block.SetColor("_EmissionColor", c * 0.5f);
            }
            else
            {
                // сброс
                r.SetPropertyBlock(null);
                continue;
            }

            r.SetPropertyBlock(block);
        }

        toggled = !toggled;
    }
}
