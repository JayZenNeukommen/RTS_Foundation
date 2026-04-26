using System.Collections.Generic;
using UnityEngine;

public class SupplyProvider : MonoBehaviour
{
    private static readonly List<SupplyProvider> allSupplyProviders = new List<SupplyProvider>();
    public static IReadOnlyList<SupplyProvider> AllSupplyProviders => allSupplyProviders;

    public RTSFaction faction = RTSFaction.Player1;
    public int supplyProvided = 8;
    public bool isOperational = true;

    private void OnEnable()
    {
        if (!allSupplyProviders.Contains(this))
        {
            allSupplyProviders.Add(this);
        }
    }

    private void OnDisable()
    {
        allSupplyProviders.Remove(this);
    }
}