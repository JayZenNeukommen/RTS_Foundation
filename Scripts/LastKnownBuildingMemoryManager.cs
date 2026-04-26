using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class LastKnownBuildingMemoryManager : MonoBehaviour
{
    private class BuildingMemoryRecord
    {
        public SelectableBuilding realBuilding;
        public GameObject ghostObject;
        public Vector3 lastKnownPosition;
        public Quaternion lastKnownRotation;
    }

    [Header("Memory Settings")]
    public RTSFaction localViewFaction = RTSFaction.Player1;
    public float updateInterval = 0.25f;

    [Header("Ghost Visual")]
    public Material ghostMaterial;
    public bool showGhostsOnlyInExploredFog = true;

    private readonly Dictionary<SelectableBuilding, BuildingMemoryRecord> records = new Dictionary<SelectableBuilding, BuildingMemoryRecord>();
    private float updateTimer;

    private void Update()
    {
        updateTimer -= Time.deltaTime;

        if (updateTimer > 0f)
            return;

        updateTimer = updateInterval;

        if (FogOfWarManager.Instance == null)
            return;

        ScanVisibleEnemyBuildings();
        UpdateMemoryRecords();
    }

    private void ScanVisibleEnemyBuildings()
    {
        IReadOnlyList<SelectableBuilding> buildings = SelectableBuilding.AllBuildings;

        foreach (SelectableBuilding building in buildings)
        {
            if (building == null || building.Unit == null)
                continue;

            RTSUnit unit = building.Unit;

            if (unit.IsDead || !unit.isTargetable)
                continue;

            if (unit.faction == RTSFaction.Neutral)
                continue;

            if (unit.faction == localViewFaction)
                continue;

            bool visible = FogOfWarManager.Instance.IsPositionVisibleToFaction(
                building.transform.position,
                localViewFaction
            );

            if (!visible)
                continue;

            SelectableBuilding key = building;

            if (!records.TryGetValue(key, out BuildingMemoryRecord record))
            {
                record = new BuildingMemoryRecord();
                record.ghostObject = CreateGhostObject(building);

                records.Add(key, record);
            }

            record.realBuilding = building;
            record.lastKnownPosition = building.transform.position;
            record.lastKnownRotation = building.transform.rotation;

            UpdateGhostTransform(record);

            if (record.ghostObject != null)
            {
                record.ghostObject.SetActive(false);
            }
        }
    }

    private void UpdateMemoryRecords()
    {
        List<SelectableBuilding> recordsToRemove = new List<SelectableBuilding>();

        foreach (KeyValuePair<SelectableBuilding, BuildingMemoryRecord> pair in records)
        {
            BuildingMemoryRecord record = pair.Value;

            if (record == null)
            {
                recordsToRemove.Add(pair.Key);
                continue;
            }

            bool realExists = record.realBuilding != null;

            bool lastKnownVisible = FogOfWarManager.Instance.IsPositionVisibleToFaction(
                record.lastKnownPosition,
                localViewFaction
            );

            bool lastKnownExplored = FogOfWarManager.Instance.IsPositionExploredToFaction(
                record.lastKnownPosition,
                localViewFaction
            );

            if (!realExists)
            {
                if (lastKnownVisible)
                {
                    recordsToRemove.Add(pair.Key);
                    continue;
                }

                SetGhostVisible(record, lastKnownExplored);
                continue;
            }

            bool realCurrentlyVisible = FogOfWarManager.Instance.IsPositionVisibleToFaction(
                record.realBuilding.transform.position,
                localViewFaction
            );

            if (realCurrentlyVisible)
            {
                // The real building is visible, so the memory ghost should be hidden.
                SetGhostVisible(record, false);
                continue;
            }

            if (lastKnownVisible)
            {
                // You can see the old location, but the real building is not visible there.
                // This handles destroyed or moved buildings.
                recordsToRemove.Add(pair.Key);
                continue;
            }

            bool shouldShowGhost = lastKnownExplored || !showGhostsOnlyInExploredFog;
            SetGhostVisible(record, shouldShowGhost);
        }

        foreach (SelectableBuilding building in recordsToRemove)
        {
            RemoveRecord(building);
        }
    }

    private GameObject CreateGhostObject(SelectableBuilding sourceBuilding)
    {
        GameObject ghost = Instantiate(
            sourceBuilding.gameObject,
            sourceBuilding.transform.position,
            sourceBuilding.transform.rotation
        );

        ghost.name = "LastKnown_" + sourceBuilding.name;

        PrepareGhostObject(ghost);

        ghost.SetActive(false);

        return ghost;
    }

    private void PrepareGhostObject(GameObject ghost)
    {
        RTSUnit[] units = ghost.GetComponentsInChildren<RTSUnit>();

        foreach (RTSUnit unit in units)
        {
            unit.faction = RTSFaction.Neutral;
            unit.isTargetable = false;
        }

        ResourceDepot[] depots = ghost.GetComponentsInChildren<ResourceDepot>();

        foreach (ResourceDepot depot in depots)
        {
            depot.faction = RTSFaction.Neutral;
        }

        UnitProducer[] producers = ghost.GetComponentsInChildren<UnitProducer>();

        foreach (UnitProducer producer in producers)
        {
            producer.faction = RTSFaction.Neutral;
        }

        SupplyProvider[] supplyProviders = ghost.GetComponentsInChildren<SupplyProvider>();

        foreach (SupplyProvider provider in supplyProviders)
        {
            provider.faction = RTSFaction.Neutral;
        }

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            Destroy(collider);
        }

        NavMeshObstacle[] obstacles = ghost.GetComponentsInChildren<NavMeshObstacle>();

        foreach (NavMeshObstacle obstacle in obstacles)
        {
            Destroy(obstacle);
        }

        NavMeshAgent[] agents = ghost.GetComponentsInChildren<NavMeshAgent>();

        foreach (NavMeshAgent agent in agents)
        {
            Destroy(agent);
        }

        // Disable gameplay scripts on the ghost.
        // Do not destroy RTSUnit directly; keep it neutral and untargetable.
        MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour is RTSUnit)
                continue;

            behaviour.enabled = false;
            Destroy(behaviour);
        }

        RTSWorldVisuals[] worldVisuals = ghost.GetComponentsInChildren<RTSWorldVisuals>();

        foreach (RTSWorldVisuals visual in worldVisuals)
        {
            Destroy(visual);
        }

        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.enabled = true;

            if (ghostMaterial != null)
            {
                renderer.material = ghostMaterial;
            }
            else
            {
                renderer.material.color = Color.gray;
            }
        }
    }

    private void UpdateGhostTransform(BuildingMemoryRecord record)
    {
        if (record == null || record.ghostObject == null)
            return;

        record.ghostObject.transform.position = record.lastKnownPosition;
        record.ghostObject.transform.rotation = record.lastKnownRotation;
    }

    private void SetGhostVisible(BuildingMemoryRecord record, bool visible)
    {
        if (record == null || record.ghostObject == null)
            return;

        UpdateGhostTransform(record);
        record.ghostObject.SetActive(visible);
    }

    private void RemoveRecord(SelectableBuilding building)
    {
        if (!records.TryGetValue(building, out BuildingMemoryRecord record))
            return;

        if (record.ghostObject != null)
        {
            Destroy(record.ghostObject);
        }

        records.Remove(building);
    }
}