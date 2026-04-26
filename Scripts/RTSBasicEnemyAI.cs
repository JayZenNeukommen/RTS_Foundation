using System.Collections.Generic;
using UnityEngine;

public class RTSBasicEnemyAI : MonoBehaviour
{
    [Header("Faction")]
    public RTSFaction aiFaction = RTSFaction.Player2;
    public RTSFaction enemyFaction = RTSFaction.Player1;

    [Header("Economy")]
    public int desiredWorkerCount = 8;
    public float workerAssignInterval = 2f;

    [Header("Production")]
    public float productionInterval = 2f;
    public int maxQueuePerProducer = 2;
    public bool trainCombatUnits = true;

    [Header("Attack Waves")]
    public bool enableAttackWaves = true;
    public float attackWaveInterval = 35f;
    public int minimumWaveSize = 5;
    public bool sendAllCombatUnits = true;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private float workerAssignTimer;
    private float productionTimer;
    private float attackWaveTimer;

    private int combatOptionIndex;

    private void Start()
    {
        workerAssignTimer = 0f;
        productionTimer = 0f;
        attackWaveTimer = attackWaveInterval;
    }

    private void Update()
    {
        workerAssignTimer -= Time.deltaTime;
        productionTimer -= Time.deltaTime;
        attackWaveTimer -= Time.deltaTime;

        if (workerAssignTimer <= 0f)
        {
            workerAssignTimer = workerAssignInterval;
            AssignIdleWorkersToMinerals();
        }

        if (productionTimer <= 0f)
        {
            productionTimer = productionInterval;
            ManageProduction();
        }

        if (enableAttackWaves && attackWaveTimer <= 0f)
        {
            TryLaunchAttackWave();
            attackWaveTimer = attackWaveInterval;
        }
    }

    private void AssignIdleWorkersToMinerals()
    {
        IReadOnlyList<SelectableUnit> selectableUnits = SelectableUnit.AllSelectableUnits;

        foreach (SelectableUnit selectable in selectableUnits)
        {
            if (selectable == null || selectable.Unit == null)
                continue;

            RTSUnit unit = selectable.Unit;

            if (unit.faction != aiFaction)
                continue;

            if (!unit.canGather)
                continue;

            WorkerGatherer gatherer = selectable.GetComponent<WorkerGatherer>();
            WorkerBuilder builder = selectable.GetComponent<WorkerBuilder>();
            RTSCombatController combat = selectable.GetComponent<RTSCombatController>();

            if (gatherer == null)
                continue;

            if (gatherer.IsBusyGathering)
                continue;

            if (builder != null && builder.IsBusyBuildingOrRepairing)
                continue;

            if (combat != null && combat.CurrentState != RTSCommandState.Idle)
                continue;

            MineralNode mineral = FindNearestAvailableMineral(selectable.transform.position);

            if (mineral != null)
            {
                selectable.Gather(mineral);

                if (showDebugLogs)
                {
                    Debug.Log("AI assigned worker to mineral: " + mineral.name);
                }
            }
        }
    }

    private void ManageProduction()
    {
        IReadOnlyList<UnitProducer> producers = UnitProducer.AllProducers;

        int workerCount = CountWorkers();
        int combatCount = CountCombatUnits();

        foreach (UnitProducer producer in producers)
        {
            if (producer == null)
                continue;

            if (!producer.isActiveAndEnabled)
                continue;

            if (producer.faction != aiFaction)
                continue;

            BuildingConstructionController construction = producer.GetComponent<BuildingConstructionController>();

            if (construction != null && !construction.IsComplete)
                continue;

            if (producer.TotalQueueCount >= maxQueuePerProducer)
                continue;

            ProductionOption workerOption = FindWorkerOption(producer);
            List<ProductionOption> combatOptions = FindCombatOptions(producer);

            if (workerCount < desiredWorkerCount && workerOption != null)
            {
                if (CanAffordAndSupply(workerOption))
                {
                    producer.TryQueueProduction(workerOption.hotkey);
                    workerCount++;

                    if (showDebugLogs)
                    {
                        Debug.Log("AI queued worker at " + producer.name);
                    }
                }

                continue;
            }

            if (!trainCombatUnits)
                continue;

            if (combatOptions.Count > 0)
            {
                ProductionOption option = combatOptions[combatOptionIndex % combatOptions.Count];
                combatOptionIndex++;

                if (CanAffordAndSupply(option))
                {
                    producer.TryQueueProduction(option.hotkey);
                    combatCount++;

                    if (showDebugLogs)
                    {
                        Debug.Log("AI queued combat unit at " + producer.name);
                    }
                }
            }
        }
    }

