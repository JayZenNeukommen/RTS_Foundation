using System.Collections.Generic;
using UnityEngine;

public class RTSHUD : MonoBehaviour
{
    [Header("HUD Settings")]
    public RTSFaction localFaction = RTSFaction.Player1;
    public int panelWidth = 360;
    public int lineHeight = 24;

    [Header("Layout")]
    public int bottomHudHeight = 250;
    public int minimapReservedWidth = 240;
    public int selectionPanelWidth = 400;
    public int selectionPanelHeight = 250;
    public int commandPanelWidth = 420;
    public int commandPanelHeight = 420;
    public int panelMargin = 10;

    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle warningStyle;

    private void EnsureStyles()
    {
        if (boxStyle != null)
            return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.fontSize = 16;
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.fontSize = 16;
        warningStyle.normal.textColor = Color.yellow;

        boxStyle.fontSize = 15;

        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;

        labelStyle.fontSize = 15;
        warningStyle.fontSize = 15;
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawBottomHudBackground();
        DrawResourcePanel();
        DrawSelectionPanel();
        DrawCommandPanel();
    }

    private void DrawResourcePanel()
    {
        int minerals = 0;
        string supplyText = "Supply: ? / ?";

        if (ResourceManager.Instance != null)
        {
            minerals = ResourceManager.Instance.GetMinerals(localFaction);
            supplyText = ResourceManager.Instance.GetSupplyText(localFaction);
        }

        Rect rect = new Rect(Screen.width - 330, 10, 320, 80);
        GUI.Box(rect, "", boxStyle);

        GUI.Label(new Rect(rect.x + 10, rect.y + 10, 300, lineHeight), "Minerals: " + minerals, titleStyle);
        GUI.Label(new Rect(rect.x + 10, rect.y + 38, 300, lineHeight), supplyText, labelStyle);
    }

    private void DrawSelectionPanel()
    {
        UnitSelectionManager selection = UnitSelectionManager.Instance;

        if (selection == null)
            return;

        IReadOnlyList<SelectableUnit> units = selection.SelectedUnits;
        UnitProducer producer = selection.SelectedProducer;
        SelectableBuilding building = selection.SelectedBuilding;

        float selectionX = minimapReservedWidth + panelMargin;
        float selectionY = Screen.height - selectionPanelHeight - panelMargin;

        Rect rect = new Rect(selectionX, selectionY, selectionPanelWidth, selectionPanelHeight);
        GUI.Box(rect, "", boxStyle);

        float y = selectionY + 10;

        GUI.Label(new Rect(20, y, selectionPanelWidth - 20, lineHeight), "Selection", titleStyle);
        y += 30;

        if (units != null && units.Count > 0)
        {
            DrawUnitSelectionInfo(units, selectionX, ref y);
            return;
        }

        if (building != null)
        {
            DrawBuildingSelectionInfo(building, selectionX, ref y);
            return;
        }

        if (producer != null)
        {
            DrawProducerSelectionInfo(producer, selectionX, ref y);
            return;
        }

        GUI.Label(new Rect(20, y, selectionPanelWidth - 20, lineHeight), "Nothing selected.", labelStyle);
    }

    private void DrawUnitSelectionInfo(IReadOnlyList<SelectableUnit> units, float panelX, ref float y)
    {
        if (units.Count == 1)
        {
            SelectableUnit selectable = units[0];

            if (selectable == null || selectable.Unit == null)
            {
                GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Invalid unit selected.", warningStyle);
                return;
            }

            RTSUnit unit = selectable.Unit;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), unit.unitName, titleStyle);
            y += 28;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Faction: " + unit.faction, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "HP: " + unit.currentHp + " / " + unit.maxHp, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Damage: " + unit.attackDamage + " | Range: " + unit.attackRange, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Sight: " + unit.sightRange + " | Supply: " + unit.supplyCost, labelStyle);
            y += lineHeight;

