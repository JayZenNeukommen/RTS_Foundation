using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(RTSUnit))]
[RequireComponent(typeof(NavMeshAgent))]
public class WorkerBuilder : MonoBehaviour
{
    [Header("Building")]
    public float buildRange = 1.8f;
    public float buildSpeed = 1f;

    [Header("Repair")]
    public float repairRange = 1.8f;
    public float repairSpeed = 15f;

    private RTSUnit unit;
    private NavMeshAgent agent;
    private RTSCombatController combatController;

    private BuildingConstructionController currentConstruction;
    private RTSUnit currentRepairTarget;
    private float repairAccumulator;
    public bool IsBusyBuildingOrRepairing => currentConstruction != null || currentRepairTarget != null;

    private void Awake()
    {
        unit = GetComponent<RTSUnit>();
        agent = GetComponent<NavMeshAgent>();
        combatController = GetComponent<RTSCombatController>();
    }

    private void Update()
    {
        if (currentConstruction != null)
        {
            HandleConstruction();
            return;
        }

        if (currentRepairTarget != null)
        {
            HandleRepair();
            return;
        }
    }

    public void StartBuilding(BuildingConstructionController construction)
    {
        if (!unit.canBuild)
        {
            Debug.LogWarning(name + " cannot build.");
            return;
        }

        if (construction == null)
            return;

        currentConstruction = construction;
        currentRepairTarget = null;
        repairAccumulator = 0f;

        Vector3 buildPoint = currentConstruction.GetBuildPoint(transform.position);
        MoveToPoint(buildPoint);

        Debug.Log(name + " assigned to build " + construction.name);
    }

    public void StartRepair(RTSUnit repairTarget)
    {
        if (!unit.canBuild)
        {
            Debug.LogWarning(name + " cannot repair.");
            return;
        }

        if (repairTarget == null)
            return;

        if (repairTarget.faction != unit.faction)
        {
            Debug.LogWarning(name + " cannot repair enemy target.");
            return;
        }

        if (!repairTarget.IsDamaged)
        {
            Debug.Log(repairTarget.name + " does not need repairs.");
            return;
        }

        currentConstruction = null;
        currentRepairTarget = repairTarget;
        repairAccumulator = 0f;

        Vector3 repairPoint = GetInteractionPoint(currentRepairTarget.transform, transform.position, 1f, 3f);
        MoveToPoint(repairPoint);

        Debug.Log(name + " assigned to repair " + repairTarget.name);
    }

    public void CancelBuilding()
    {
        currentConstruction = null;
        currentRepairTarget = null;
        repairAccumulator = 0f;
    }

    private void HandleConstruction()
    {
        if (currentConstruction == null)
            return;

        if (currentConstruction.IsComplete)
        {
            currentConstruction = null;
            return;
        }

        Vector3 buildPoint = currentConstruction.GetBuildPoint(transform.position);
        float distance = HorizontalDistance(transform.position, buildPoint);

        if (distance > buildRange)
        {
            agent.isStopped = false;
            agent.SetDestination(buildPoint);
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;

        FacePosition(currentConstruction.transform.position);
        currentConstruction.AddBuildProgress(Time.deltaTime * buildSpeed);
    }

    private void HandleRepair()
    {
        if (currentRepairTarget == null || currentRepairTarget.IsDead)
        {
            currentRepairTarget = null;
            repairAccumulator = 0f;
            return;
        }

        if (!currentRepairTarget.IsDamaged)
        {
            Debug.Log(currentRepairTarget.name + " repair complete.");
            currentRepairTarget = null;
            repairAccumulator = 0f;
            return;
        }

        Vector3 repairPoint = GetInteractionPoint(currentRepairTarget.transform, transform.position, 1f, 3f);
        float distance = HorizontalDistance(transform.position, repairPoint);

        if (distance > repairRange)
        {
            agent.isStopped = false;
            agent.SetDestination(repairPoint);
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;

        FacePosition(currentRepairTarget.transform.position);

        repairAccumulator += repairSpeed * Time.deltaTime;
        int repairAmount = Mathf.FloorToInt(repairAccumulator);

        if (repairAmount > 0)
        {
            currentRepairTarget.Repair(repairAmount);
            repairAccumulator -= repairAmount;
        }
    }

    private void MoveToPoint(Vector3 point)
    {
        if (combatController != null)
        {
            combatController.MoveTo(point);
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(point);
        }
    }

    private Vector3 GetInteractionPoint(Transform target, Vector3 fromPosition, float offset, float navMeshSearchRadius)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();

        if (targetCollider == null)
            return target.position;

        Vector3 closestPoint = targetCollider.ClosestPoint(fromPosition);

        Vector3 directionFromCenter = closestPoint - target.position;
        directionFromCenter.y = 0f;

        if (directionFromCenter.sqrMagnitude < 0.001f)
        {
            directionFromCenter = fromPosition - target.position;
            directionFromCenter.y = 0f;
        }

        if (directionFromCenter.sqrMagnitude < 0.001f)
        {
            directionFromCenter = target.forward;
        }

        Vector3 outsidePoint = closestPoint + directionFromCenter.normalized * offset;

        if (NavMesh.SamplePosition(outsidePoint, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return outsidePoint;
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            720f * Time.deltaTime
        );
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}