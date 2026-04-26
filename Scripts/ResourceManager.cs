using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Header("Player Minerals")]
    public int player1Minerals = 50;
    public int player2Minerals = 50;

    [Header("Reserved Supply From Queued Units")]
    public int player1ReservedSupply;
    public int player2ReservedSupply;

    [Header("Debug")]
    public bool logSupplyChanges = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddMinerals(RTSFaction faction, int amount)
    {
        if (amount <= 0)
            return;

        switch (faction)
        {
            case RTSFaction.Player1:
                player1Minerals += amount;
                Debug.Log("Player 1 Minerals: " + player1Minerals);
                break;

            case RTSFaction.Player2:
                player2Minerals += amount;
                Debug.Log("Player 2 Minerals: " + player2Minerals);
                break;
        }
    }

    public bool SpendMinerals(RTSFaction faction, int amount)
    {
        if (amount <= 0)
            return true;

        switch (faction)
        {
            case RTSFaction.Player1:
                if (player1Minerals < amount)
                    return false;

                player1Minerals -= amount;
                Debug.Log("Player 1 Minerals: " + player1Minerals);
                return true;

            case RTSFaction.Player2:
                if (player2Minerals < amount)
                    return false;

                player2Minerals -= amount;
                Debug.Log("Player 2 Minerals: " + player2Minerals);
                return true;
        }

        return false;
    }

    public int GetMinerals(RTSFaction faction)
    {
        switch (faction)
        {
            case RTSFaction.Player1:
                return player1Minerals;

            case RTSFaction.Player2:
                return player2Minerals;

            default:
                return 0;
        }
    }

    public int GetUsedSupply(RTSFaction faction)
    {
        int used = 0;

        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        foreach (RTSUnit unit in units)
        {
            if (unit == null)
                continue;

            if (unit.IsDead)
                continue;

            if (!unit.isTargetable)
                continue;

            if (unit.faction != faction)
                continue;

            used += Mathf.Max(0, unit.supplyCost);
        }

        return used;
    }

    public int GetMaxSupply(RTSFaction faction)
    {
        int max = 0;

        IReadOnlyList<SupplyProvider> providers = SupplyProvider.AllSupplyProviders;

        foreach (SupplyProvider provider in providers)
        {
            if (provider == null)
                continue;

            if (provider.faction != faction)
                continue;

            if (!provider.isOperational)
                continue;

            max += Mathf.Max(0, provider.supplyProvided);
        }

        return max;
    }

    public int GetReservedSupply(RTSFaction faction)
    {
        switch (faction)
        {
            case RTSFaction.Player1:
                return player1ReservedSupply;

            case RTSFaction.Player2:
                return player2ReservedSupply;

            default:
                return 0;
        }
    }

    public int GetTotalCommittedSupply(RTSFaction faction)
    {
        return GetUsedSupply(faction) + GetReservedSupply(faction);
    }

    public bool CanReserveSupply(RTSFaction faction, int supplyAmount)
    {
        if (supplyAmount <= 0)
            return true;

        int committed = GetTotalCommittedSupply(faction);
        int max = GetMaxSupply(faction);

        return committed + supplyAmount <= max;
    }

    public bool ReserveSupply(RTSFaction faction, int supplyAmount)
    {
        if (supplyAmount <= 0)
            return true;

        if (!CanReserveSupply(faction, supplyAmount))
        {
            Debug.LogWarning(
                "Not enough supply. " +
                "Committed: " + GetTotalCommittedSupply(faction) +
                " / Max: " + GetMaxSupply(faction) +
                " | Needed: " + supplyAmount
            );

            return false;
        }

        switch (faction)
        {
            case RTSFaction.Player1:
                player1ReservedSupply += supplyAmount;
                break;

            case RTSFaction.Player2:
                player2ReservedSupply += supplyAmount;
                break;
        }

        if (logSupplyChanges)
        {
            Debug.Log("Supply reserved. " + GetSupplyText(faction));
        }

        return true;
    }

    public void ReleaseReservedSupply(RTSFaction faction, int supplyAmount)
    {
        if (supplyAmount <= 0)
            return;

        switch (faction)
        {
            case RTSFaction.Player1:
                player1ReservedSupply = Mathf.Max(0, player1ReservedSupply - supplyAmount);
                break;

            case RTSFaction.Player2:
                player2ReservedSupply = Mathf.Max(0, player2ReservedSupply - supplyAmount);
                break;
        }

        if (logSupplyChanges)
        {
            Debug.Log("Reserved supply released. " + GetSupplyText(faction));
        }
    }

    public string GetSupplyText(RTSFaction faction)
    {
        int committed = GetTotalCommittedSupply(faction);
        int max = GetMaxSupply(faction);

        return "Supply: " + committed + " / " + max;
    }
}