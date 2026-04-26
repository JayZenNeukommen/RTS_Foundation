using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    public IReadOnlyList<SelectableUnit> SelectedUnits => selectedUnits;
    public UnitProducer SelectedProducer => selectedProducer;
    public SelectableBuilding SelectedBuilding => selectedBuilding;
    public bool HasPendingCommand => pendingCommand != PendingCommandType.None;

    [Header("Selection Settings")]
    public float dragThreshold = 10f;

    [Header("Movement Settings")]
    public float formationSpacing = 1.5f;

    private readonly List<SelectableUnit> selectedUnits = new List<SelectableUnit>();

    private SelectableBuilding selectedBuilding;

    private Vector2 dragStartPosition;
    private Vector2 dragEndPosition;
    private bool isDragging;

    private Camera mainCamera;
    private UnitProducer selectedProducer;
    private bool suppressSelectionUntilMouseReleased;

    private enum PendingCommandType
    {
        None,
        AttackMove,
        AttackGround,
        Patrol,
        Repair
    }

    private class ControlGroup
    {
        public List<SelectableUnit> units = new List<SelectableUnit>();
        public SelectableBuilding building;
    }

    private ControlGroup[] controlGroups = new ControlGroup[10];

    private float[] lastControlGroupTapTime = new float[10];
    private float doubleTapTime = 0.35f;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;

        for (int i = 0; i < controlGroups.Length; i++)
        {
            controlGroups[i] = new ControlGroup();
        }
    }

    private void Update()
    {
        if (BuildingPlacementManager.Instance != null && BuildingPlacementManager.Instance.IsPlacing)
            return;

        if (pendingCommand != PendingCommandType.None)
        {
            HandlePendingCommandInput();
            return;
        }

        if (suppressSelectionUntilMouseReleased)
        {
            isDragging = false;

            if (!Input.GetMouseButton(0))
            {
                suppressSelectionUntilMouseReleased = false;
            }

            HandleCommandInput();
            return;
        }

        HandleCommandInput();

        if (pendingCommand != PendingCommandType.None)
            return;

        HandleSelectionInput();
    }

    private PendingCommandType pendingCommand = PendingCommandType.None;

    public string PendingCommandName
    {
        get
        {
            if (pendingCommand == PendingCommandType.AttackMove)
                return "Attack-Move";

            if (pendingCommand == PendingCommandType.AttackGround)
                return "Attack-Ground";

            if (pendingCommand == PendingCommandType.Patrol)
                return "Patrol";
            if (pendingCommand == PendingCommandType.Repair)
                return "Repair";

            return "";
        }
    }

    private void HandleSelectionInput()
    {
        if (pendingCommand != PendingCommandType.None)
            return;

        if (RTSMinimap.Instance != null && RTSMinimap.Instance.IsMouseOverMinimap())
        {
            isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragStartPosition = Input.mousePosition;
            isDragging = false;
        }

        if (Input.GetMouseButton(0))
        {
            dragEndPosition = Input.mousePosition;

            if (Vector2.Distance(dragStartPosition, dragEndPosition) > dragThreshold)
            {
                isDragging = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragEndPosition = Input.mousePosition;

            if (isDragging)
            {
                SelectUnitsInBox();
            }
            else
            {
                TrySingleSelect();
            }

            isDragging = false;
        }
    }

    private void HandleControlGroupInput()
    {
        for (int i = 1; i <= 9; i++)
        {
            KeyCode key = KeyCode.Alpha0 + i;

            if (!Input.GetKeyDown(key))
                continue;

            bool ctrlHeld =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            if (ctrlHeld)
            {
                SaveControlGroup(i);
            }
            else
            {
                RecallControlGroup(i);
            }
        }
    }

    private void SaveControlGroup(int groupNumber)
    {
        if (groupNumber < 1 || groupNumber > 9)
            return;

        ControlGroup group = controlGroups[groupNumber];

        group.units.Clear();
        group.building = null;

        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit == null || unit.Unit == null || unit.Unit.IsDead)
                continue;

            group.units.Add(unit);
        }

        if (selectedBuilding != null &&
            selectedBuilding.Unit != null &&
            !selectedBuilding.Unit.IsDead)
        {
            group.building = selectedBuilding;
        }

        Debug.Log("Saved control group " + groupNumber + " with " + group.units.Count + " units" +
                  (group.building != null ? " and building " + group.building.name : "."));
    }

    private void RecallControlGroup(int groupNumber)
    {
        if (groupNumber < 1 || groupNumber > 9)
            return;

        ControlGroup group = controlGroups[groupNumber];

        CleanupControlGroup(group);

        if (group.units.Count == 0 && group.building == null)
        {
            Debug.Log("Control group " + groupNumber + " is empty.");
            return;
        }

        bool doubleTap = Time.time - lastControlGroupTapTime[groupNumber] <= doubleTapTime;
        lastControlGroupTapTime[groupNumber] = Time.time;

        ClearSelection();
        ClearBuildingSelection();

        if (group.units.Count > 0)
        {
            foreach (SelectableUnit unit in group.units)
            {
                SelectUnit(unit);
            }
        }
        else if (group.building != null)
        {
            SelectBuilding(group.building);
        }

        if (doubleTap)
        {
            FocusCameraOnControlGroup(group);
        }

        Debug.Log("Recalled control group " + groupNumber);
    }

    private void CleanupControlGroup(ControlGroup group)
    {
        if (group == null)
            return;

        for (int i = group.units.Count - 1; i >= 0; i--)
        {
            SelectableUnit unit = group.units[i];

            if (unit == null || unit.Unit == null || unit.Unit.IsDead)
            {
                group.units.RemoveAt(i);
            }
        }

        if (group.building != null)
        {
            if (group.building.Unit == null || group.building.Unit.IsDead)
            {
                group.building = null;
            }
        }
    }

    private void FocusCameraOnControlGroup(ControlGroup group)
    {
        if (RTSCameraController.Instance == null)
            return;

        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (SelectableUnit unit in group.units)
        {
            if (unit == null)
                continue;

            center += unit.transform.position;
            count++;
        }

        if (group.building != null)
        {
            center += group.building.transform.position;
            count++;
        }

        if (count <= 0)
            return;

        center /= count;

        RTSCameraController.Instance.FocusOnWorldPosition(center);
    }

    private void HandleCommandInput()
    {
        bool mouseOverMinimap =
            RTSMinimap.Instance != null &&
            RTSMinimap.Instance.IsMouseOverMinimap();

        if (mouseOverMinimap)
        {
            HandleMinimapCommandInput();

            // Important:
            // Only block world mouse commands while over minimap.
            // Do NOT block keyboard commands like S, H, A, G, P.
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                return;
            }
        }

        HandleControlGroupInput();

        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClickCommand();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            StopSelectedUnits();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            HoldSelectedUnits();
        }

        if (selectedProducer != null)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                selectedProducer.TryQueueProduction(KeyCode.W);
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                selectedProducer.TryQueueProduction(KeyCode.M);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                selectedProducer.TryQueueProduction(KeyCode.R);
            }
        }

        if (selectedUnits.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                pendingCommand = PendingCommandType.AttackMove;

                isDragging = false;
                dragStartPosition = Input.mousePosition;
                dragEndPosition = Input.mousePosition;

                Debug.Log("Attack-Move selected. Left-click ground or enemy.");
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                pendingCommand = PendingCommandType.AttackGround;

                isDragging = false;
                dragStartPosition = Input.mousePosition;
                dragEndPosition = Input.mousePosition;

                Debug.Log("Attack-Ground selected. Left-click ground.");
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                pendingCommand = PendingCommandType.Patrol;

                isDragging = false;
                dragStartPosition = Input.mousePosition;
                dragEndPosition = Input.mousePosition;

                Debug.Log("Patrol selected. Left-click ground.");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                pendingCommand = PendingCommandType.Repair;

                isDragging = false;
                dragStartPosition = Input.mousePosition;
                dragEndPosition = Input.mousePosition;

                Debug.Log("Repair selected. Left-click damaged friendly building.");
            }
        }

        //WORKER BUILD COMMANDS
        if (selectedUnits.Count > 0 && selectedUnits[0].Unit != null && selectedUnits[0].Unit.canBuild)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                BuildingPlacementManager.Instance.TryBeginPlacementByHotkey(selectedUnits[0], KeyCode.Q);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                BuildingPlacementManager.Instance.TryBeginPlacementByHotkey(selectedUnits[0], KeyCode.E);
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                BuildingPlacementManager.Instance.TryBeginPlacementByHotkey(selectedUnits[0], KeyCode.T);
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                BuildingPlacementManager.Instance.TryBeginPlacementByHotkey(selectedUnits[0], KeyCode.B);
            }
        }

        if (selectedBuilding != null && selectedUnits.Count == 0)
        {
            HandleSelectedBuildingHotkeys();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
            ClearBuildingSelection();
        }

    }

    private void HandleSelectedBuildingHotkeys()
    {
        if (selectedBuilding == null)
            return;

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (selectedBuilding.Construction != null &&
                !selectedBuilding.Construction.IsComplete)
            {
                selectedBuilding.Construction.CancelConstruction(0.75f);
                ClearBuildingSelection();
                return;
            }

            if (selectedBuilding.Producer != null)
            {
                bool shiftHeld =
                    Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

                if (shiftHeld)
                {
                    selectedBuilding.Producer.CancelLatestQueuedProduction(1f);
                }
                else
                {
                    selectedBuilding.Producer.CancelCurrentProduction(1f);
                }

                return;
            }
        }

        if (selectedBuilding.Turret != null)
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                selectedBuilding.Turret.StopCommand();
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                selectedBuilding.Turret.HoldPosition();
            }
        }

        if (selectedBuilding.SupplyBlock != null)
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                selectedBuilding.SupplyBlock.ToggleLowered();
            }
        }
    }

    private void HandlePendingCommandInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            pendingCommand = PendingCommandType.None;
            isDragging = false;
            suppressSelectionUntilMouseReleased = true;
            Debug.Log("Pending command cancelled.");
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        if (RTSMinimap.Instance != null &&
            RTSMinimap.Instance.TryGetWorldPositionFromMouse(out Vector3 minimapWorldPoint))
        {
            HandlePendingCommandOnMinimap(minimapWorldPoint);
        }
        else
        {
            if (pendingCommand == PendingCommandType.AttackMove)
            {
                HandleAttackMoveClick();
            }
            else if (pendingCommand == PendingCommandType.AttackGround)
            {
                HandleAttackGroundClick();
            }
            else if (pendingCommand == PendingCommandType.Patrol)
            {
                HandlePatrolClick();
            }
            else if (pendingCommand == PendingCommandType.Repair)
            {
                HandleRepairClick();
            }
        }

        pendingCommand = PendingCommandType.None;

        // Do not let this command-click become a selection click/drag.
        isDragging = false;
        suppressSelectionUntilMouseReleased = true;

        // Important: do NOT set these to Vector2.zero.
        // That is what creates the box from the bottom-left corner.
        dragStartPosition = Input.mousePosition;
        dragEndPosition = Input.mousePosition;
    }

    private void HandleAttackMoveClick()
    {
        if (selectedUnits.Count == 0)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RTSUnit enemyClicked = null;

        Vector3 groundPoint = Vector3.zero;
        bool foundGroundPoint = false;

        RTSUnit firstSelectedUnit = selectedUnits[0].Unit;

        foreach (RaycastHit hit in hits)
        {
            RTSUnit clickedUnit = hit.collider.GetComponentInParent<RTSUnit>();

            if (clickedUnit != null &&
                firstSelectedUnit != null &&
                firstSelectedUnit.IsEnemy(clickedUnit))
            {
                enemyClicked = clickedUnit;
                break;
            }

            if (!foundGroundPoint)
            {
                WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

                if (ground != null)
                {
                    groundPoint = hit.point;
                    foundGroundPoint = true;
                }
            }
        }

        if (enemyClicked != null)
        {
            Debug.Log("Attack command issued through A-click against: " + enemyClicked.unitName);

            foreach (SelectableUnit selectedUnit in selectedUnits)
            {
                if (selectedUnit != null)
                {
                    selectedUnit.AttackTarget(enemyClicked);
                }
            }

            return;
        }

        if (foundGroundPoint)
        {
            Debug.Log("Attack-Move command issued.");

            AttackMoveSelectedUnitsToPoint(groundPoint);
        }
    }

    private void HandleAttackGroundClick()
    {
        if (selectedUnits.Count == 0)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 groundPoint = Vector3.zero;
        bool foundGroundPoint = false;

        foreach (RaycastHit hit in hits)
        {
            WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

            if (ground != null)
            {
                groundPoint = hit.point;
                foundGroundPoint = true;
                break;
            }
        }

        if (!foundGroundPoint)
            return;

        Debug.Log("Attack-Ground command issued.");

        AttackGroundSelectedUnitsAtPoint(groundPoint);
    }

    private void HandlePatrolClick()
    {
        if (selectedUnits.Count == 0)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 groundPoint = Vector3.zero;
        bool foundGroundPoint = false;

        foreach (RaycastHit hit in hits)
        {
            WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

            if (ground != null)
            {
                groundPoint = hit.point;
                foundGroundPoint = true;
                break;
            }
        }

        if (!foundGroundPoint)
            return;

        Debug.Log("Patrol command issued.");

        PatrolSelectedUnitsToPoint(groundPoint);
    }

    private void HandlePendingCommandOnMinimap(Vector3 worldPoint)
    {
        if (selectedUnits.Count == 0)
            return;

        if (pendingCommand == PendingCommandType.AttackMove)
        {
            AttackMoveSelectedUnitsToPoint(worldPoint);
            Debug.Log("Attack-Move command issued through minimap.");
            return;
        }

        if (pendingCommand == PendingCommandType.AttackGround)
        {
            AttackGroundSelectedUnitsAtPoint(worldPoint);
            Debug.Log("Attack-Ground command issued through minimap.");
            return;
        }

        if (pendingCommand == PendingCommandType.Patrol)
        {
            PatrolSelectedUnitsToPoint(worldPoint);
            Debug.Log("Patrol command issued through minimap.");
            return;
        }
    }

    private void AttackGroundSelectedUnitsAtPoint(Vector3 targetPoint)
    {
        foreach (SelectableUnit selectedUnit in selectedUnits)
        {
            if (selectedUnit == null || selectedUnit.Unit == null)
                continue;

            if (!selectedUnit.Unit.canAttackGround)
                continue;

            selectedUnit.AttackGroundAt(targetPoint);
        }
    }

    private void TrySingleSelect()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
            SelectableBuilding building = hit.collider.GetComponentInParent<SelectableBuilding>();
            UnitProducer producer = hit.collider.GetComponentInParent<UnitProducer>();

            ClearSelection();
            ClearBuildingSelection();

            if (unit != null)
            {
                SelectUnit(unit);
                return;
            }

            if (building != null)
            {
                SelectBuilding(building);
                return;
            }

            // Fallback for older producer buildings that do not have SelectableBuilding yet.
            if (producer != null)
            {
                selectedProducer = producer;
                selectedProducer.SetSelected(true);
                return;
            }
        }
    }

    private void SelectUnitsInBox()
    {
        ClearSelection();
        ClearBuildingSelection();

        Rect selectionRect = GetScreenRect(dragStartPosition, dragEndPosition);

        SelectableUnit[] allUnits = Object.FindObjectsByType<SelectableUnit>(FindObjectsInactive.Exclude);

        foreach (SelectableUnit unit in allUnits)
        {
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(unit.transform.position);

            if (screenPosition.z < 0)
                continue;

            Vector2 guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            if (selectionRect.Contains(guiPoint))
            {
                SelectUnit(unit);
            }
        }
    }

    private void SelectBuilding(SelectableBuilding building)
    {
        if (building == null)
            return;

        selectedBuilding = building;
        selectedBuilding.SetSelected(true);

        selectedProducer = building.Producer;

        if (selectedProducer != null)
        {
            Debug.Log(building.name + " selected. Production building.");
        }
        else
        {
            Debug.Log(building.name + " selected.");
        }
    }

    private void ClearBuildingSelection()
    {
        if (selectedBuilding != null)
        {
            selectedBuilding.SetSelected(false);
            selectedBuilding = null;
            selectedProducer = null;
            return;
        }

        if (selectedProducer != null)
        {
            selectedProducer.SetSelected(false);
            selectedProducer = null;
        }
    }

    private void SelectUnit(SelectableUnit unit)
    {
        if (selectedUnits.Contains(unit))
            return;

        selectedUnits.Add(unit);
        unit.SetSelected(true);
    }

    private void ClearSelection()
    {
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.SetSelected(false);
            }
        }

        selectedUnits.Clear();
    }

    private void HandleMinimapCommandInput()
    {
        if (RTSMinimap.Instance == null)
            return;

        if (!RTSMinimap.Instance.TryGetWorldPositionFromMouse(out Vector3 worldPoint))
            return;

        // Left-click minimap with no pending command = camera pan.
        if (Input.GetMouseButtonDown(0))
        {
            if (RTSCameraController.Instance != null)
            {
                RTSCameraController.Instance.FocusOnWorldPosition(worldPoint);
            }

            suppressSelectionUntilMouseReleased = true;
            isDragging = false;
            return;
        }

        // Right-click minimap = normal RTS command.
        if (Input.GetMouseButtonDown(1))
        {
            if (selectedProducer != null && selectedUnits.Count == 0)
            {
                selectedProducer.SetRallyPoint(worldPoint);
                Debug.Log("Producer rally point set through minimap.");
                return;
            }

            if (selectedUnits.Count > 0)
            {
                MoveSelectedUnitsToPoint(worldPoint);
                Debug.Log("Move command issued through minimap.");
                return;
            }
        }
    }

    private void HandleRightClickCommand()
    {
        if (selectedProducer != null && selectedUnits.Count == 0)
        {
            HandleProducerRallyCommand();
            return;
        }

        if (selectedBuilding != null && selectedUnits.Count == 0)
        {
            HandleSelectedBuildingRightClickCommand();
            return;
        }

        if (selectedUnits.Count == 0)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RTSUnit enemyClicked = null;
        MineralNode mineralClicked = null;
        ResourceDepot depotClicked = null;
        SelectableBuilding friendlyBuildingClicked = null;

        Vector3 groundPoint = Vector3.zero;
        bool foundGroundPoint = false;

        RTSUnit firstSelectedUnit = selectedUnits[0].Unit;

        foreach (RaycastHit hit in hits)
        {
            RTSUnit clickedUnit = hit.collider.GetComponentInParent<RTSUnit>();

            if (clickedUnit != null && firstSelectedUnit != null && firstSelectedUnit.IsEnemy(clickedUnit))
            {
                enemyClicked = clickedUnit;
                break;
            }

            if (friendlyBuildingClicked == null)
            {
                SelectableBuilding building = hit.collider.GetComponentInParent<SelectableBuilding>();

                if (building != null &&
                    building.Unit != null &&
                    firstSelectedUnit != null &&
                    building.Unit.faction == firstSelectedUnit.faction)
                {
                    friendlyBuildingClicked = building;
                }
            }

            if (mineralClicked == null)
            {
                MineralNode mineral = hit.collider.GetComponentInParent<MineralNode>();

                if (mineral != null)
                {
                    mineralClicked = mineral;
                }
            }

            if (depotClicked != null)
            {
                bool anyCargoWorkerReturned = false;

                foreach (SelectableUnit selectedUnit in selectedUnits)
                {
                    if (selectedUnit == null || selectedUnit.Unit == null)
                        continue;

                    if (!selectedUnit.Unit.canGather)
                        continue;

                    if (selectedUnit.Gatherer != null && selectedUnit.Gatherer.HasCarriedResources)
                    {
                        selectedUnit.ReturnResources(depotClicked);
                        anyCargoWorkerReturned = true;
                    }
                }

                if (anyCargoWorkerReturned)
                {
                    Debug.Log("Return resource command issued against: " + depotClicked.name);
                    return;
                }
            }

            if (friendlyBuildingClicked != null)
            {
                bool anyWorkerHandledBuilding = false;

                foreach (SelectableUnit selectedUnit in selectedUnits)
                {
                    if (selectedUnit == null || selectedUnit.Unit == null)
                        continue;

                    if (selectedUnit.Unit.canBuild)
                    {
                        bool needsConstruction =
                            friendlyBuildingClicked.Construction != null &&
                            !friendlyBuildingClicked.Construction.IsComplete;

                        bool needsRepair =
                            friendlyBuildingClicked.Unit != null &&
                            friendlyBuildingClicked.Unit.IsDamaged;

                        if (needsConstruction || needsRepair)
                        {
                            selectedUnit.BuildOrRepair(friendlyBuildingClicked);
                            anyWorkerHandledBuilding = true;
                        }
                    }
                }

                if (anyWorkerHandledBuilding)
                {
                    Debug.Log("Build/repair command issued against: " + friendlyBuildingClicked.name);
                    return;
                }
            }

            if (depotClicked == null)
            {
                ResourceDepot depot = hit.collider.GetComponentInParent<ResourceDepot>();

                if (depot != null &&
                    depot.enabled &&
                    depot.isOperational &&
                    firstSelectedUnit != null &&
                    depot.faction == firstSelectedUnit.faction)
                {
                    RTSUnit depotUnit = depot.GetComponent<RTSUnit>();

                    if (depotUnit != null && depotUnit.isTargetable)
                    {
                        depotClicked = depot;
                    }
                }
            }

            if (depotClicked != null)
            {
                Debug.Log("Return/depot command issued against: " + depotClicked.name);

                foreach (SelectableUnit selectedUnit in selectedUnits)
                {
                    if (selectedUnit == null || selectedUnit.Unit == null)
                        continue;

                    if (selectedUnit.Unit.canGather)
                    {
                        selectedUnit.ReturnResources(depotClicked);
                    }
                    else
                    {
                        selectedUnit.MoveTo(depotClicked.GetNearestDepositPoint(selectedUnit.transform.position));
                    }
                }

                return;
            }

            if (!foundGroundPoint)
            {
                WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

                if (ground != null)
                {
                    groundPoint = hit.point;
                    foundGroundPoint = true;
                }
            }
        }

        if (enemyClicked != null)
        {
            Debug.Log("Attack command issued against: " + enemyClicked.unitName);

            foreach (SelectableUnit selectedUnit in selectedUnits)
            {
                selectedUnit.AttackTarget(enemyClicked);
            }

            return;
        }

        if (mineralClicked != null)
        {
            Debug.Log("Gather command issued against: " + mineralClicked.name);

            foreach (SelectableUnit selectedUnit in selectedUnits)
            {
                if (selectedUnit.Unit != null && selectedUnit.Unit.canGather)
                {
                    selectedUnit.Gather(mineralClicked);
                }
            }

            return;
        }

        Debug.Log("Move command issued.");

        if (foundGroundPoint)
        {
            MoveSelectedUnitsToPoint(groundPoint);
        }
    }

    private void HandleSelectedBuildingRightClickCommand()
    {
        if (selectedBuilding == null)
            return;

        if (selectedBuilding.Turret == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RTSUnit ownUnit = selectedBuilding.Unit;

        foreach (RaycastHit hit in hits)
        {
            RTSUnit clickedUnit = hit.collider.GetComponentInParent<RTSUnit>();

            if (clickedUnit == null || ownUnit == null)
                continue;

            if (ownUnit.IsEnemy(clickedUnit))
            {
                selectedBuilding.Turret.AttackTarget(clickedUnit);
                return;
            }
        }
    }

    private void HandleProducerRallyCommand()
    {
        if (selectedProducer == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        MineralNode mineralClicked = null;
        RTSUnit enemyClicked = null;

        Vector3 groundPoint = Vector3.zero;
        bool foundGroundPoint = false;

        foreach (RaycastHit hit in hits)
        {
            if (mineralClicked == null)
            {
                MineralNode mineral = hit.collider.GetComponentInParent<MineralNode>();

                if (mineral != null)
                {
                    mineralClicked = mineral;
                }
            }

            if (enemyClicked == null)
            {
                RTSUnit clickedUnit = hit.collider.GetComponentInParent<RTSUnit>();

                if (clickedUnit != null &&
                    clickedUnit.isTargetable &&
                    clickedUnit.faction != RTSFaction.Neutral &&
                    clickedUnit.faction != selectedProducer.faction)
                {
                    enemyClicked = clickedUnit;
                }
            }

            if (!foundGroundPoint)
            {
                WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

                if (ground != null)
                {
                    groundPoint = hit.point;
                    foundGroundPoint = true;
                }
            }
        }

        if (mineralClicked != null)
        {
            selectedProducer.SetRallyMineral(mineralClicked);
            return;
        }

        if (enemyClicked != null)
        {
            selectedProducer.SetRallyAttackTarget(enemyClicked);
            return;
        }

        if (foundGroundPoint)
        {
            selectedProducer.SetRallyPoint(groundPoint);
        }
    }

    private void HandleRepairClick()
    {
        if (selectedUnits.Count == 0)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RTSUnit firstSelectedUnit = selectedUnits[0].Unit;
        SelectableBuilding repairTarget = null;

        foreach (RaycastHit hit in hits)
        {
            SelectableBuilding building = hit.collider.GetComponentInParent<SelectableBuilding>();

            if (building == null || building.Unit == null || firstSelectedUnit == null)
                continue;

            if (building.Unit.faction != firstSelectedUnit.faction)
                continue;

            if (!building.Unit.IsDamaged)
                continue;

            repairTarget = building;
            break;
        }

        if (repairTarget == null)
        {
            Debug.Log("No damaged friendly building selected for repair.");
            return;
        }

        foreach (SelectableUnit selectedUnit in selectedUnits)
        {
            if (selectedUnit == null || selectedUnit.Unit == null)
                continue;

            if (!selectedUnit.Unit.canBuild)
                continue;

            selectedUnit.RepairBuilding(repairTarget);
        }

        Debug.Log("Repair command issued against: " + repairTarget.name);
    }

    private void MoveSelectedUnitsToPoint(Vector3 targetPoint)
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Vector3 offset = GetFormationOffset(i, selectedUnits.Count);
            Vector3 destination = targetPoint + offset;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                selectedUnits[i].MoveTo(navHit.position);
            }
        }
    }

    private void AttackMoveSelectedUnitsToPoint(Vector3 targetPoint)
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Vector3 offset = GetFormationOffset(i, selectedUnits.Count);
            Vector3 destination = targetPoint + offset;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                selectedUnits[i].AttackMoveTo(navHit.position);
            }
        }
    }

    private void PatrolSelectedUnitsToPoint(Vector3 targetPoint)
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Vector3 offset = GetFormationOffset(i, selectedUnits.Count);
            Vector3 destination = targetPoint + offset;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                selectedUnits[i].PatrolTo(navHit.position);
            }
        }
    }

    private Vector3 GetFormationOffset(int index, int totalUnits)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(totalUnits));

        int row = index / columns;
        int column = index % columns;

        float xOffset = (column - (columns - 1) / 2f) * formationSpacing;
        float zOffset = (row - (columns - 1) / 2f) * formationSpacing;

        return new Vector3(xOffset, 0f, zOffset);
    }

    private void HoldSelectedUnits()
    {
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.HoldPosition();
            }
        }
    }

    private void StopSelectedUnits()
    {
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.Stop();
            }
        }
    }

    private Rect GetScreenRect(Vector2 screenPosition1, Vector2 screenPosition2)
    {
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;

        Vector2 topLeft = Vector2.Min(screenPosition1, screenPosition2);
        Vector2 bottomRight = Vector2.Max(screenPosition1, screenPosition2);

        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }

    private void OnGUI()
    {
        if (pendingCommand != PendingCommandType.None)
            return;

        if (suppressSelectionUntilMouseReleased)
            return;

        if (!isDragging)
            return;

        Rect rect = GetScreenRect(dragStartPosition, Input.mousePosition);

        GUI.color = new Color(0f, 1f, 0f, 0.2f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.green;
        DrawScreenRectBorder(rect, 2f);

        GUI.color = Color.white;
    }

    private void DrawScreenRectBorder(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
    }
}