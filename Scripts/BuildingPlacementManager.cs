using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class BuildingPlacementOption
{
    public string displayName;
    public KeyCode hotkey;
    public GameObject buildingPrefab;
    public int mineralCost = 100;
    public float buildTime = 10f;

    [Header("Placement")]
    public Vector2 footprintSize = new Vector2(2f, 2f);
    public float yOffset = 0.5f;
}

public class BuildingPlacementManager : MonoBehaviour
{
    public static BuildingPlacementManager Instance { get; private set; }

    [Header("Placement Options")]
    public List<BuildingPlacementOption> buildingOptions = new List<BuildingPlacementOption>();

    [Header("Ghost Settings")]
    public Material validGhostMaterial;
    public Material invalidGhostMaterial;
    public float gridSize = 1f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private BuildingPlacementOption currentOption;
    private SelectableUnit currentBuilder;
    private GameObject ghostObject;
    private WalkableGround currentGroundUnderMouse;

    private Vector3 currentPlacementPosition;
    private Quaternion currentPlacementRotation = Quaternion.identity;
    private bool currentPlacementValid;

    public bool IsPlacing => currentOption != null;

    private Camera mainCamera;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!IsPlacing)
            return;

        UpdateGhostPosition();

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentPlacementRotation *= Quaternion.Euler(0f, 90f, 0f);
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceBuilding();
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void TryBeginPlacementByHotkey(SelectableUnit builder, KeyCode hotkey)
    {
        if (builder == null || builder.Unit == null)
            return;

        if (!builder.Unit.canBuild)
        {
            Debug.LogWarning(builder.name + " cannot build.");
            return;
        }

        foreach (BuildingPlacementOption option in buildingOptions)
        {
            if (option.hotkey == hotkey)
            {
                BeginPlacement(builder, option);
                return;
            }
        }
    }

    private void BeginPlacement(SelectableUnit builder, BuildingPlacementOption option)
    {
        if (option.buildingPrefab == null)
        {
            Debug.LogWarning(option.displayName + " has no prefab assigned.");
            return;
        }

        currentBuilder = builder;
        currentOption = option;

        if (ghostObject != null)
        {
            Destroy(ghostObject);
        }

        ghostObject = Instantiate(option.buildingPrefab);
        ghostObject.name = option.displayName + "_Ghost";

        DisableGhostGameplay(ghostObject);
        SetGhostMaterial(validGhostMaterial);

        if (showDebugLogs)
        {
            Debug.Log("Placing " + option.displayName + ". Left-click to place. Right-click/Escape to cancel. R to rotate.");
        }
    }

    private void UpdateGhostPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
        {
            if (ghostObject != null)
                ghostObject.SetActive(false);

            currentGroundUnderMouse = null;
            currentPlacementValid = false;
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool foundGround = false;
        RaycastHit groundHit = new RaycastHit();

        foreach (RaycastHit hit in hits)
        {
            WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

            if (ground != null)
            {
                currentGroundUnderMouse = ground;
                groundHit = hit;
                foundGround = true;
                break;
            }
        }

        if (!foundGround)
        {
            if (ghostObject != null)
                ghostObject.SetActive(false);

            currentGroundUnderMouse = null;
            currentPlacementValid = false;
            return;
        }

        Vector3 snapped = SnapToGrid(groundHit.point);
        snapped.y = groundHit.point.y + currentOption.yOffset;

        currentPlacementPosition = snapped;

        if (ghostObject != null)
        {
            ghostObject.SetActive(true);
            ghostObject.transform.position = currentPlacementPosition;
            ghostObject.transform.rotation = currentPlacementRotation;
        }

        currentPlacementValid = IsPlacementValid(currentPlacementPosition);

        SetGhostMaterial(currentPlacementValid ? validGhostMaterial : invalidGhostMaterial);
    }

    private bool IsPlacementValid(Vector3 position)
    {
        if (currentBuilder == null || currentBuilder.Unit == null)
            return false;

        if (currentGroundUnderMouse == null)
            return false;

        if (!currentGroundUnderMouse.allowBuilding)
            return false;

        if (ResourceManager.Instance == null)
            return false;

        int minerals = ResourceManager.Instance.GetMinerals(currentBuilder.Unit.faction);

        if (minerals < currentOption.mineralCost)
            return false;

        Vector3 halfExtents = new Vector3(
            currentOption.footprintSize.x / 2f,
            1f,
            currentOption.footprintSize.y / 2f
        );

        Vector3 checkCenter = position;
        checkCenter.y += 0.5f;

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            currentPlacementRotation,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (ghostObject != null && hit.transform.IsChildOf(ghostObject.transform))
                continue;

            if (hit.GetComponentInParent<WalkableGround>() != null)
                continue;

            return false;
        }

        if (!NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        return true;
    }

    private void TryPlaceBuilding()
    {
        if (!currentPlacementValid)
        {
            Debug.LogWarning("Cannot place building here.");
            return;
        }

        RTSFaction faction = currentBuilder.Unit.faction;

        bool paid = ResourceManager.Instance.SpendMinerals(faction, currentOption.mineralCost);

        if (!paid)
        {
            Debug.LogWarning("Not enough minerals.");
            return;
        }

        GameObject building = Instantiate(
             currentOption.buildingPrefab,
             currentPlacementPosition,
             currentPlacementRotation
         );

        building.name = currentOption.displayName;

        ApplyFaction(building, faction);

        BuildingConstructionController construction =
            building.GetComponent<BuildingConstructionController>();

        if (construction == null)
        {
            construction = building.AddComponent<BuildingConstructionController>();
        }

        SelectableBuilding selectableBuilding = building.GetComponent<SelectableBuilding>();

        if (selectableBuilding != null)
        {
            selectableBuilding.RefreshCachedComponents();
        }

        construction.Initialize(faction, currentOption.buildTime, currentOption.mineralCost);

        SelectableUnit builder = currentBuilder;

        if (builder != null)
        {
            builder.StartBuilding(construction);
        }

        Debug.Log("Placed construction site for " + currentOption.displayName);

        CancelPlacement();
    }

    private void ApplyFaction(GameObject building, RTSFaction faction)
    {
        RTSUnit rtsUnit = building.GetComponent<RTSUnit>();

        if (rtsUnit != null)
        {
            rtsUnit.faction = faction;
        }

        ResourceDepot depot = building.GetComponent<ResourceDepot>();

        if (depot != null)
        {
            depot.faction = faction;
        }

        UnitProducer producer = building.GetComponent<UnitProducer>();

        if (producer != null)
        {
            producer.faction = faction;
        }

        SupplyProvider supplyProvider = building.GetComponent<SupplyProvider>();

        if (supplyProvider != null)
        {
            supplyProvider.faction = faction;
        }
    }

    public void CancelPlacement()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
        }

        ghostObject = null;
        currentOption = null;
        currentBuilder = null;
        currentPlacementValid = false;
        currentGroundUnderMouse = null;
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        if (gridSize <= 0f)
            return position;

        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;

        return position;
    }

    private void DisableGhostGameplay(GameObject ghost)
    {
        RTSUnit[] units = ghost.GetComponentsInChildren<RTSUnit>();

        foreach (RTSUnit unit in units)
        {
            unit.isTargetable = false;
            unit.faction = RTSFaction.Neutral;
        }

        ResourceDepot[] depots = ghost.GetComponentsInChildren<ResourceDepot>();

        foreach (ResourceDepot depot in depots)
        {
            depot.faction = RTSFaction.Neutral;
            depot.isOperational = false;
            depot.enabled = false;
        }

        SupplyProvider[] supplyProviders = ghost.GetComponentsInChildren<SupplyProvider>();

        foreach (SupplyProvider supplyProvider in supplyProviders)
        {
            supplyProvider.faction = RTSFaction.Neutral;
            supplyProvider.isOperational = false;
            supplyProvider.enabled = false;
        }

        UnitProducer[] producers = ghost.GetComponentsInChildren<UnitProducer>();

        foreach (UnitProducer producer in producers)
        {
            producer.faction = RTSFaction.Neutral;
            producer.enabled = false;
        }

        SelectableBuilding[] selectableBuildings = ghost.GetComponentsInChildren<SelectableBuilding>();

        foreach (SelectableBuilding selectableBuilding in selectableBuildings)
        {
            selectableBuilding.enabled = false;
        }

        BuildingConstructionController[] constructionControllers = ghost.GetComponentsInChildren<BuildingConstructionController>();

        foreach (BuildingConstructionController construction in constructionControllers)
        {
            construction.enabled = false;
        }

        RTSTurretController[] turrets = ghost.GetComponentsInChildren<RTSTurretController>();

        foreach (RTSTurretController turret in turrets)
        {
            turret.enabled = false;
        }

        RTSCombatController[] combatControllers = ghost.GetComponentsInChildren<RTSCombatController>();

        foreach (RTSCombatController combatController in combatControllers)
        {
            combatController.enabled = false;
        }

        WorkerGatherer[] gatherers = ghost.GetComponentsInChildren<WorkerGatherer>();

        foreach (WorkerGatherer gatherer in gatherers)
        {
            gatherer.enabled = false;
        }

        WorkerBuilder[] builders = ghost.GetComponentsInChildren<WorkerBuilder>();

        foreach (WorkerBuilder builder in builders)
        {
            builder.enabled = false;
        }

        NavMeshObstacle[] obstacles = ghost.GetComponentsInChildren<NavMeshObstacle>();

        foreach (NavMeshObstacle obstacle in obstacles)
        {
            obstacle.enabled = false;
        }

        NavMeshAgent[] agents = ghost.GetComponentsInChildren<NavMeshAgent>();

        foreach (NavMeshAgent agent in agents)
        {
            agent.enabled = false;
        }

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private void SetGhostMaterial(Material material)
    {
        if (ghostObject == null || material == null)
            return;

        Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            renderer.material = material;
        }
    }

    private void OnDrawGizmos()
    {
        if (!IsPlacing || currentOption == null)
            return;

        Gizmos.color = currentPlacementValid ? Color.green : Color.red;

        Vector3 size = new Vector3(
            currentOption.footprintSize.x,
            2f,
            currentOption.footprintSize.y
        );

        Gizmos.matrix = Matrix4x4.TRS(
            currentPlacementPosition + Vector3.up * 0.5f,
            currentPlacementRotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.matrix = Matrix4x4.identity;
    }
}