            if (unit.canGather || unit.canBuild)
            {
                string workerText = "Worker: ";

                if (unit.canGather)
                    workerText += "Gather ";

                if (unit.canBuild)
                    workerText += "Build";

                GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), workerText, labelStyle);
                y += lineHeight;

                WorkerGatherer gatherer = selectable.GetComponent<WorkerGatherer>();

                if (gatherer != null)
                {
                    GUI.Label(
                        new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight),
                        "Carrying Minerals: " + gatherer.carriedMinerals + " / " + gatherer.carryCapacity,
                        labelStyle
                    );

                    y += lineHeight;
                }
            }
        }
        else
        {
            int workers = 0;
            int combatUnits = 0;
            int totalHp = 0;
            int maxHp = 0;

            foreach (SelectableUnit selectable in units)
            {
                if (selectable == null || selectable.Unit == null)
                    continue;

                RTSUnit unit = selectable.Unit;

                totalHp += unit.currentHp;
                maxHp += unit.maxHp;

                if (unit.canGather || unit.canBuild)
                    workers++;
                else
                    combatUnits++;
            }

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), units.Count + " units selected", titleStyle);
            y += 30;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Workers: " + workers, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Combat units: " + combatUnits, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Total HP: " + totalHp + " / " + maxHp, labelStyle);
        }
    }

    private void DrawBuildingSelectionInfo(SelectableBuilding building, float panelX, ref float y)
    {
        if (building == null)
        {
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Invalid building selected.", warningStyle);
            return;
        }

        RTSUnit unit = building.Unit;
        UnitProducer producer = building.Producer;
        SupplyProvider supply = building.Supply;

        string buildingName = unit != null ? unit.unitName : building.name;

        GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), buildingName, titleStyle);
        y += 30;

        if (unit != null)
        {
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Faction: " + unit.faction, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "HP: " + unit.currentHp + " / " + unit.maxHp, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Sight: " + unit.sightRange, labelStyle);
            y += lineHeight;

            if (unit.canAttack)
            {
                GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Damage: " + unit.attackDamage + " | Range: " + unit.attackRange, labelStyle);
                y += lineHeight;
            }
        }

        if (building.Construction != null && !building.Construction.IsComplete)
        {
            int percent = Mathf.RoundToInt(building.Construction.BuildProgress01 * 100f);
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Constructing: " + percent + "%", labelStyle);
            y += lineHeight;
        }

        if (supply != null)
        {
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Provides Supply: " + supply.supplyProvided, labelStyle);
            y += lineHeight;
        }

        if (building.Turret != null)
        {
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Turret State: " + building.Turret.CurrentState, labelStyle);
            y += lineHeight;
        }

        if (building.SupplyBlock != null)
        {
            string stateText = building.SupplyBlock.IsLowered ? "Lowered" : "Raised";
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Supply Block: " + stateText, labelStyle);
            y += lineHeight;
        }

        if (producer != null)
        {
            if (producer.IsProducing)
            {
                int percent = Mathf.RoundToInt(producer.ProductionProgress01 * 100f);

                GUI.Label(
                    new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight),
                    "Training: " + producer.CurrentProductionName + " " + percent + "%",
                    labelStyle
                );

                y += lineHeight;
            }
            else
            {
                GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Training: Idle", labelStyle);
                y += lineHeight;
            }

            string[] queuedNames = producer.GetQueuedProductionNames();

            GUI.Label(
                new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight),
                "Queued: " + queuedNames.Length,
                labelStyle
            );

            y += lineHeight;

            for (int i = 0; i < queuedNames.Length; i++)
            {
                GUI.Label(
                    new Rect(panelX + 25, y, selectionPanelWidth - 35, lineHeight),
                    (i + 1) + ". " + queuedNames[i],
                    labelStyle
                );

                y += lineHeight;
            }

            if (producer.productionOptions != null && producer.productionOptions.Count > 0)
            {
                y += 4;
                GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Can Produce:", titleStyle);
                y += lineHeight;

                foreach (ProductionOption option in producer.productionOptions)
                {
                    if (option == null)
                        continue;

                    string text = option.hotkey + ": " + option.displayName + " (" + option.mineralCost + ")";

                    GUI.Label(
                        new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight),
                        text,
                        labelStyle
                    );

                    y += lineHeight;
                }
            }
        }
    }

    private void DrawProducerSelectionInfo(UnitProducer producer, float panelX, ref float y)
    {
        GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), producer.name, titleStyle);
        y += 30;

        GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Faction: " + producer.faction, labelStyle);
        y += lineHeight;

        if (producer.IsProducing)
        {
            int percent = Mathf.RoundToInt(producer.ProductionProgress01 * 100f);
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Training: " + producer.CurrentProductionName + " " + percent + "%", labelStyle);
            y += lineHeight;

            Rect barBack = new Rect(20, y + 4, panelWidth - 40, 16);
            Rect barFill = new Rect(20, y + 4, (panelWidth - 40) * producer.ProductionProgress01, 16);

            GUI.Box(barBack, "");
            GUI.Box(barFill, "");
            y += 26;
        }
        else
        {
            GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Training: Idle", labelStyle);
            y += lineHeight;
        }

        GUI.Label(new Rect(panelX + 10, y, selectionPanelWidth - 20, lineHeight), "Queue: " + producer.QueueCount, labelStyle);
    }

    private void DrawCommandPanel()
    {
        UnitSelectionManager selection = UnitSelectionManager.Instance;

        if (selection == null)
            return;

        IReadOnlyList<SelectableUnit> units = selection.SelectedUnits;
        UnitProducer producer = selection.SelectedProducer;
        SelectableBuilding building = selection.SelectedBuilding;

        if (producer == null && building != null)
        {
            producer = building.Producer;
        }

        float commandX = Screen.width - commandPanelWidth - panelMargin;
        float commandY = Screen.height - commandPanelHeight - panelMargin;

        Rect rect = new Rect(commandX, commandY, commandPanelWidth, commandPanelHeight);
        GUI.Box(rect, "", boxStyle);

        float y = commandY + 10;

        GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Commands", titleStyle);
        y += 30;

        if (building != null &&
            building.Construction != null &&
            !building.Construction.IsComplete)
        {
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Under Construction", titleStyle);
            y += 30;

            int percent = Mathf.RoundToInt(building.Construction.BuildProgress01 * 100f);
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Progress: " + percent + "%", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Building is not operational yet.", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "X: Cancel construction", labelStyle);
            y += lineHeight;

            return;
        }

        if (units != null && units.Count > 0)
        {
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click ground: Move", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click minimap: Move", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click enemy: Attack", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "A + Left-click: Attack-Move", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "G + Left-click: Attack Ground", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "P + Left-click: Patrol", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "R + Left-click: Repair", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "S: Stop", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "H: Hold Position", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Ctrl + 1-9: Save group", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "1-9: Select group", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Double 1-9: Jump to group", labelStyle);
            y += lineHeight;

            SelectableUnit first = units[0];

            if (first != null && first.Unit != null && first.Unit.canBuild)
            {
                y += 8;
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Worker Build:", titleStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Q: Supply Block", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "E: Barracks", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "T: Turret", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "B: Main Base", labelStyle);
            }

            return;
        }

        if (building != null && producer == null)
        {
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Selected building.", labelStyle);
            y += lineHeight;

            if (building.Turret != null)
            {
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click enemy: Focus fire", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "S: Stop firing", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "H: Resume auto-fire", labelStyle);
                y += lineHeight;
            }

            if (building.SupplyBlock != null)
            {
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "L: Lower / Raise", labelStyle);
                y += lineHeight;
            }

            if (building.Turret == null && building.SupplyBlock == null)
            {
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "No active commands yet.", labelStyle);
            }

            return;
        }

        //PRODUCER BUILDINGS COMMANDS
        if (producer != null)
        {
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Producer Building", titleStyle);
            y += 30;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click ground: Set rally point", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click minimap: Set rally point", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click mineral: Worker rally mine", labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Right-click enemy: Attack rally", labelStyle);
            y += lineHeight;

            y += 8;
            GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Production:", titleStyle);
            y += lineHeight;

            if (producer.productionOptions == null || producer.productionOptions.Count == 0)
            {
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "No production options.", warningStyle);
                y += lineHeight;
            }
            else
            {
                foreach (ProductionOption option in producer.productionOptions)
                {
                    if (option == null)
                        continue;

                    string text =
                        option.hotkey +
                        ": " +
                        option.displayName +
                        " | " +
                        option.mineralCost +
                        " minerals | " +
                        option.buildTime +
                        "s";

                    GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), text, labelStyle);
                    y += lineHeight;
                }
            }

            if (producer.IsProducing)
            {
                y += 8;
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "X: Cancel current production", labelStyle);
                y += lineHeight;
            }

            if (producer.QueueCount > 0)
            {
                GUI.Label(new Rect(commandX + 10, y, commandPanelWidth - 20, lineHeight), "Shift + X: Cancel latest queued", labelStyle);
                y += lineHeight;
            }

            return;
        }
    }

    private void DrawBottomHudBackground()
    {
        Rect bottomRect = new Rect(
            0,
            Screen.height - bottomHudHeight,
            Screen.width,
            bottomHudHeight
        );

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(bottomRect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}