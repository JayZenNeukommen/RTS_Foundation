using UnityEngine;

[RequireComponent(typeof(RTSUnit))]
[RequireComponent(typeof(RTSCombatController))]
public class RTSAutoReactionController : MonoBehaviour
{
    [Header("Reaction Ranges")]
    public float allyHelpRadius = 8f;
    public float fleeDistance = 7f;

    [Header("Reaction Rules")]
    public bool enableSelfDamageReaction = true;
    public bool enableAllyDamageReaction = true;

    private RTSUnit unit;
    private RTSCombatController combatController;

    private void Awake()
    {
        unit = GetComponent<RTSUnit>();
        combatController = GetComponent<RTSCombatController>();
    }

    private void OnEnable()
    {
        RTSUnit.AnyUnitDamaged += HandleAnyUnitDamaged;
    }

    private void OnDisable()
    {
        RTSUnit.AnyUnitDamaged -= HandleAnyUnitDamaged;
    }

    private void HandleAnyUnitDamaged(RTSUnit damagedUnit, RTSUnit attacker, Vector3 damageSourcePosition)
    {
        if (unit == null || combatController == null)
            return;

        if (unit.IsDead)
            return;

        if (unit.faction == RTSFaction.Neutral)
            return;

        if (!combatController.CanAutoReact())
            return;

        if (damagedUnit == unit)
        {
            HandleSelfDamaged(attacker, damageSourcePosition);
            return;
        }

        if (damagedUnit != null && damagedUnit.faction == unit.faction)
        {
            HandleAllyDamaged(damagedUnit, attacker, damageSourcePosition);
        }
    }

    private void HandleSelfDamaged(RTSUnit attacker, Vector3 damageSourcePosition)
    {
        if (!enableSelfDamageReaction)
            return;

        if (ShouldFleeFrom(attacker))
        {
            combatController.FleeFrom(damageSourcePosition, fleeDistance);
            return;
        }

        if (CanRespondAggressivelyTo(attacker))
        {
            combatController.AttackMoveTo(damageSourcePosition);
        }
    }

    private void HandleAllyDamaged(RTSUnit damagedAlly, RTSUnit attacker, Vector3 damageSourcePosition)
    {
        if (!enableAllyDamageReaction)
            return;

        if (!unit.assistNearbyAlliesWhenIdle)
            return;

        if (unit.canGather)
            return;

        if (!unit.canAttack)
            return;

        if (!CanRespondAggressivelyTo(attacker))
            return;

        float distanceToAlly = HorizontalDistance(transform.position, damagedAlly.transform.position);

        if (distanceToAlly > allyHelpRadius)
            return;

        combatController.AttackMoveTo(damageSourcePosition);
    }

    private bool ShouldFleeFrom(RTSUnit attacker)
    {
        if (unit.fleeWhenIdleAttacked)
            return true;

        if (!unit.canAttack)
            return true;

        if (unit.canGather)
            return true;

        if (attacker == null)
            return true;

        if (!unit.CanWeaponTarget(attacker))
            return true;

        if (FogOfWarManager.Instance != null &&
            !FogOfWarManager.Instance.IsUnitVisibleToFaction(attacker, unit.faction) &&
            !FogOfWarManager.Instance.IsUnitTemporarilyRevealed(attacker))
        {
            return true;
        }

        return false;
    }

    private bool CanRespondAggressivelyTo(RTSUnit attacker)
    {
        if (attacker == null)
            return false;

        if (!unit.canAttack)
            return false;

        if (!unit.CanWeaponTarget(attacker))
            return false;

        if (FogOfWarManager.Instance != null &&
            !FogOfWarManager.Instance.IsUnitVisibleToFaction(attacker, unit.faction) &&
            !FogOfWarManager.Instance.IsUnitTemporarilyRevealed(attacker))
        {
            return false;
        }

        return true;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}