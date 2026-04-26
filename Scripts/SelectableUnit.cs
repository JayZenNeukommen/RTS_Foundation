using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class SelectableUnit : MonoBehaviour
{
    private static readonly List<SelectableUnit> allSelectableUnits = new List<SelectableUnit>();
    public static IReadOnlyList<SelectableUnit> AllSelectableUnits => allSelectableUnits;

    [Header("Selection")]
    public Color selectedColor = Color.yellow;

    private Renderer[] renderers;
    private Color[] originalColors;

    private RTSCombatController combatController;
    private WorkerGatherer workerGatherer;
    private WorkerBuilder workerBuilder;

    public WorkerGatherer Gatherer => workerGatherer;

    public bool IsSelected { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public RTSUnit Unit { get; private set; }

    private void Awake()
    {
        Unit = GetComponent<RTSUnit>();
        Agent = GetComponent<NavMeshAgent>();
        combatController = GetComponent<RTSCombatController>();
        workerGatherer = GetComponent<WorkerGatherer>();
        workerBuilder = GetComponent<WorkerBuilder>();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    private void OnEnable()
    {
        if (!allSelectableUnits.Contains(this))
        {
            allSelectableUnits.Add(this);
        }
    }

    private void OnDisable()
    {
        allSelectableUnits.Remove(this);
    }

    public void BuildOrRepair(SelectableBuilding building)
    {
        if (building == null)
            return;

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (workerBuilder == null)
        {
            MoveTo(building.GetNearestInteractionPoint(transform.position));
            return;
        }

        if (building.Construction != null && !building.Construction.IsComplete)
        {
            workerBuilder.StartBuilding(building.Construction);
            return;
        }

        if (building.Unit != null && building.Unit.IsDamaged)
        {
            workerBuilder.StartRepair(building.Unit);
            return;
        }

        MoveTo(building.GetNearestInteractionPoint(transform.position));
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = selected ? selectedColor : originalColors[i];
        }
    }

    public void MoveTo(Vector3 destination)
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController != null)
        {
            combatController.MoveTo(destination);
            return;
        }

        if (Agent == null)
            return;

        Agent.isStopped = false;
        Agent.SetDestination(destination);
    }

    public void AttackTarget(RTSUnit target)
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController == null)
        {
            Debug.LogWarning(name + " has no RTSCombatController.");
            return;
        }

        Debug.Log(name + " received attack command against " + target.unitName);
        combatController.AttackTarget(target);
    }

    public void AttackMoveTo(Vector3 destination)
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController != null)
        {
            combatController.AttackMoveTo(destination);
            return;
        }

        MoveTo(destination);
    }

    public void AttackGroundAt(Vector3 groundPoint)
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController == null)
        {
            Debug.LogWarning(name + " has no RTSCombatController.");
            return;
        }

        combatController.AttackGroundAt(groundPoint);
    }

    public void PatrolTo(Vector3 destination)
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController != null)
        {
            combatController.PatrolTo(destination);
            return;
        }

        MoveTo(destination);
    }

    public void StartBuilding(BuildingConstructionController construction)
    {
        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (workerBuilder == null)
        {
            Debug.LogWarning(name + " has no WorkerBuilder component.");
            return;
        }

        workerBuilder.StartBuilding(construction);
    }

    private void CancelBuilderCommand()
    {
        if (workerBuilder != null)
        {
            workerBuilder.CancelBuilding();
        }
    }

    public void Stop()
    {
        CancelBuilderCommand();

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (combatController != null)
        {
            combatController.StopCommand();
            return;
        }

        if (Agent == null)
            return;

        Agent.ResetPath();
        Agent.isStopped = true;
    }

    public void HoldPosition()
    {
        CancelBuilderCommand();

        if (combatController == null)
            return;

        combatController.HoldPosition();
    }

    public void Gather(MineralNode mineral)
    {
        CancelBuilderCommand();

        if (workerGatherer == null)
        {
            Debug.LogWarning(name + " has no WorkerGatherer component.");
            return;
        }

        workerGatherer.StartGathering(mineral);
    }

    public void ReturnResources(ResourceDepot depot)
    {
        CancelBuilderCommand();

        if (depot == null)
            return;

        if (!depot.enabled || !depot.isOperational)
        {
            Debug.LogWarning("Cannot return resources to non-operational depot.");
            return;
        }

        if (workerGatherer == null)
        {
            MoveTo(depot.GetNearestDepositPoint(transform.position));
            return;
        }

        if (workerGatherer.HasCarriedResources)
        {
            workerGatherer.ReturnCarriedResources(depot, true);
        }
        else
        {
            MoveTo(depot.GetNearestDepositPoint(transform.position));
        }
    }

    public void RepairBuilding(SelectableBuilding building)
    {
        if (building == null)
            return;

        if (workerGatherer != null)
        {
            workerGatherer.CancelGathering();
        }

        if (workerBuilder == null)
        {
            MoveTo(building.GetNearestInteractionPoint(transform.position));
            return;
        }

        if (building.Unit != null && building.Unit.IsDamaged)
        {
            workerBuilder.StartRepair(building.Unit);
            return;
        }

        Debug.Log(building.name + " does not need repairs.");
    }

}