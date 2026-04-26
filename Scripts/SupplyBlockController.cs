using UnityEngine;
using UnityEngine.AI;

public class SupplyBlockController : MonoBehaviour
{
    [Header("Supply Block State")]
    public bool startsLowered = false;
    public bool IsLowered { get; private set; }

    [Header("Visual Settings")]
    public float loweredHeightMultiplier = 0.25f;
    public float loweredYOffset = -0.35f;

    private NavMeshObstacle obstacle;
    private Vector3 raisedScale;
    private Vector3 raisedPosition;

    private void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();

        raisedScale = transform.localScale;
        raisedPosition = transform.localPosition;

        if (startsLowered)
        {
            Lower();
        }
        else
        {
            Raise();
        }
    }

    public void ToggleLowered()
    {
        if (IsLowered)
        {
            Raise();
        }
        else
        {
            Lower();
        }
    }

    public void Lower()
    {
        IsLowered = true;

        Vector3 loweredScale = raisedScale;
        loweredScale.y = Mathf.Max(0.05f, raisedScale.y * loweredHeightMultiplier);

        transform.localScale = loweredScale;
        transform.localPosition = raisedPosition + new Vector3(0f, loweredYOffset, 0f);

        if (obstacle != null)
        {
            obstacle.enabled = false;
        }

        Debug.Log(name + " lowered. Units may pass through.");
    }

    public void Raise()
    {
        IsLowered = false;

        transform.localScale = raisedScale;
        transform.localPosition = raisedPosition;

        if (obstacle != null)
        {
            obstacle.enabled = true;
        }

        Debug.Log(name + " raised. Units are blocked.");
    }
}