    private void TryLaunchAttackWave()
    {
        List<SelectableUnit> combatUnits = GetCombatUnits();

        if (combatUnits.Count < minimumWaveSize)
        {
            if (showDebugLogs)
            {
                Debug.Log("AI attack wave delayed. Combat units: " + combatUnits.Count + "/" + minimumWaveSize);
            }

            return;
        }

        Vector3 attackTarget = FindEnemyTargetPosition();

        int sentCount = 0;

        foreach (SelectableUnit unit in combatUnits)
        {
            if (unit == null || unit.Unit == null)
                continue;

            RTSCombatController combat = unit.GetComponent<RTSCombatController>();

            if (combat == null)
                continue;

            if (!sendAllCombatUnits)
            {
                if (combat.CurrentState != RTSCommandState.Idle &&
                    combat.CurrentState != RTSCommandState.Stopped &&
                    combat.CurrentState != RTSCommandState.HoldPosition)
                {
                    continue;
                }
            }

            unit.AttackMoveTo(attackTarget);
            sentCount++;
        }

        if (showDebugLogs)
        {
            Debug.Log("AI launched attack wave with " + sentCount + " units toward " + attackTarget);
        }
    }

    private ProductionOption FindWorkerOption(UnitProducer producer)
    {
        foreach (ProductionOption option in producer.productionOptions)
        {
            if (option == null || option.unitPrefab == null)
                continue;

            RTSUnit unit = option.unitPrefab.GetComponent<RTSUnit>();

            if (unit == null)
                continue;

            if (unit.canGather)
                return option;
        }

        return null;
    }

    private List<ProductionOption> FindCombatOptions(UnitProducer producer)
    {
        List<ProductionOption> options = new List<ProductionOption>();

        foreach (ProductionOption option in producer.productionOptions)
        {
            if (option == null || option.unitPrefab == null)
                continue;

            RTSUnit unit = option.unitPrefab.GetComponent<RTSUnit>();

            if (unit == null)
                continue;

            if (!unit.canGather && unit.canAttack && unit.supplyCost > 0)
            {
                options.Add(option);
            }
        }

        return options;
    }

    private bool CanAffordAndSupply(ProductionOption option)
    {
        if (ResourceManager.Instance == null)
            return false;

        if (option == null || option.unitPrefab == null)
            return false;

        if (ResourceManager.Instance.GetMinerals(aiFaction) < option.mineralCost)
            return false;

        RTSUnit unit = option.unitPrefab.GetComponent<RTSUnit>();

        if (unit == null)
            return true;

        return ResourceManager.Instance.CanReserveSupply(aiFaction, unit.supplyCost);
    }

    private int CountWorkers()
    {
        int count = 0;

        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        foreach (RTSUnit unit in units)
        {
            if (unit == null || unit.IsDead)
                continue;

            if (unit.faction != aiFaction)
                continue;

            if (unit.canGather)
                count++;
        }

        return count;
    }

    private int CountCombatUnits()
    {
        int count = 0;

        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        foreach (RTSUnit unit in units)
        {
            if (unit == null || unit.IsDead)
                continue;

            if (unit.faction != aiFaction)
                continue;

            if (unit.canGather)
                continue;

            if (unit.canAttack && unit.supplyCost > 0)
                count++;
        }

        return count;
    }

    private List<SelectableUnit> GetCombatUnits()
    {
        List<SelectableUnit> result = new List<SelectableUnit>();

        IReadOnlyList<SelectableUnit> selectableUnits = SelectableUnit.AllSelectableUnits;

        foreach (SelectableUnit selectable in selectableUnits)
        {
            if (selectable == null || selectable.Unit == null)
                continue;

            RTSUnit unit = selectable.Unit;

            if (unit.IsDead)
                continue;

            if (unit.faction != aiFaction)
                continue;

            if (unit.canGather)
                continue;

            if (!unit.canAttack)
                continue;

            if (unit.supplyCost <= 0)
                continue;

            result.Add(selectable);
        }

        return result;
    }

    private MineralNode FindNearestAvailableMineral(Vector3 fromPosition)
    {
        IReadOnlyList<MineralNode> minerals = MineralNode.AllMineralNodes;

        MineralNode nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (MineralNode mineral in minerals)
        {
            if (mineral == null || mineral.IsEmpty)
                continue;

            float distance = HorizontalDistance(fromPosition, mineral.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = mineral;
            }
        }

        return nearest;
    }

    private Vector3 FindEnemyTargetPosition()
    {
        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        RTSUnit bestTarget = null;
        float bestScore = Mathf.NegativeInfinity;

        foreach (RTSUnit unit in units)
        {
            if (unit == null || unit.IsDead || !unit.isTargetable)
                continue;

            if (unit.faction != enemyFaction)
                continue;

            float score = 0f;

            if (unit.GetComponent<SelectableBuilding>() != null)
                score += 50f;

            if (unit.canGather)
                score += 25f;

            score += Random.Range(0f, 5f);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = unit;
            }
        }

        if (bestTarget != null)
            return bestTarget.transform.position;

        return Vector3.zero;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}