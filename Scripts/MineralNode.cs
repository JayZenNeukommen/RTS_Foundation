using UnityEngine;
using System.Collections.Generic;

public class MineralNode : MonoBehaviour
{
    private static readonly List<MineralNode> allMineralNodes = new List<MineralNode>();
    public static IReadOnlyList<MineralNode> AllMineralNodes => allMineralNodes;

    [Header("Mineral Settings")]
    public int maxAmount = 1500;
    public int currentAmount = 1500;

    [Header("Visual")]
    public bool shrinkAsDepleted = true;
    public bool hideWhenEmpty = false;
    public float minimumVisualScale = 0.25f;

    public bool IsEmpty => currentAmount <= 0;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;

        if (currentAmount <= 0)
        {
            currentAmount = maxAmount;
        }

        UpdateVisual();
    }

    private void OnEnable()
    {
        if (!allMineralNodes.Contains(this))
        {
            allMineralNodes.Add(this);
        }
    }

    private void OnDisable()
    {
        allMineralNodes.Remove(this);
    }

    public int Harvest(int requestedAmount)
    {
        if (IsEmpty)
            return 0;

        int taken = Mathf.Min(requestedAmount, currentAmount);
        currentAmount -= taken;

        Debug.Log(name + " harvested for " + taken + ". Remaining: " + currentAmount);

        if (currentAmount <= 0)
        {
            currentAmount = 0;
            Debug.Log(name + " is empty.");
        }

        UpdateVisual();

        return taken;
    }

    private void UpdateVisual()
    {
        if (hideWhenEmpty && IsEmpty)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!shrinkAsDepleted)
            return;

        float percent = maxAmount > 0
            ? Mathf.Clamp01((float)currentAmount / maxAmount)
            : 0f;

        float scaleMultiplier = Mathf.Lerp(minimumVisualScale, 1f, percent);

        transform.localScale = originalScale * scaleMultiplier;
    }
}