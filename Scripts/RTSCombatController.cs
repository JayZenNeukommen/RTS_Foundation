using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum RTSCommandState
{
    Idle,
    Moving,
    AttackTarget,
    AttackMove,
    AttackGround,
    Patrol,
    HoldPosition,
    Stopped
}

[RequireComponent(typeof(RTSUnit))]
[RequireComponent(typeof(NavMeshAgent))]
public class RTSCombatController : MonoBehaviour
{
    private RTSUnit self;
    private NavMeshAgent agent;

    private RTSUnit currentTarget;
    private RTSCommandState currentState = RTSCommandState.Idle;

    private Vector3 attackMoveDestination;
    private Vector3 attackGroundPoint;

    private Vector3 patrolPointA;
    private Vector3 patrolPointB;
    private bool patrollingToB;

    private float attackTimer;
    private float defaultStoppingDistance;

    public RTSCommandState CurrentState => currentState;

    public bool CanAutoReact()
    {
        return currentState == RTSCommandState.Idle;
    }

    private void Awake()
    {
        self = GetComponent<RTSUnit>();
        agent = GetComponent<NavMeshAgent>();

        agent.speed = self.moveSpeed;
        defaultStoppingDistance = agent.stoppingDistance;
    }

    private void Update()
    {
        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case RTSCommandState.Idle:
                LookForNearbyEnemyAndAttack();
                break;

            case RTSCommandState.Moving:
                HandleMoving();
                break;

            case RTSCommandState.AttackTarget:
                HandleAttackTarget();
                break;

            case RTSCommandState.AttackMove:
                HandleAttackMove();
                break;

            case RTSCommandState.AttackGround:
                HandleAttackGround();
                break;

            case RTSCommandState.Patrol:
                HandlePatrol();
                break;

            case RTSCommandState.HoldPosition:
                HandleHoldPosition();
                break;

            case RTSCommandState.Stopped:
                break;
        }
    }

    public void MoveTo(Vector3 destination)
    {
        currentTarget = null;
        currentState = RTSCommandState.Moving;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    public void AttackTarget(RTSUnit target)
    {
        if (target == null)
        {
            Debug.LogWarning(name + " tried to attack null target.");
            return;
        }

        if (!self.canAttack)
        {
            Debug.LogWarning(name + " cannot attack because canAttack is false.");
            return;
        }

        if (!self.IsEnemy(target))
        {
            Debug.LogWarning(name + " cannot attack " + target.name + " because it is not an enemy.");
            return;
        }

        Debug.Log(name + " is now attacking " + target.name);

        currentTarget = target;
        currentState = RTSCommandState.AttackTarget;

        agent.isStopped = false;
    }

    public void AttackMoveTo(Vector3 destination)
    {
        if (!self.canAttack)
        {
            MoveTo(destination);
            return;
        }

        attackMoveDestination = destination;
        currentTarget = null;
        currentState = RTSCommandState.AttackMove;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(destination);

        Debug.Log(name + " attack-moving to " + destination);
    }

    public void AttackGroundAt(Vector3 groundPoint)
    {
        if (!self.canAttack || !self.canAttackGround)
        {
            Debug.LogWarning(name + " cannot attack ground.");
            return;
        }

        if (!self.useProjectileAttack || self.projectilePrefab == null)
        {
            Debug.LogWarning(name + " cannot attack ground because it has no projectile attack.");
            return;
        }

        if (self.splashRadius <= 0f)
        {
            Debug.LogWarning(name + " cannot attack ground because splash radius is 0.");
            return;
        }

        attackGroundPoint = groundPoint;
        currentTarget = null;
        currentState = RTSCommandState.AttackGround;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;

        Debug.Log(name + " attack-ground command at " + groundPoint);
    }

    public void PatrolTo(Vector3 destination)
    {
        patrolPointA = transform.position;
        patrolPointB = destination;
        patrollingToB = true;

        currentTarget = null;
        currentState = RTSCommandState.Patrol;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(patrolPointB);

        Debug.Log(name + " patrolling to " + destination);
    }

    public void StopCommand()
    {
        currentTarget = null;
        currentState = RTSCommandState.Stopped;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.ResetPath();
        agent.isStopped = true;
    }

    public void HoldPosition()
    {
        currentTarget = null;
        currentState = RTSCommandState.HoldPosition;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.ResetPath();
        agent.isStopped = true;
    }

    private void HandleMoving()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            currentState = RTSCommandState.Idle;
        }
    }

    private void HandleAttackTarget()
    {
        if (!IsValidVisibleEnemy(currentTarget))
        {
            currentTarget = null;
            currentState = RTSCommandState.Idle;
            agent.ResetPath();
            agent.isStopped = true;
            return;
        }

        // Important improvement:
        // While chasing the commanded target, attack any valid enemy already inside weapon range.
        RTSUnit nearbyEnemyInWeaponRange = FindNearestEnemyInRange(GetSelfEffectiveAttackRange());

        if (nearbyEnemyInWeaponRange != null)
        {
            agent.ResetPath();
            agent.isStopped = true;

            FaceTarget(nearbyEnemyInWeaponRange.transform.position);
            TryAttack(nearbyEnemyInWeaponRange);
            return;
        }

        ChaseOrAttackCurrentTarget();
    }

    private void HandleAttackMove()
    {
        if (!self.canAttack)
        {
            MoveTo(attackMoveDestination);
            return;
        }

        RTSUnit enemyInWeaponRange = FindNearestEnemyInRange(GetSelfEffectiveAttackRange());

        if (enemyInWeaponRange != null)
        {
            currentTarget = enemyInWeaponRange;

            agent.ResetPath();
            agent.isStopped = true;

            FaceTarget(enemyInWeaponRange.transform.position);
            TryAttack(enemyInWeaponRange);
            return;
        }

        if (!IsValidVisibleEnemy(currentTarget) || !IsTargetWithinRange(currentTarget, self.sightRange))
        {
            currentTarget = FindNearestEnemyInRange(self.sightRange);
        }

        if (currentTarget != null)
        {
            ChaseOrAttackCurrentTarget();
            return;
        }

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(attackMoveDestination);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            currentState = RTSCommandState.Idle;
        }
    }

    private void HandleAttackGround()
    {
        if (!self.canAttack || !self.canAttackGround)
        {
            currentState = RTSCommandState.Idle;
            return;
        }

        float distance = GetHorizontalDistanceTo(attackGroundPoint);
        float effectiveAttackRange = GetSelfEffectiveAttackRange();

        if (distance > effectiveAttackRange)
        {
            agent.isStopped = false;
            agent.stoppingDistance = Mathf.Max(0.2f, effectiveAttackRange * 0.75f);
            agent.SetDestination(attackGroundPoint);
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;

        FaceTarget(attackGroundPoint);
        TryAttackGround(attackGroundPoint);
    }

    private void HandlePatrol()
    {
        if (!self.canAttack)
        {
            ContinuePatrolMovement();
            return;
        }

        RTSUnit enemyInWeaponRange = FindNearestEnemyInRange(GetSelfEffectiveAttackRange());

        if (enemyInWeaponRange != null)
        {
            currentTarget = enemyInWeaponRange;

            agent.ResetPath();
            agent.isStopped = true;

            FaceTarget(enemyInWeaponRange.transform.position);
            TryAttack(enemyInWeaponRange);
            return;
        }

        if (!IsValidVisibleEnemy(currentTarget) || !IsTargetWithinRange(currentTarget, self.sightRange))
        {
            currentTarget = FindNearestEnemyInRange(self.sightRange);
        }

        if (currentTarget != null)
        {
            ChaseOrAttackCurrentTarget();
            return;
        }

        ContinuePatrolMovement();
    }

    private void HandleHoldPosition()
    {
        if (!IsValidVisibleEnemy(currentTarget) || !IsTargetInRange(currentTarget))
        {
            currentTarget = FindNearestEnemyInRange(GetSelfEffectiveAttackRange());
        }

        if (currentTarget != null)
        {
            agent.ResetPath();
            agent.isStopped = true;

            FaceTarget(currentTarget.transform.position);
            TryAttack(currentTarget);
        }
    }

    private void ContinuePatrolMovement()
    {
        Vector3 currentDestination = patrollingToB ? patrolPointB : patrolPointA;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(currentDestination);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            patrollingToB = !patrollingToB;

            Vector3 nextDestination = patrollingToB ? patrolPointB : patrolPointA;

            agent.stoppingDistance = defaultStoppingDistance;
            agent.isStopped = false;
            agent.SetDestination(nextDestination);
        }
    }

    private void ChaseOrAttackCurrentTarget()
    {
        if (currentTarget == null)
            return;

        float distance = GetHorizontalDistanceTo(currentTarget.transform.position);
        float effectiveAttackRange = GetEffectiveAttackRange(currentTarget);

        if (distance > effectiveAttackRange)
        {
            agent.isStopped = false;
            agent.stoppingDistance = Mathf.Max(0.2f, effectiveAttackRange * 0.75f);
            agent.SetDestination(currentTarget.transform.position);
        }
        else
        {
            agent.ResetPath();
            agent.isStopped = true;

            FaceTarget(currentTarget.transform.position);
            TryAttack(currentTarget);
        }
    }

    private void LookForNearbyEnemyAndAttack()
    {
        if (!self.canAttack)
            return;

        if (!self.autoAcquireEnemiesWhenIdle)
            return;

        RTSUnit target = FindNearestEnemyInRange(self.sightRange);

        if (target != null)
        {
            AttackTarget(target);
        }
    }

    private void TryAttack(RTSUnit target)
    {
        if (attackTimer > 0f)
            return;

        if (!IsValidVisibleEnemy(target))
            return;

        attackTimer = self.attackCooldown;

        Debug.Log($"{self.unitName} attacks {target.unitName}");
        RTSAttackResolver.PerformAttack(self, target);
    }

    private void TryAttackGround(Vector3 groundPoint)
    {
        if (attackTimer > 0f)
            return;

        attackTimer = self.attackCooldown;

        Debug.Log($"{self.unitName} attacks ground at {groundPoint}");
        RTSAttackResolver.PerformAttackGround(self, groundPoint);
    }

    private RTSUnit FindNearestEnemyInRange(float range)
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

            float distance = GetHorizontalDistanceTo(unit.transform.position);

            if (distance <= range && distance < nearestDistance)
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

        if (target.IsDead)
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

        float distance = GetHorizontalDistanceTo(target.transform.position);
        return distance <= GetEffectiveAttackRange(target);
    }

    private bool IsTargetWithinRange(RTSUnit target, float range)
    {
        if (target == null)
            return false;

        float distance = GetHorizontalDistanceTo(target.transform.position);
        return distance <= range;
    }

    private float GetHorizontalDistanceTo(Vector3 targetPosition)
    {
        Vector3 a = transform.position;
        Vector3 b = targetPosition;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    private float GetEffectiveAttackRange(RTSUnit target)
    {
        float bonusRange = 0.5f;

        NavMeshAgent targetAgent = target.Agent;

        if (agent != null)
        {
            bonusRange += agent.radius;
        }

        if (targetAgent != null)
        {
            bonusRange += targetAgent.radius;
        }

        return self.attackRange + bonusRange;
    }

    private float GetSelfEffectiveAttackRange()
    {
        float bonusRange = 0.5f;

        if (agent != null)
        {
            bonusRange += agent.radius;
        }

        return self.attackRange + bonusRange;
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

    public void FleeFrom(Vector3 dangerPosition, float fleeDistance)
    {
        Vector3 direction = transform.position - dangerPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;
            direction = new Vector3(randomDirection.x, 0f, randomDirection.y);
        }

        Vector3 desiredPosition = transform.position + direction.normalized * fleeDistance;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            desiredPosition = hit.position;
        }

        currentTarget = null;
        currentState = RTSCommandState.Moving;

        agent.stoppingDistance = defaultStoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(desiredPosition);

        Debug.Log(name + " fleeing from damage source.");
    }
}