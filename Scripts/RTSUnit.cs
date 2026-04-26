using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum RTSFaction
{
    Player1,
    Player2,
    Neutral
}

public enum ProjectileMovementMode
{
    Straight,
    Arc
}

public class RTSUnit : MonoBehaviour
{
    private static readonly List<RTSUnit> allUnits = new List<RTSUnit>();
    public static IReadOnlyList<RTSUnit> AllUnits => allUnits;

    public NavMeshAgent Agent { get; private set; }

    [Header("Identity")]
    public string unitName = "Unit";
    public RTSFaction faction = RTSFaction.Player1;
    public bool isTargetable = true;

    [Header("Definition")]
    public UnitDefinition unitDefinition;
    public bool applyDefinitionOnAwake = true;

    [Header("Supply")]
    public int supplyCost = 0;

    [Header("Health")]
    public int maxHp = 45;
    public int currentHp = 45;

    public bool IsDead => currentHp <= 0;
    public bool IsDamaged => currentHp > 0 && currentHp < maxHp;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Combat")]
    public bool canAttack = true;
    public float attackDamage = 5f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public float sightRange = 7f;

    [Header("Projectile Attack")]
    public bool useProjectileAttack = false;
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;
    public bool projectileHoming = true;
    public float projectileSpawnHeight = 1f;
    public float splashRadius = 0f;

    [Header("Projectile Motion")]
    public ProjectileMovementMode projectileMovementMode = ProjectileMovementMode.Straight;
    public float projectileArcHeight = 3f;

    [Header("Splash Visual")]
    public GameObject splashImpactEffectPrefab;

    [Header("Attack Ground")]
    public bool canAttackGround = false;

    [Header("Highground Combat")]
    public bool useHighgroundMissChance = true;
    [Range(0f, 1f)] public float lowgroundToHighgroundMissChance = 0.25f;
    public float highgroundMissOffsetRadius = 2f;

    [Header("Targeting")]
    public bool isAirUnit = false;
    public bool canTargetGround = true;
    public bool canTargetAir = false;

    [Header("Auto Behavior")]
    public bool autoAcquireEnemiesWhenIdle = true;
    public bool assistNearbyAlliesWhenIdle = true;
    public bool fleeWhenIdleAttacked = false;

    [Header("Worker Abilities")]
    public bool canGather = false;
    public bool canBuild = false;

    public static event Action<RTSUnit, RTSUnit, Vector3> AnyUnitDamaged;
    public event Action<RTSUnit, RTSUnit, Vector3> Damaged;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();

        if (applyDefinitionOnAwake && unitDefinition != null)
        {
            ApplyDefinition(unitDefinition, true);
        }
    }

    private void OnEnable()
    {
        if (!allUnits.Contains(this))
        {
            allUnits.Add(this);
        }
    }

    private void OnDisable()
    {
        allUnits.Remove(this);
    }

    public bool IsEnemy(RTSUnit other)
    {
        if (other == null)
            return false;

        if (!isTargetable || !other.isTargetable)
            return false;

        if (faction == RTSFaction.Neutral || other.faction == RTSFaction.Neutral)
            return false;

        return faction != other.faction;
    }

    public void TakeDamage(float amount, RTSUnit attacker = null, Vector3? damageSourcePosition = null)
    {
        if (IsDead)
            return;

        Vector3 sourcePosition = damageSourcePosition ?? transform.position;

        if (attacker != null)
        {
            sourcePosition = attacker.transform.position;
        }

        currentHp -= Mathf.RoundToInt(amount);

        Debug.Log($"{unitName} took {amount} damage. HP: {currentHp}/{maxHp}");

        Damaged?.Invoke(this, attacker, sourcePosition);
        AnyUnitDamaged?.Invoke(this, attacker, sourcePosition);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    public void Repair(int amount)
    {
        if (IsDead)
            return;

        if (amount <= 0)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    public bool CanWeaponTarget(RTSUnit other)
    {
        if (other == null)
            return false;

        if (!canAttack)
            return false;

        if (!IsEnemy(other))
            return false;

        if (other.isAirUnit && !canTargetAir)
            return false;

        if (!other.isAirUnit && !canTargetGround)
            return false;

        return true;
    }

    public void ApplyDefinition(UnitDefinition definition, bool refillHealth)
    {
        if (definition == null)
            return;

        unitDefinition = definition;

        unitName = definition.unitName;

        int oldMaxHp = maxHp;
        int oldCurrentHp = currentHp;

        maxHp = definition.maxHp;

        if (refillHealth)
        {
            currentHp = maxHp;
        }
        else
        {
            if (oldMaxHp > 0)
            {
                float hpPercent = Mathf.Clamp01((float)oldCurrentHp / oldMaxHp);
                currentHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpPercent));
            }
            else
            {
                currentHp = maxHp;
            }
        }

        moveSpeed = definition.moveSpeed;

        canGather = definition.canGather;
        canBuild = definition.canBuild;

        canAttack = definition.canAttack;
        attackDamage = definition.attackDamage;
        attackRange = definition.attackRange;
        attackCooldown = definition.attackCooldown;
        sightRange = definition.sightRange;

        supplyCost = definition.supplyCost;

        isAirUnit = definition.isAirUnit;
        canTargetGround = definition.canTargetGround;
        canTargetAir = definition.canTargetAir;

        autoAcquireEnemiesWhenIdle = definition.autoAcquireEnemiesWhenIdle;
        assistNearbyAlliesWhenIdle = definition.assistNearbyAlliesWhenIdle;
        fleeWhenIdleAttacked = definition.fleeWhenIdleAttacked;

        useProjectileAttack = definition.useProjectileAttack;
        projectilePrefab = definition.projectilePrefab;
        projectileSpeed = definition.projectileSpeed;
        projectileHoming = definition.projectileHoming;
        projectileSpawnHeight = definition.projectileSpawnHeight;
        splashRadius = definition.splashRadius;

        projectileMovementMode = definition.projectileMovementMode;
        projectileArcHeight = definition.projectileArcHeight;

        splashImpactEffectPrefab = definition.splashImpactEffectPrefab;

        canAttackGround = definition.canAttackGround;

        useHighgroundMissChance = definition.useHighgroundMissChance;
        lowgroundToHighgroundMissChance = definition.lowgroundToHighgroundMissChance;
        highgroundMissOffsetRadius = definition.highgroundMissOffsetRadius;

        if (Agent != null)
        {
            Agent.speed = moveSpeed;
        }
    }

    private void Die()
    {
        Debug.Log($"{unitName} died.");
        Destroy(gameObject);
    }
}