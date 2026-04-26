using UnityEngine;
using System.Collections.Generic;

public class RTSMinimap : MonoBehaviour
{
    public static RTSMinimap Instance { get; private set; }

    [Header("Minimap")]
    public RTSFaction localFaction = RTSFaction.Player1;
    public int minimapSize = 220;
    public int margin = 10;

    [Header("Map Bounds")]
    public bool useFogManagerBounds = true;
    public Vector2 mapOriginXZ = new Vector2(-25f, -25f);
    public Vector2 mapSizeXZ = new Vector2(50f, 50f);

    [Header("Fog Sampling")]
    public int fogSampleResolution = 36;

    [Header("Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0.9f);
    public Color exploredColor = new Color(0.18f, 0.18f, 0.18f, 0.85f);
    public Color visibleColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);

    public Color player1Color = Color.blue;
    public Color player2Color = Color.red;
    public Color neutralColor = Color.gray;
    public Color cameraColor = Color.white;
    public Color borderColor = Color.white;

    private Texture2D whiteTexture;

    private void Awake()
    {
        Instance = this;
        whiteTexture = Texture2D.whiteTexture;
    }

    private void OnGUI()
    {
        UpdateMapBoundsFromFogManager();

        Rect rect = GetMinimapRect();

        DrawMinimapBackground(rect);
        DrawFogLayer(rect);
        DrawUnitsAndBuildings(rect);
        DrawCameraMarker(rect);
        DrawBorder(rect);
        //HandleMinimapClick(rect);

        GUI.color = Color.white;
    }

    public Rect GetMinimapRect()
    {
        //float bottomHudHeight = 250f;

        return new Rect(
            margin,
            Screen.height - minimapSize - margin,
            minimapSize,
            minimapSize
        );
    }

    public bool IsMouseOverMinimap()
    {
        Rect rect = GetMinimapRect();

        Vector2 guiMouse = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y
        );

        return rect.Contains(guiMouse);
    }

    private void UpdateMapBoundsFromFogManager()
    {
        if (!useFogManagerBounds)
            return;

        if (FogOfWarManager.Instance == null)
            return;

        mapOriginXZ = new Vector2(
            FogOfWarManager.Instance.gridOrigin.x,
            FogOfWarManager.Instance.gridOrigin.z
        );

        mapSizeXZ = new Vector2(
            FogOfWarManager.Instance.gridWidth * FogOfWarManager.Instance.cellSize,
            FogOfWarManager.Instance.gridHeight * FogOfWarManager.Instance.cellSize
        );
    }

