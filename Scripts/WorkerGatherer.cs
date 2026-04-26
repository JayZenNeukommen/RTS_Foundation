using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(RTSUnit))]
[RequireComponent(typeof(NavMeshAgent))]
public class WorkerGatherer : MonoBehaviour
{
    private enum GatherState
    {
        Idle,
        MovingToMineral,
        Harvesting,
        ReturningToDepot
    }

    [Header("Gathering")]
    public int carryCapacity = 5;
    public int carriedMinerals = 0;
    public float interactDistance = 1.8f;
    public float harvestDuration = 1.5f;

    [Header("Carry Visual")]
    public bool autoCreateCarryVisual = true;
    public GameObject carriedResourceVisual;
    public Vector3 carryVisualLocalPosition = new Vector3(0f, 1.2f, 0.45f);
    public Vector3 carryVisualScale = new Vector3(0.35f, 0.35f, 0.35f);

    public bool HasCarriedResources => carriedMinerals > 0;
    public bool IsBusyGathering => state != GatherState.Idle;

    private RTSUnit unit;
    private NavMeshAgent agent;

    private MineralNode targetMineral;
    private ResourceDepot targetDepot;

    private GatherState state = GatherState.Idle;
    private float harvestTimer;
    private Vector3 currentDepotDepositPoint;
    private bool findNearestMineralAfterDeposit;
    private bool useSpecificDepotForNextReturn;

    private void Awake()
    {
        unit = GetComponent<RTSUnit>();
        agent = GetComponent<NavMeshAgent>();

        SetupCarryVisual();
        UpdateCarryVisual();
    }

    private void Update()
    {
        switch (state)
        {
            case GatherState.MovingToMineral:
                HandleMovingToMineral();
                break;

            case GatherState.Harvesting:
                HandleHarvesting();
                break;

            case GatherState.ReturningToDepot:
                HandleReturningToDepot();
                break;
        }
    }

    public void StartGathering(MineralNode mineral)
    {
        if (!unit.canGather)
        {
            Debug.LogWarning(name + " cannot gather.");
            return;
        }

        if (mineral == null || mineral.IsEmpty)
        {
            Debug.LogWarning("No valid mineral node selected.");
            return;
        }

        ResourceDepot depot = FindNearestDepot();

        if (depot == null)
        {
            Debug.LogWarning(name + " could not find a resource depot.");
            return;
        }

        targetMineral = mineral;
        targetDepot = depot;

        if (carriedMinerals > 0)
        {
            MoveToDepot();
        }
        else
        {
            MoveToMineral();
        }

        Debug.Log(name + " started gathering from " + mineral.name);
    }

    public void CancelGathering()
    {
        state = GatherState.Idle;
        targetMineral = null;
        targetDepot = null;
        harvestTimer = 0f;
        findNearestMineralAfterDeposit = false;
        useSpecificDepotForNextReturn = false;
    }

    private void MoveToMineral()
    {
        if (targetMineral == null || targetMineral.IsEmpty)
        {
            CancelGathering();
            return;
        }

        state = GatherState.MovingToMineral;

        agent.isStopped = false;
        agent.SetDestination(targetMineral.transform.position);
    }

    private void MoveToDepot()
    {
        ResourceDepot depotToUse = null;

        if (useSpecificDepotForNextReturn &&
            targetDepot != null &&
            targetDepot.faction == unit.faction &&
            targetDepot.isOperational)
        {
            depotToUse = targetDepot;
        }
        else
        {
            depotToUse = FindNearestDepot();
        }

        if (depotToUse == null)
        {
            CancelGathering();
            return;
        }

        targetDepot = depotToUse;
        state = GatherState.ReturningToDepot;

        currentDepotDepositPoint = targetDepot.GetNearestDepositPoint(transform.position);

        agent.isStopped = false;
        agent.SetDestination(currentDepotDepositPoint);
    }

    private void HandleMovingToMineral()
    {
        if (targetMineral == null || targetMineral.IsEmpty)
        {
            targetMineral = FindNearestMineral();

            if (targetMineral != null)
            {
                MoveToMineral();
                return;
            }

            CancelGathering();
            return;
        }

        float distance = GetHorizontalDistance(transform.position, targetMineral.transform.position);

        if (distance <= interactDistance)
        {
            agent.ResetPath();
            agent.isStopped = true;

            harvestTimer = harvestDuration;
            state = GatherState.Harvesting;

            Debug.Log(name + " is harvesting.");
        }
    }

