using UnityEngine;
using System.Collections.Generic;

public class RTSProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public bool homing = true;
    public float hitDistance = 0.25f;
    public ProjectileMovementMode movementMode = ProjectileMovementMode.Straight;
    public float arcHeight = 3f;

    [Header("Lifetime")]
    public float maxLifetime = 8f;

    [Header("Splash Visual")]
    public GameObject splashImpactEffectPrefab;
    public float splashVisualLifetime = 0.35f;

    private RTSUnit attacker;
    private RTSUnit target;

    private RTSFaction attackerFaction;
    private Vector3 targetPoint;
    private Vector3 lastKnownTargetPoint;

    private Vector3 startPosition;
    private float travelDuration;
    private float travelTimer;

    private float damage;
    private float splashRadius;
    private bool forcedMiss;

    private bool initialized;

    public void Initialize(
    RTSUnit newAttacker,
    RTSUnit newTarget,
    Vector3 newTargetPoint,
    float newDamage,
    float newSpeed,
    bool newHoming,
    float newSplashRadius,
    ProjectileMovementMode newMovementMode,
    float newArcHeight,
    GameObject newSplashImpactEffectPrefab,
    bool newForcedMiss
)
    {
        attacker = newAttacker;
        target = newTarget;

        attackerFaction = attacker != null ? attacker.faction : RTSFaction.Neutral;

        targetPoint = newTargetPoint;
        lastKnownTargetPoint = newTargetPoint;

        damage = newDamage;
        speed = Mathf.Max(0.1f, newSpeed);
        homing = newForcedMiss ? false : newHoming;
        splashRadius = newSplashRadius;

        movementMode = newMovementMode;
        arcHeight = newArcHeight;
        splashImpactEffectPrefab = newSplashImpactEffectPrefab;
        forcedMiss = newForcedMiss;

        startPosition = transform.position;

        float distance = Vector3.Distance(startPosition, targetPoint);
        travelDuration = Mathf.Max(0.05f, distance / speed);
        travelTimer = 0f;

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        maxLifetime -= Time.deltaTime;

        if (maxLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateTargetPoint();

        if (movementMode == ProjectileMovementMode.Arc)
        {
            MoveArcProjectile();
        }
        else
        {
            MoveStraightProjectile();
        }
    }

    private void UpdateTargetPoint()
    {
        if (forcedMiss)
        {
            targetPoint = lastKnownTargetPoint;
            return;
        }

        if (homing && target != null && !target.IsDead && target.isTargetable)
        {
            lastKnownTargetPoint = target.transform.position + Vector3.up * 0.8f;
            targetPoint = lastKnownTargetPoint;
        }
        else
        {
            targetPoint = lastKnownTargetPoint;
        }
    }

    private void MoveStraightProjectile()
    {
        Vector3 direction = targetPoint - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        if (direction.magnitude <= Mathf.Max(hitDistance, distanceThisFrame))
        {
            Impact();
            return;
        }

        Vector3 move = direction.normalized * distanceThisFrame;
        transform.position += move;

        if (move.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(move.normalized);
        }
    }

    private void MoveArcProjectile()
    {
        travelTimer += Time.deltaTime;

        float t = Mathf.Clamp01(travelTimer / travelDuration);

        Vector3 flatPosition = Vector3.Lerp(startPosition, targetPoint, t);

        float arcOffset = 4f * arcHeight * t * (1f - t);

        Vector3 nextPosition = flatPosition + Vector3.up * arcOffset;
        Vector3 move = nextPosition - transform.position;

        transform.position = nextPosition;

        if (move.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(move.normalized);
        }

        if (t >= 1f)
        {
            Impact();
        }
    }

    private void Impact()
    {
        Vector3 impactPoint = GetImpactPoint();

        if (splashRadius > 0f)
        {
            SpawnSplashVisual(impactPoint);
            DealSplashDamage(impactPoint);
        }
        else
        {
            DealDirectDamage(impactPoint);
        }

        Destroy(gameObject);
    }

    private void DealDirectDamage(Vector3 impactPoint)
    {
        if (forcedMiss)
        {
            Debug.Log("Projectile missed its target.");
            return;
        }

        if (target == null || target.IsDead || !target.isTargetable)
            return;

        if (attacker != null && !attacker.IsEnemy(target))
            return;

        // Non-homing direct projectiles can miss if the target moved away.
        if (!homing)
        {
            float distance = HorizontalDistance(impactPoint, target.transform.position);
            float allowedMissDistance = Mathf.Max(0.6f, hitDistance + 0.5f);

            if (distance > allowedMissDistance)
            {
                Debug.Log("Non-homing projectile missed " + target.unitName);
                return;
            }
        }

        target.TakeDamage(damage, attacker, impactPoint);
    }

    private void DealSplashDamage(Vector3 impactPoint)
    {
        IReadOnlyList<RTSUnit> allUnits = RTSUnit.AllUnits;

        foreach (RTSUnit unit in allUnits)
        {
            if (unit == null)
                continue;

            if (unit.IsDead || !unit.isTargetable)
                continue;

            if (unit.faction == RTSFaction.Neutral)
                continue;

            if (unit.faction == attackerFaction)
                continue;

            float distance = HorizontalDistance(impactPoint, unit.transform.position);

            if (distance <= splashRadius)
            {
                unit.TakeDamage(damage, attacker, impactPoint);
            }
        }
    }

    private Vector3 GetImpactPoint()
    {
        Vector3 point = transform.position;

        if (!forcedMiss && homing && target != null)
        {
            point = target.transform.position;
        }

        if (TryFindGroundPoint(point, out Vector3 groundPoint))
        {
            return groundPoint + Vector3.up * 0.05f;
        }

        return point;
    }

    private bool TryFindGroundPoint(Vector3 nearPoint, out Vector3 groundPoint)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            nearPoint + Vector3.up * 8f,
            Vector3.down,
            20f
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            WalkableGround ground = hit.collider.GetComponentInParent<WalkableGround>();

            if (ground != null)
            {
                groundPoint = hit.point;
                return true;
            }
        }

        groundPoint = nearPoint;
        return false;
    }

    private void SpawnSplashVisual(Vector3 impactPoint)
    {
        GameObject effectObject;

        if (splashImpactEffectPrefab != null)
        {
            effectObject = Instantiate(splashImpactEffectPrefab, impactPoint, Quaternion.identity);
        }
        else
        {
            effectObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            effectObject.name = "SplashImpact_Default";

            Collider collider = effectObject.GetComponent<Collider>();

            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = effectObject.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.35f, 0f, 0.65f);
            }
        }

        SplashImpactEffect effect = effectObject.GetComponent<SplashImpactEffect>();

        if (effect == null)
        {
            effect = effectObject.AddComponent<SplashImpactEffect>();
        }

        effect.Initialize(splashRadius, splashVisualLifetime);
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }
}