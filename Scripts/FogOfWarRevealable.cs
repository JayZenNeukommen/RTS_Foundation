using UnityEngine;

public class FogOfWarRevealable : MonoBehaviour
{
    [Header("Reveal Settings")]
    public RTSFaction visibleToFaction = RTSFaction.Player1;
    public bool hideWhenNotVisible = true;
    public bool disableCollidersWhenHidden = true;

    private RTSUnit unit;
    private Renderer[] renderers;
    private Collider[] colliders;

    private void Awake()
    {
        unit = GetComponentInParent<RTSUnit>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    private void Update()
    {
        if (!hideWhenNotVisible)
            return;

        if (unit == null)
            return;

        if (unit.faction == visibleToFaction)
        {
            SetVisible(true);
            return;
        }

        if (unit.faction == RTSFaction.Neutral)
        {
            SetVisible(true);
            return;
        }

        if (FogOfWarManager.Instance == null)
        {
            SetVisible(true);
            return;
        }

        bool visible =
            FogOfWarManager.Instance.IsUnitVisibleToFaction(unit, visibleToFaction) ||
            FogOfWarManager.Instance.IsUnitTemporarilyRevealed(unit);

        SetVisible(visible);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        if (!disableCollidersWhenHidden)
            return;

        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = visible;
            }
        }
    }
}