    private void HandleHarvesting()
    {
        if (targetMineral == null || targetMineral.IsEmpty)
        {
            targetMineral = FindNearestMineral();

            if (targetMineral != null)
            {
                MoveToMineral();
                return;
            }

            CancelGathering();
            return;
        }

        harvestTimer -= Time.deltaTime;

        if (harvestTimer > 0f)
            return;

        carriedMinerals = targetMineral.Harvest(carryCapacity);
        UpdateCarryVisual();

        if (carriedMinerals <= 0)
        {
            CancelGathering();
            return;
        }

        Debug.Log(name + " carrying " + carriedMinerals + " minerals.");

        MoveToDepot();
    }

    private void HandleReturningToDepot()
    {
        if (targetDepot == null)
        {
            targetDepot = FindNearestDepot();

            if (targetDepot == null)
            {
                CancelGathering();
                return;
            }
        }

        float distance = GetHorizontalDistance(transform.position, currentDepotDepositPoint);

        if (distance <= interactDistance)
        {
            DepositMinerals();

            if (targetMineral != null && !targetMineral.IsEmpty)
            {
                MoveToMineral();
                return;
            }

            targetMineral = FindNearestMineral();

            if (targetMineral != null)
            {
                findNearestMineralAfterDeposit = false;
                MoveToMineral();
                return;
            }

            CancelGathering();
        }
    }

    private void DepositMinerals()
    {
        if (carriedMinerals <= 0)
            return;

        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("No ResourceManager found in scene.");
            return;
        }

        ResourceManager.Instance.AddMinerals(unit.faction, carriedMinerals);

        Debug.Log(name + " deposited " + carriedMinerals + " minerals.");

        carriedMinerals = 0;
        UpdateCarryVisual();
        useSpecificDepotForNextReturn = false;
    }

    private MineralNode FindNearestMineral()
    {
        IReadOnlyList<MineralNode> minerals = MineralNode.AllMineralNodes;

        MineralNode nearestMineral = null;
        float nearestDistance = Mathf.Infinity;

        foreach (MineralNode mineral in minerals)
        {
            if (mineral == null || mineral.IsEmpty)
                continue;

            if (!UnityEngine.AI.NavMesh.SamplePosition(
                    mineral.transform.position,
                    out UnityEngine.AI.NavMeshHit navHit,
                    3f,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                continue;
            }

            float distance = GetHorizontalDistance(transform.position, navHit.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestMineral = mineral;
            }
        }

        return nearestMineral;
    }

    private void SetupCarryVisual()
    {
        if (carriedResourceVisual == null && autoCreateCarryVisual)
        {
            carriedResourceVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carriedResourceVisual.name = "CarriedMineralsVisual";
            carriedResourceVisual.transform.SetParent(transform);
            carriedResourceVisual.transform.localPosition = carryVisualLocalPosition;
            carriedResourceVisual.transform.localRotation = Quaternion.identity;
            carriedResourceVisual.transform.localScale = carryVisualScale;

            Collider visualCollider = carriedResourceVisual.GetComponent<Collider>();

            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            Renderer renderer = carriedResourceVisual.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.material.color = Color.cyan;
            }
        }
    }

    private void UpdateCarryVisual()
    {
        if (carriedResourceVisual != null)
        {
            carriedResourceVisual.SetActive(carriedMinerals > 0);
        }
    }

    private ResourceDepot FindNearestDepot()
    {
        IReadOnlyList<ResourceDepot> depots = ResourceDepot.AllDepots;

        ResourceDepot nearestDepot = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ResourceDepot depot in depots)
        {
            if (depot.faction != unit.faction)
                continue;

            if (!depot.isOperational)
                continue;

            float distance = GetHorizontalDistance(transform.position, depot.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestDepot = depot;
            }
        }

        return nearestDepot;
    }

    public void ReturnCarriedResources(ResourceDepot depot, bool findMineralAfterDeposit)
    {
        if (!unit.canGather)
        {
            Debug.LogWarning(name + " cannot return resources because it cannot gather.");
            return;
        }

        if (depot == null)
        {
            Debug.LogWarning(name + " has no depot to return to.");
            return;
        }

        if (depot.faction != unit.faction)
        {
            Debug.LogWarning(name + " cannot return resources to enemy depot.");
            return;
        }

        if (!depot.isOperational)
        {
            Debug.LogWarning(name + " cannot return resources to unfinished depot.");
            return;
        }

        targetDepot = depot;
        findNearestMineralAfterDeposit = findMineralAfterDeposit;
        useSpecificDepotForNextReturn = true;

        MoveToDepot();

        Debug.Log(name + " ordered to return resources.");
    }

    private float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}