using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class SelectableBuilding : MonoBehaviour
{
    private static readonly List<SelectableBuilding> allBuildings = new List<SelectableBuilding>();
    public static IReadOnlyList<SelectableBuilding> AllBuildings => allBuildings;

    [Header("Selection")]
    public Color selectedColor = Color.cyan;

    private Renderer[] renderers;
    private Color[] originalColors;

    public bool IsSelected { get; private set; }

    public RTSUnit Unit { get; private set; }
    public UnitProducer Producer { get; private set; }
    public ResourceDepot Depot { get; private set; }
    public SupplyProvider Supply { get; private set; }
    public RTSTurretController Turret { get; private set; }
    public SupplyBlockController SupplyBlock { get; private set; }

    private BuildingConstructionController construction;
    public BuildingConstructionController Construction
    {
        get
        {
            if (construction == null)
            {
                construction = GetComponent<BuildingConstructionController>();
            }

            return construction;
        }
    }

    private void Awake()
    {
        RefreshCachedComponents();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    private void OnEnable()
    {
        if (!allBuildings.Contains(this))
        {
            allBuildings.Add(this);
        }
    }

    private void OnDisable()
    {
        allBuildings.Remove(this);
    }

    public void RefreshCachedComponents()
    {
        Unit = GetComponent<RTSUnit>();
        Producer = GetComponent<UnitProducer>();
        Depot = GetComponent<ResourceDepot>();
        Supply = GetComponent<SupplyProvider>();
        Turret = GetComponent<RTSTurretController>();
        SupplyBlock = GetComponent<SupplyBlockController>();
        construction = GetComponent<BuildingConstructionController>();
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].material.color = selected ? selectedColor : originalColors[i];
            }
        }
    }

    public Vector3 GetNearestInteractionPoint(Vector3 fromPosition, float offset = 1f, float navMeshSearchRadius = 3f)
    {
        Collider buildingCollider = GetComponentInChildren<Collider>();

        if (buildingCollider == null)
            return transform.position;

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

        Vector3 outsidePoint = closestPoint + directionFromCenter.normalized * offset;

        if (NavMesh.SamplePosition(outsidePoint, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return outsidePoint;
    }

}