using UnityEngine;
using UnityEngine.AI;

public class BuildingConstructionController : MonoBehaviour
{
    [Header("Construction")]
    public float buildTime = 10f;
    public float buildProgress;
    public bool IsComplete { get; private set; }

    [Header("Visual")]
    public bool scaleDuringConstruction = true;
    public float startingHeightMultiplier = 0.25f;

    [Header("Build Point")]
    public float buildPointOffset = 1.0f;
    public float navMeshSearchRadius = 3f;

    [Header("Refund")]
    public int mineralCost;
    public float defaultCancelRefundFraction = 0.75f;

    public float BuildProgress01
    {
        get
        {
            if (buildTime <= 0f)
                return 1f;

            return Mathf.Clamp01(buildProgress / buildTime);
        }
    }

    private RTSUnit unit;
    private UnitProducer producer;
    private ResourceDepot depot;
    private SupplyProvider supplyProvider;
    private RTSTurretController turret;
    private SupplyBlockController supplyBlock;
    private Collider buildingCollider;

    private Vector3 originalScale;
    private Vector3 originalPosition;

    private bool initialized;

    private void Awake()
    {
        CacheComponents();

        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    private void CacheComponents()
    {
        unit = GetComponent<RTSUnit>();
        producer = GetComponent<UnitProducer>();
        depot = GetComponent<ResourceDepot>();
        supplyProvider = GetComponent<SupplyProvider>();
        turret = GetComponent<RTSTurretController>();
        supplyBlock = GetComponent<SupplyBlockController>();
        buildingCollider = GetComponentInChildren<Collider>();
    }

    public void Initialize(RTSFaction faction, float newBuildTime, int newMineralCost = 0)
    {
        CacheComponents();

        buildTime = Mathf.Max(0.1f, newBuildTime);
        mineralCost = Mathf.Max(0, newMineralCost);
        buildProgress = 0f;
        IsComplete = false;
        initialized = true;

        if (unit != null)
        {
            unit.faction = faction;
            unit.currentHp = 1;
            unit.isTargetable = true;
        }

        if (depot != null)
        {
            depot.faction = faction;
        }

        if (supplyProvider != null)
        {
            supplyProvider.faction = faction;
        }

        if (producer != null)
        {
            producer.faction = faction;
        }

        SetOperational(false);
        ApplyConstructionVisual();

        Debug.Log(name + " construction started.");
    }

    public void AddBuildProgress(float amount)
    {
        if (!initialized || IsComplete)
            return;

        buildProgress += amount;

        if (unit != null)
        {
            int constructedHp = Mathf.Max(1, Mathf.RoundToInt(unit.maxHp * BuildProgress01));
            unit.currentHp = Mathf.Max(unit.currentHp, constructedHp);
        }

        ApplyConstructionVisual();

        if (buildProgress >= buildTime)
        {
            CompleteConstruction();
        }
    }

    public Vector3 GetBuildPoint(Vector3 fromPosition)
    {
        if (buildingCollider == null)
        {
            return transform.position;
        }

        Vector3 closestPoint = buildingCollider.ClosestPoint(fromPosition);

        Vector3 directionFromCenter = closestPoint - transform.position;
        directionFromCenter.y = 0f;

        if (directionFromCenter.sqrMagnitude < 0.001f)
        {
            directionFromCenter = fromPosition - transform.position;
            directionFromCenter.y = 0f;
        }

        if (directionFromCenter.sqrMagnitude < 0.001f)
        {
            directionFromCenter = transform.forward;
        }

        Vector3 outsidePoint = closestPoint + directionFromCenter.normalized * buildPointOffset;

        if (NavMesh.SamplePosition(outsidePoint, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return outsidePoint;
    }

    private void CompleteConstruction()
    {
        IsComplete = true;
        buildProgress = buildTime;

        transform.localScale = originalScale;
        transform.localPosition = originalPosition;

        if (unit != null)
        {
            unit.currentHp = unit.maxHp;
        }

        SetOperational(true);

        Debug.Log(name + " construction complete.");
    }

    public void CancelConstruction(float refundFraction = -1f)
    {
        if (IsComplete)
        {
            Debug.LogWarning(name + " is already complete and cannot be cancelled as construction.");
            return;
        }

        if (refundFraction < 0f)
        {
            refundFraction = defaultCancelRefundFraction;
        }

        refundFraction = Mathf.Clamp01(refundFraction);

        if (ResourceManager.Instance != null && unit != null && mineralCost > 0)
        {
            int refundAmount = Mathf.RoundToInt(mineralCost * refundFraction);
            ResourceManager.Instance.AddMinerals(unit.faction, refundAmount);
            Debug.Log(name + " construction cancelled. Refunded " + refundAmount + " minerals.");
        }

        Destroy(gameObject);
    }

    private void SetOperational(bool operational)
    {
        if (depot != null)
        {
            depot.isOperational = operational;
        }

        if (supplyProvider != null)
        {
            supplyProvider.isOperational = operational;
        }

        if (producer != null)
        {
            producer.enabled = operational;
        }

        if (turret != null)
        {
            turret.enabled = operational;
        }

        if (supplyBlock != null)
        {
            supplyBlock.enabled = operational;
        }
    }

    private void ApplyConstructionVisual()
    {
        if (!scaleDuringConstruction)
            return;

        float progress = BuildProgress01;
        float yMultiplier = Mathf.Lerp(startingHeightMultiplier, 1f, progress);

        Vector3 newScale = originalScale;
        newScale.y = originalScale.y * yMultiplier;

        transform.localScale = newScale;
    }
}