    private void DrawMinimapBackground(Rect rect)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(rect, whiteTexture);
    }

    private void DrawFogLayer(Rect rect)
    {
        if (FogOfWarManager.Instance == null)
            return;

        int resolution = Mathf.Max(4, fogSampleResolution);
        float cellWidth = rect.width / resolution;
        float cellHeight = rect.height / resolution;

        for (int x = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++)
            {
                float worldX = mapOriginXZ.x + ((x + 0.5f) / resolution) * mapSizeXZ.x;
                float worldZ = mapOriginXZ.y + ((z + 0.5f) / resolution) * mapSizeXZ.y;

                Vector3 worldPoint = new Vector3(worldX, 0f, worldZ);

                bool visible = FogOfWarManager.Instance.IsPositionVisibleToFaction(worldPoint, localFaction);
                bool explored = FogOfWarManager.Instance.IsPositionExploredToFaction(worldPoint, localFaction);

                if (visible)
                    GUI.color = visibleColor;
                else if (explored)
                    GUI.color = exploredColor;
                else
                    GUI.color = unexploredColor;

                Rect cellRect = new Rect(
                    rect.x + x * cellWidth,
                    rect.y + rect.height - (z + 1) * cellHeight,
                    cellWidth + 1f,
                    cellHeight + 1f
                );

                GUI.DrawTexture(cellRect, whiteTexture);
            }
        }
    }

    private void DrawUnitsAndBuildings(Rect rect)
    {
        IReadOnlyList<RTSUnit> units = RTSUnit.AllUnits;

        foreach (RTSUnit unit in units)
        {
            if (unit == null || unit.IsDead || !unit.isTargetable)
                continue;

            if (!ShouldDrawUnit(unit))
                continue;

            Vector2 point = WorldToMinimapPoint(unit.transform.position, rect);

            bool isBuilding = unit.GetComponent<SelectableBuilding>() != null;

            float size = isBuilding ? 6f : 4f;

            GUI.color = GetFactionColor(unit.faction);

            Rect dotRect = new Rect(
                point.x - size * 0.5f,
                point.y - size * 0.5f,
                size,
                size
            );

            GUI.DrawTexture(dotRect, whiteTexture);
        }
    }

    private bool ShouldDrawUnit(RTSUnit unit)
    {
        if (unit.faction == localFaction)
            return true;

        if (unit.faction == RTSFaction.Neutral)
            return false;

        if (FogOfWarManager.Instance == null)
            return true;

        return FogOfWarManager.Instance.IsUnitVisibleToFaction(unit, localFaction) ||
               FogOfWarManager.Instance.IsUnitTemporarilyRevealed(unit);
    }

    private Color GetFactionColor(RTSFaction faction)
    {
        switch (faction)
        {
            case RTSFaction.Player1:
                return player1Color;

            case RTSFaction.Player2:
                return player2Color;

            default:
                return neutralColor;
        }
    }

    private void DrawCameraMarker(Rect rect)
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        Vector2 point = WorldToMinimapPoint(camera.transform.position, rect);

        GUI.color = cameraColor;

        float size = 8f;

        GUI.DrawTexture(
            new Rect(point.x - size * 0.5f, point.y - 1f, size, 2f),
            whiteTexture
        );

        GUI.DrawTexture(
            new Rect(point.x - 1f, point.y - size * 0.5f, 2f, size),
            whiteTexture
        );
    }

    private void DrawBorder(Rect rect)
    {
        GUI.color = borderColor;

        float thickness = 2f;

        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), whiteTexture);
    }

    private void HandleMinimapClick(Rect rect)
    {
        Event currentEvent = Event.current;

        if (currentEvent == null)
            return;

        if (currentEvent.type != EventType.MouseDown)
            return;

        if (currentEvent.button != 0)
            return;

        if (!rect.Contains(currentEvent.mousePosition))
            return;

        if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.HasPendingCommand)
        {
            currentEvent.Use();
            return;
        }

        Vector3 worldPoint = MinimapPointToWorld(currentEvent.mousePosition, rect);

        if (RTSCameraController.Instance != null)
        {
            RTSCameraController.Instance.FocusOnWorldPosition(worldPoint);
        }
        else if (Camera.main != null)
        {
            Vector3 cameraPosition = Camera.main.transform.position;
            cameraPosition.x = worldPoint.x;
            cameraPosition.z = worldPoint.z;
            Camera.main.transform.position = cameraPosition;
        }

        currentEvent.Use();
    }

    private Vector2 WorldToMinimapPoint(Vector3 worldPosition, Rect rect)
    {
        float normalizedX = Mathf.InverseLerp(
            mapOriginXZ.x,
            mapOriginXZ.x + mapSizeXZ.x,
            worldPosition.x
        );

        float normalizedZ = Mathf.InverseLerp(
            mapOriginXZ.y,
            mapOriginXZ.y + mapSizeXZ.y,
            worldPosition.z
        );

        float x = rect.x + normalizedX * rect.width;
        float y = rect.y + rect.height - normalizedZ * rect.height;

        return new Vector2(x, y);
    }

    public Vector3 MinimapPointToWorld(Vector2 minimapPoint, Rect rect)
    {
        float normalizedX = Mathf.InverseLerp(rect.x, rect.xMax, minimapPoint.x);
        float normalizedZ = 1f - Mathf.InverseLerp(rect.y, rect.yMax, minimapPoint.y);

        float worldX = mapOriginXZ.x + normalizedX * mapSizeXZ.x;
        float worldZ = mapOriginXZ.y + normalizedZ * mapSizeXZ.y;

        return new Vector3(worldX, 0f, worldZ);
    }

    public bool TryGetWorldPositionFromMouse(out Vector3 worldPoint)
    {
        Rect rect = GetMinimapRect();

        Vector2 guiMouse = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y
        );

        if (!rect.Contains(guiMouse))
        {
            worldPoint = Vector3.zero;
            return false;
        }

        worldPoint = MinimapPointToWorld(guiMouse, rect);
        return true;
    }
}