using UnityEngine;
using System.Collections.Generic;

public enum TurretCommandState
{
    AutoFire,
    AttackTarget,
    HoldFire
}

[RequireComponent(typeof(RTSUnit))]
public class RTSTurretController : MonoBehaviour
{
    private RTSUnit self;
    private RTSUnit currentTarget;

    private float attackTimer;

    public TurretCommandState CurrentState { get; private set; } = TurretCommandState.AutoFire;

    private void Awake()
    {
        self = GetComponent<RTSUnit>();
    }

    private void Update()
    {
        if (!self.canAttack || self.IsDead)
            return;

        attackTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case TurretCommandState.AutoFire:
                HandleAutoFire();
                break;

            case TurretCommandState.AttackTarget:
                HandleAttackTarget();
                break;

            case TurretCommandState.HoldFire:
                break;
        }
    }

    public void AttackTarget(RTSUnit target)
    {
        if (target == null)
            return;

        if (!self.IsEnemy(target))
        {
            Debug.LogWarning(name + " cannot attack " + target.name + " because it is not an enemy.");
            return;
        }

        currentTarget = target;
        CurrentState = TurretCommandState.AttackTarget;

        Debug.Log(name + " focusing fire on " + target.unitName);
    }

    public void StopCommand()
    {
        currentTarget = null;
        CurrentState = TurretCommandState.HoldFire;

        Debug.Log(name + " stopped firing.");
    }

    public void HoldPosition()
    {
        currentTarget = null;
        CurrentState = TurretCommandState.AutoFire;

        Debug.Log(name + " resumed auto-fire.");
    }

    private void HandleAutoFire()
    {
        RTSUnit target = FindNearestEnemyInRange();

        if (target == null)
            return;

        FaceTarget(target.transform.position);
        TryAttack(target);
    }

    private void HandleAttackTarget()
    {
        if (!IsValidVisibleEnemy(currentTarget))
        {
            currentTarget = null;
            CurrentState = TurretCommandState.AutoFire;
            return;
        }

        if (!IsTargetInRange(currentTarget))
        {
            currentTarget = null;
            CurrentState = TurretCommandState.AutoFire;
            return;
        }

        FaceTarget(currentTarget.transform.position);
        TryAttack(currentTarget);
    }

    private void TryAttack(RTSUnit target)
    {
        if (attackTimer > 0f)
            return;

        attackTimer = self.attackCooldown;

        Debug.Log(self.unitName + " fires at " + target.unitName);
        RTSAttackResolver.PerformAttack(self, target);
    }

    private RTSUnit FindNearestEnemyInRange()
    {
        IReadOnlyList<RTSUnit> allUnits = RTSUnit.AllUnits;

        RTSUnit nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;

        foreach (RTSUnit unit in allUnits)
        {
            if (unit == self)
                continue;

            if (!IsValidVisibleEnemy(unit))
                continue;

            float distance = GetHorizontalDistance(transform.position, unit.transform.position);

            if (distance <= self.attackRange && distance < nearestDistance)
            {
                nearestEnemy = unit;
                nearestDistance = distance;
            }
        }

        return nearestEnemy;
    }

    private bool IsValidVisibleEnemy(RTSUnit target)
    {
        if (target == null)
            return false;

        if (target.IsDead || !target.isTargetable)
            return false;

        if (!self.CanWeaponTarget(target))
            return false;

        if (FogOfWarManager.Instance != null &&
            !FogOfWarManager.Instance.IsUnitVisibleToFaction(target, self.faction))
        {
            return false;
        }

        return true;
    }

    private bool IsTargetInRange(RTSUnit target)
    {
        if (target == null)
            return false;

        float distance = GetHorizontalDistance(transform.position, target.transform.position);
        return distance <= self.attackRange + 1f;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
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

    private float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}