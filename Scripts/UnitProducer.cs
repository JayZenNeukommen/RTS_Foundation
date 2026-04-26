using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class ProductionOption
{
    public string displayName;
    public KeyCode hotkey;
    public GameObject unitPrefab;
    public int mineralCost = 50;
    public float buildTime = 5f;
}

public class UnitProducer : MonoBehaviour
{
    private static readonly List<UnitProducer> allProducers = new List<UnitProducer>();
    public static IReadOnlyList<UnitProducer> AllProducers => allProducers;

    [Header("Producer Settings")]
    public RTSFaction faction = RTSFaction.Player1;
    public Transform spawnPoint;
    public float spawnSearchRadius = 3f;

    [Header("Rally")]
    public bool hasRallyPoint;
    public Vector3 rallyPoint;

    private MineralNode rallyMineral;
    private RTSUnit rallyAttackTarget;

    [Header("Production Options")]
    public List<ProductionOption> productionOptions = new List<ProductionOption>();

    [Header("Selection")]
    public Color selectedColor = Color.cyan;

    private Renderer[] renderers;
    private Color[] originalColors;

    private Queue<ProductionOption> productionQueue = new Queue<ProductionOption>();
    private ProductionOption currentProduction;
    private float productionTimer;

    private Queue<int> reservedSupplyQueue = new Queue<int>();
    private int currentProductionReservedSupply;

    public string CurrentProductionName => currentProduction != null ? currentProduction.displayName : "";
    public bool IsProducing => currentProduction != null;
    public float ProductionProgress01 => currentProduction != null && currentProduction.buildTime > 0f
        ? 1f - Mathf.Clamp01(productionTimer / currentProduction.buildTime)
        : 0f;
    public int QueueCount => productionQueue.Count;

    public int TotalQueueCount
    {
        get
        {
            int active = currentProduction != null ? 1 : 0;
            return active + productionQueue.Count;
        }
    }

    public string[] GetQueuedProductionNames()
    {
        ProductionOption[] queuedOptions = productionQueue.ToArray();
        string[] names = new string[queuedOptions.Length];

        for (int i = 0; i < queuedOptions.Length; i++)
        {
            names[i] = queuedOptions[i] != null ? queuedOptions[i].displayName : "Unknown";
        }

        return names;
    }

