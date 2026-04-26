using UnityEngine;
using UnityEngine.AI;

public class UnitClickMover : MonoBehaviour
{
    private NavMeshAgent agent;
    private bool selected;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelect();
        }

        if (selected && Input.GetMouseButtonDown(1))
        {
            MoveToMousePoint();
        }
    }

    void TrySelect()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            selected = hit.transform == transform;
            Debug.Log(selected ? "Selected " + name : "Deselected " + name);
        }
    }

    void MoveToMousePoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            agent.SetDestination(hit.point);
        }
    }
}