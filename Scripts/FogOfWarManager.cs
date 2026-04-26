using System.Collections.Generic;
using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Height Vision")]
    public bool useHeightVisionRules = true;
    public float groundRaycastHeight = 20f;
    public float groundRaycastDistance = 50f;

    [Header("Map Grid")]
    public Vector3 gridOrigin = new Vector3(-25f, 0f, -25f);
    public int gridWidth = 50;
    public int gridHeight = 50;
    public float cellSize = 1f;

    [Header("Visual Overlay")]
    public RTSFaction localViewFaction = RTSFaction.Player1;
    public float overlayHeight = 4f;
    public Material unexploredMaterial;
    public Material exploredMaterial;

    [Header("Update")]
    public float updateInterval = 0.2f;

    private readonly System.Collections.Generic.Dictionary<RTSUnit, float> temporaryRevealTimers =
    new System.Collections.Generic.Dictionary<RTSUnit, float>();

    private bool[,] player1Visible;
    private bool[,] player2Visible;

    private bool[,] player1Explored;
    private bool[,] player2Explored;

    private Renderer[,] fogRenderers;

    private float updateTimer;

    private void Awake()
    {
        Instance = this;

        player1Visible = new bool[gridWidth, gridHeight];
        player2Visible = new bool[gridWidth, gridHeight];

        player1Explored = new bool[gridWidth, gridHeight];
        player2Explored = new bool[gridWidth, gridHeight];

        fogRenderers = new Renderer[gridWidth, gridHeight];

        CreateFogTiles();
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;

        if (updateTimer > 0f)
            return;

        updateTimer = updateInterval;

        UpdateVision();
        UpdateTemporaryReveals();
        UpdateFogVisuals();
    }

    private void CreateFogTiles()
    {
        GameObject parent = new GameObject("Fog Tiles");
        parent.transform.SetParent(transform);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = "FogTile_" + x + "_" + z;

                tile.transform.SetParent(parent.transform);

                Vector3 worldPosition = GridToWorldCenter(x, z);
                worldPosition.y = overlayHeight;

                tile.transform.position = worldPosition;
                tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                tile.transform.localScale = Vector3.one * cellSize;

                Collider collider = tile.GetComponent<Collider>();

                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = unexploredMaterial;

                fogRenderers[x, z] = renderer;
            }
        }
    }

    public bool IsUnitTemporarilyRevealed(RTSUnit unit)
    {
        if (unit == null)
            return false;

        return temporaryRevealTimers.ContainsKey(unit);
    }

    public void TemporarilyRevealUnit(RTSUnit unit, RTSFaction viewerFaction, float duration)
    {
        if (unit == null)
            return;

        if (duration <= 0f)
            return;

        temporaryRevealTimers[unit] = Mathf.Max(
            temporaryRevealTimers.ContainsKey(unit) ? temporaryRevealTimers[unit] : 0f,
            duration
        );

        RevealAround(unit.transform.position, Mathf.Max(1f, unit.sightRange * 0.25f), viewerFaction);
    }

    private void UpdateTemporaryReveals()
    {
        if (temporaryRevealTimers.Count == 0)
            return;

        List<RTSUnit> toRemove = new List<RTSUnit>();
        List<RTSUnit> toUpdateUnits = new List<RTSUnit>();
        List<float> toUpdateTimes = new List<float>();

        foreach (KeyValuePair<RTSUnit, float> pair in temporaryRevealTimers)
        {
            RTSUnit unit = pair.Key;

            if (unit == null || unit.IsDead)
            {
                toRemove.Add(unit);
                continue;
            }

            float remaining = pair.Value - updateInterval;

            if (remaining <= 0f)
            {
                toRemove.Add(unit);
                continue;
            }

            toUpdateUnits.Add(unit);
            toUpdateTimes.Add(remaining);

            if (unit.faction == RTSFaction.Player1)
            {
                RevealAround(unit.transform.position, Mathf.Max(1f, unit.sightRange * 0.25f), RTSFaction.Player2);
            }
            else if (unit.faction == RTSFaction.Player2)
            {
                RevealAround(unit.transform.position, Mathf.Max(1f, unit.sightRange * 0.25f), RTSFaction.Player1);
            }
        }

        for (int i = 0; i < toUpdateUnits.Count; i++)
        {
            if (toUpdateUnits[i] != null)
            {
                temporaryRevealTimers[toUpdateUnits[i]] = toUpdateTimes[i];
            }
        }

        foreach (RTSUnit unit in toRemove)
        {
            temporaryRevealTimers.Remove(unit);
        }
    }

    private void UpdateVision()
    {
        ClearVisible(player1Visible);
        ClearVisible(player2Visible);

        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        foreach (RTSUnit unit in units)
        {
            if (unit == null || unit.IsDead || !unit.isTargetable)
                continue;

            if (unit.faction == RTSFaction.Neutral)
                continue;

            RevealAround(unit.transform.position, unit.sightRange, unit.faction);
        }
    }

    private void ClearVisible(bool[,] visible)
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                visible[x, z] = false;
            }
        }
    }

    private void RevealAround(Vector3 worldPosition, float radius, RTSFaction faction)
    {
        if (!WorldToGrid(worldPosition, out int centerX, out int centerZ))
            return;

        int viewerVisionLevel = GetVisionLevelAtWorldPosition(worldPosition);

        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int x = centerX - cellRadius; x <= centerX + cellRadius; x++)
        {
            for (int z = centerZ - cellRadius; z <= centerZ + cellRadius; z++)
            {
                if (!IsInsideGrid(x, z))
                    continue;

                Vector3 cellWorld = GridToWorldCenter(x, z);
                float distance = HorizontalDistance(worldPosition, cellWorld);

                if (distance > radius)
                    continue;

                if (useHeightVisionRules)
                {
                    int cellVisionLevel = GetVisionLevelAtWorldPosition(cellWorld);

                    if (!CanViewerRevealCell(viewerVisionLevel, cellVisionLevel))
                        continue;
                }

                if (faction == RTSFaction.Player1)
                {
                    player1Visible[x, z] = true;
                    player1Explored[x, z] = true;
                }
                else if (faction == RTSFaction.Player2)
                {
                    player2Visible[x, z] = true;
                    player2Explored[x, z] = true;
                }
            }
        }
    }

    public int GetVisionLevelAtWorldPosition(Vector3 worldPosition)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            worldPosition + Vector3.up * groundRaycastHeight,
            Vector3.down,
            groundRaycastDistance
        );

        if (hits.Length == 0)
            return 0;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

            if (ground != null)
            {
                return ground.visionLevel;
            }
        }

        return 0;
    }

    private bool CanViewerRevealCell(int viewerVisionLevel, int cellVisionLevel)
    {
        // Same level can see same level.
        if (viewerVisionLevel == cellVisionLevel)
            return true;

        // Highground can see lowground.
        if (viewerVisionLevel > cellVisionLevel)
            return true;

        // Lowground cannot reveal highground.
        return false;
    }

    private void UpdateFogVisuals()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Renderer renderer = fogRenderers[x, z];

                if (renderer == null)
                    continue;

                bool visible = IsCellVisible(x, z, localViewFaction);
                bool explored = IsCellExplored(x, z, localViewFaction);

                if (visible)
                {
                    renderer.enabled = false;
                }
                else
                {
                    renderer.enabled = true;
                    renderer.sharedMaterial = explored ? exploredMaterial : unexploredMaterial;
                }
            }
        }
    }

    public bool IsPositionVisibleToFaction(Vector3 worldPosition, RTSFaction faction)
    {
        if (!WorldToGrid(worldPosition, out int x, out int z))
            return false;

        return IsCellVisible(x, z, faction);
    }

    public bool IsPositionExploredToFaction(Vector3 worldPosition, RTSFaction faction)
    {
        if (!WorldToGrid(worldPosition, out int x, out int z))
            return false;

        return IsCellExplored(x, z, faction);
    }

    public bool IsUnitVisibleToFaction(RTSUnit target, RTSFaction viewerFaction)
    {
        if (target == null)
            return false;

        return IsPositionVisibleToFaction(target.transform.position, viewerFaction);
    }

    private bool IsCellVisible(int x, int z, RTSFaction faction)
    {
        if (!IsInsideGrid(x, z))
            return false;

        if (faction == RTSFaction.Player1)
            return player1Visible[x, z];

        if (faction == RTSFaction.Player2)
            return player2Visible[x, z];

        return false;
    }

    private bool IsCellExplored(int x, int z, RTSFaction faction)
    {
        if (!IsInsideGrid(x, z))
            return false;

        if (faction == RTSFaction.Player1)
            return player1Explored[x, z];

        if (faction == RTSFaction.Player2)
            return player2Explored[x, z];

        return false;
    }

    private bool WorldToGrid(Vector3 worldPosition, out int x, out int z)
    {
        x = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize);
        z = Mathf.FloorToInt((worldPosition.z - gridOrigin.z) / cellSize);

        return IsInsideGrid(x, z);
    }

    private Vector3 GridToWorldCenter(int x, int z)
    {
        return new Vector3(
            gridOrigin.x + x * cellSize + cellSize * 0.5f,
            0f,
            gridOrigin.z + z * cellSize + cellSize * 0.5f
        );
    }

    private bool IsInsideGrid(int x, int z)
    {
        return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}