    public bool IsSelected { get; private set; }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }

        if (spawnPoint == null)
        {
            Transform foundSpawn = transform.Find("SpawnPoint");

            if (foundSpawn != null)
            {
                spawnPoint = foundSpawn;
            }
        }
    }

    private void OnEnable()
    {
        if (!allProducers.Contains(this))
        {
            allProducers.Add(this);
        }
    }

    private void OnDisable()
    {
        allProducers.Remove(this);
    }

    private void Update()
    {
        HandleProduction();
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = selected ? selectedColor : originalColors[i];
        }

        if (selected)
        {
            Debug.Log(name + " selected. Available production:");

            foreach (ProductionOption option in productionOptions)
            {
                Debug.Log(option.hotkey + " = " + option.displayName + " | Cost: " + option.mineralCost);
            }
        }
    }

    public void TryQueueProduction(KeyCode key)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(name + " is not operational yet.");
            return;
        }

        foreach (ProductionOption option in productionOptions)
        {
            if (option.hotkey == key)
            {
                QueueProduction(option);
                return;
            }
        }
    }

    private void QueueProduction(ProductionOption option)
    {
        if (option.unitPrefab == null)
        {
            Debug.LogWarning(option.displayName + " has no unit prefab assigned.");
            return;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("No ResourceManager found.");
            return;
        }

        int supplyCost = GetSupplyCost(option);

        if (!ResourceManager.Instance.CanReserveSupply(faction, supplyCost))
        {
            Debug.LogWarning("Not enough supply to train " + option.displayName);
            return;
        }

        bool paid = ResourceManager.Instance.SpendMinerals(faction, option.mineralCost);

        if (!paid)
        {
            Debug.LogWarning("Not enough minerals to train " + option.displayName);
            return;
        }

        bool reserved = ResourceManager.Instance.ReserveSupply(faction, supplyCost);

        if (!reserved)
        {
            Debug.LogWarning("Failed to reserve supply for " + option.displayName);
            return;
        }

        productionQueue.Enqueue(option);
        reservedSupplyQueue.Enqueue(supplyCost);

        Debug.Log(option.displayName + " queued at " + name);
        Debug.Log(ResourceManager.Instance.GetSupplyText(faction));

        if (currentProduction == null)
        {
            StartNextProduction();
        }
    }

    private void HandleProduction()
    {
        if (currentProduction == null)
            return;

        productionTimer -= Time.deltaTime;

        if (productionTimer > 0f)
            return;

        CompleteProduction();
        StartNextProduction();
    }

    private void StartNextProduction()
    {
        if (productionQueue.Count == 0)
        {
            currentProduction = null;
            currentProductionReservedSupply = 0;
            return;
        }

        currentProduction = productionQueue.Dequeue();
        currentProductionReservedSupply = reservedSupplyQueue.Dequeue();

        productionTimer = currentProduction.buildTime;

        Debug.Log(name + " started training " + currentProduction.displayName);
    }

    private void CompleteProduction()
    {
        if (currentProduction == null)
            return;

        Vector3 spawnPosition = GetSpawnPosition();

        GameObject createdUnit = Instantiate(
            currentProduction.unitPrefab,
            spawnPosition,
            Quaternion.identity
        );

        RTSUnit createdRTSUnit = createdUnit.GetComponent<RTSUnit>();

        if (createdRTSUnit != null)
        {
            createdRTSUnit.faction = faction;
        }

        NavMeshAgent agent = createdUnit.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.Warp(spawnPosition);
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ReleaseReservedSupply(faction, currentProductionReservedSupply);
        }

        Debug.Log(name + " completed " + currentProduction.displayName);

        IssueRallyCommand(createdUnit);
    }

    private int GetSupplyCost(ProductionOption option)
    {
        if (option == null || option.unitPrefab == null)
            return 0;

        RTSUnit unit = option.unitPrefab.GetComponent<RTSUnit>();

        if (unit == null)
            return 0;

        return Mathf.Max(0, unit.supplyCost);
    }

    private void IssueRallyCommand(GameObject createdUnit)
    {
        if (!hasRallyPoint)
            return;

        SelectableUnit selectable = createdUnit.GetComponent<SelectableUnit>();
        RTSUnit createdRTSUnit = createdUnit.GetComponent<RTSUnit>();

        if (selectable == null || createdRTSUnit == null)
            return;

        if (rallyMineral != null && !rallyMineral.IsEmpty && createdRTSUnit.canGather)
        {
            selectable.Gather(rallyMineral);
            return;
        }

        if (rallyAttackTarget != null && !rallyAttackTarget.IsDead && createdRTSUnit.IsEnemy(rallyAttackTarget))
        {
            selectable.AttackTarget(rallyAttackTarget);
            return;
        }

        selectable.MoveTo(rallyPoint);
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 preferredDirection = GetPreferredSpawnDirection();

        Vector3 spawnPosition;

        if (TryFindSpawnPositionInDirection(preferredDirection, out spawnPosition))
        {
            return spawnPosition;
        }

        Vector3[] fallbackDirections =
        {
        transform.forward,
        -transform.forward,
        transform.right,
        -transform.right,
        (transform.forward + transform.right).normalized,
        (transform.forward - transform.right).normalized,
        (-transform.forward + transform.right).normalized,
        (-transform.forward - transform.right).normalized
    };

        foreach (Vector3 direction in fallbackDirections)
        {
            if (TryFindSpawnPositionInDirection(direction, out spawnPosition))
            {
                return spawnPosition;
            }
        }

        Vector3 basePosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * spawnSearchRadius;

        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, spawnSearchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return basePosition;
    }

    private Vector3 GetPreferredSpawnDirection()
    {
        if (hasRallyPoint)
        {
            Vector3 directionToRally = rallyPoint - transform.position;
            directionToRally.y = 0f;

            if (directionToRally.sqrMagnitude > 0.01f)
            {
                return directionToRally.normalized;
            }
        }

        if (spawnPoint != null)
        {
            Vector3 directionToSpawnPoint = spawnPoint.position - transform.position;
            directionToSpawnPoint.y = 0f;

            if (directionToSpawnPoint.sqrMagnitude > 0.01f)
            {
                return directionToSpawnPoint.normalized;
            }
        }

        return transform.forward;
    }

    private bool TryFindSpawnPositionInDirection(Vector3 direction, out Vector3 spawnPosition)
    {
        spawnPosition = transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        Collider buildingCollider = GetComponentInChildren<Collider>();

        float distanceFromCenter = spawnSearchRadius;

        if (buildingCollider != null)
        {
            Bounds bounds = buildingCollider.bounds;
            float horizontalRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            distanceFromCenter = horizontalRadius + 1.25f;
        }

        Vector3 candidate = transform.position + direction * distanceFromCenter;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, spawnSearchRadius, NavMesh.AllAreas))
        {
            if (buildingCollider == null || IsPointOutsideBuildingFootprint(hit.position, buildingCollider))
            {
                spawnPosition = hit.position;
                return true;
            }
        }

        return false;
    }

    private bool IsPointOutsideBuildingFootprint(Vector3 point, Collider buildingCollider)
    {
        Bounds bounds = buildingCollider.bounds;

        float padding = 0.15f;

        bool outsideX =
            point.x < bounds.min.x - padding ||
            point.x > bounds.max.x + padding;

        bool outsideZ =
            point.z < bounds.min.z - padding ||
            point.z > bounds.max.z + padding;

        return outsideX || outsideZ;
    }

    public void SetRallyPoint(Vector3 point)
    {
        hasRallyPoint = true;
        rallyPoint = point;
        rallyMineral = null;
        rallyAttackTarget = null;

        Debug.Log(name + " rally point set to ground position: " + point);
    }

    public void SetRallyMineral(MineralNode mineral)
    {
        if (mineral == null)
            return;

        hasRallyPoint = true;
        rallyPoint = mineral.transform.position;
        rallyMineral = mineral;
        rallyAttackTarget = null;

        Debug.Log(name + " rally point set to mineral: " + mineral.name);
    }

    public void SetRallyAttackTarget(RTSUnit target)
    {
        if (target == null)
            return;

        hasRallyPoint = true;
        rallyPoint = target.transform.position;
        rallyMineral = null;
        rallyAttackTarget = target;

        Debug.Log(name + " rally point set to attack target: " + target.unitName);
    }

    public void CancelCurrentProduction(float refundFraction = 1f)
    {
        if (currentProduction == null)
        {
            Debug.Log(name + " has no active production to cancel.");
            return;
        }

        refundFraction = Mathf.Clamp01(refundFraction);

        if (ResourceManager.Instance != null)
        {
            int refundAmount = Mathf.RoundToInt(currentProduction.mineralCost * refundFraction);

            ResourceManager.Instance.ReleaseReservedSupply(faction, currentProductionReservedSupply);
            ResourceManager.Instance.AddMinerals(faction, refundAmount);

            Debug.Log(
                name + " cancelled " + currentProduction.displayName +
                ". Refunded " + refundAmount + " minerals."
            );
        }

        currentProduction = null;
        currentProductionReservedSupply = 0;
        productionTimer = 0f;

        StartNextProduction();
    }

    public void CancelLatestQueuedProduction(float refundFraction = 1f)
    {
        if (productionQueue.Count == 0)
        {
            Debug.Log(name + " has no queued production to cancel.");
            return;
        }

        refundFraction = Mathf.Clamp01(refundFraction);

        List<ProductionOption> optionList = new List<ProductionOption>(productionQueue);
        List<int> supplyList = new List<int>(reservedSupplyQueue);

        int lastIndex = optionList.Count - 1;

        ProductionOption cancelledOption = optionList[lastIndex];
        int cancelledSupply = supplyList[lastIndex];

        optionList.RemoveAt(lastIndex);
        supplyList.RemoveAt(lastIndex);

        productionQueue = new Queue<ProductionOption>(optionList);
        reservedSupplyQueue = new Queue<int>(supplyList);

        if (ResourceManager.Instance != null && cancelledOption != null)
        {
            int refundAmount = Mathf.RoundToInt(cancelledOption.mineralCost * refundFraction);

            ResourceManager.Instance.ReleaseReservedSupply(faction, cancelledSupply);
            ResourceManager.Instance.AddMinerals(faction, refundAmount);

            Debug.Log(
                name + " cancelled queued " + cancelledOption.displayName +
                ". Refunded " + refundAmount + " minerals."
            );
        }
    }

}