using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class ResourceDepot : MonoBehaviour
{
    private static readonly List<ResourceDepot> allDepots = new List<ResourceDepot>();
    public static IReadOnlyList<ResourceDepot> AllDepots => allDepots;

    public RTSFaction faction = RTSFaction.Player1;
    public bool isOperational = true;

    [Header("Deposit Point Settings")]
    public float depositOffset = 0.75f;
    public float navMeshSearchRadius = 3f;

    private Collider depotCollider;

    private void Awake()
    {
        depotCollider = GetComponentInChildren<Collider>();
    }

    private void OnEnable()
    {
        if (!allDepots.Contains(this))
        {
            allDepots.Add(this);
        }
    }

    private void OnDisable()
    {
        allDepots.Remove(this);
    }

    public Vector3 GetNearestDepositPoint(Vector3 fromPosition)
    {
        if (depotCollider == null)
            return transform.position;

        Vector3 closestPoint = depotCollider.ClosestPoint(fromPosition);

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

        Vector3 outsidePoint = closestPoint + directionFromCenter.normalized * depositOffset;

        if (NavMesh.SamplePosition(outsidePoint, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return outsidePoint;
    }
}