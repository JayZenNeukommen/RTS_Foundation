using UnityEngine;

[CreateAssetMenu(fileName = "NewUnitDefinition", menuName = "RTS/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [Header("Identity")]
    public string unitName = "Unit";

    [Header("Health")]
    public int maxHp = 45;

    [Header("Movement")]
    public float moveSpeed = 3.5f;

    [Header("Worker")]
    public bool canGather = false;
    public bool canBuild = false;

    [Header("Combat")]
    public bool canAttack = true;
    public float attackDamage = 5f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public float sightRange = 7f;

    [Header("Supply")]
    public int supplyCost = 1;

    [Header("Targeting")]
    public bool isAirUnit = false;
    public bool canTargetGround = true;
    public bool canTargetAir = false;

    [Header("Auto Behavior")]
    public bool autoAcquireEnemiesWhenIdle = true;
    public bool assistNearbyAlliesWhenIdle = true;
    public bool fleeWhenIdleAttacked = false;

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
}