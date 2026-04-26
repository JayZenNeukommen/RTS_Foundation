using UnityEngine;

public class WalkableGround : MonoBehaviour
{
    [Header("Ground Rules")]
    public bool allowBuilding = true;

    [Header("Vision Height")]
    public int visionLevel = 0;
    public bool isRamp = false;
}