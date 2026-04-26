using UnityEngine;

public static class RTSAttackResolver
{
    public static void PerformAttack(RTSUnit attacker, RTSUnit target)
    {
        if (attacker == null || target == null)
            return;

        if (!attacker.canAttack)
            return;

        if (!attacker.IsEnemy(target))
            return;

        HandleHighgroundAttackReveal(attacker, target);

        bool forcedMiss = ShouldMissHighgroundShot(attacker, target, out Vector3 missPoint);

        if (attacker.useProjectileAttack && attacker.projectilePrefab != null)
        {
            if (forcedMiss)
            {
                FireProjectileAtTarget(attacker, target, missPoint, true);
            }
            else
            {
                FireProjectileAtTarget(attacker, target, target.transform.position + Vector3.up * 0.8f, false);
            }
        }
        else
        {
            if (forcedMiss)
            {
                Debug.Log(attacker.unitName + " missed " + target.unitName + " due to highground.");
                return;
            }

            target.TakeDamage(attacker.attackDamage, attacker, attacker.transform.position);
        }
    }

    public static void PerformAttackGround(RTSUnit attacker, Vector3 groundPoint)
    {
        if (attacker == null)
            return;

        if (!attacker.canAttack)
            return;

        if (!attacker.canAttackGround)
            return;

        if (!attacker.useProjectileAttack || attacker.projectilePrefab == null)
        {
            Debug.LogWarning(attacker.name + " cannot attack ground because it has no projectile attack.");
            return;
        }

        if (attacker.splashRadius <= 0f)
        {
            Debug.LogWarning(attacker.name + " cannot attack ground because splash radius is 0.");
            return;
        }

        FireProjectileAtPoint(attacker, groundPoint);
    }

    private static bool ShouldMissHighgroundShot(RTSUnit attacker, RTSUnit target, out Vector3 missPoint)
    {
        missPoint = target != null ? target.transform.position : Vector3.zero;

        if (attacker == null || target == null)
            return false;

        if (!attacker.useHighgroundMissChance)
            return false;

        if (FogOfWarManager.Instance == null)
            return false;

        int attackerLevel = FogOfWarManager.Instance.GetVisionLevelAtWorldPosition(attacker.transform.position);
        int targetLevel = FogOfWarManager.Instance.GetVisionLevelAtWorldPosition(target.transform.position);

        // Only lowground attacking highground gets miss chance.
        if (attackerLevel >= targetLevel)
            return false;

        float roll = Random.value;

        if (roll > attacker.lowgroundToHighgroundMissChance)
            return false;

        Vector2 randomOffset = Random.insideUnitCircle.normalized * attacker.highgroundMissOffsetRadius;

        missPoint = target.transform.position + new Vector3(
            randomOffset.x,
            0f,
            randomOffset.y
        );

        Debug.Log(attacker.unitName + " missed highground target. Shot offset to " + missPoint);

        return true;
    }

    private static void HandleHighgroundAttackReveal(RTSUnit attacker, RTSUnit target)
    {
        if (FogOfWarManager.Instance == null)
            return;

        int attackerLevel = FogOfWarManager.Instance.GetVisionLevelAtWorldPosition(attacker.transform.position);
        int targetLevel = FogOfWarManager.Instance.GetVisionLevelAtWorldPosition(target.transform.position);

        if (attackerLevel <= targetLevel)
            return;

        FogOfWarManager.Instance.TemporarilyRevealUnit(attacker, target.faction, 1.5f);
    }

    private static void FireProjectileAtTarget(
        RTSUnit attacker,
        RTSUnit target,
        Vector3 targetPosition,
        bool forcedMiss
    )
    {
        Vector3 spawnPosition = attacker.transform.position + Vector3.up * attacker.projectileSpawnHeight;

        GameObject projectileObject = Object.Instantiate(
            attacker.projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        RTSProjectile projectile = projectileObject.GetComponent<RTSProjectile>();

        if (projectile == null)
        {
            Debug.LogWarning(attacker.name + " projectile prefab has no RTSProjectile component.");
            Object.Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            attacker,
            target,
            targetPosition,
            attacker.attackDamage,
            attacker.projectileSpeed,
            attacker.projectileHoming,
            attacker.splashRadius,
            attacker.projectileMovementMode,
            attacker.projectileArcHeight,
            attacker.splashImpactEffectPrefab,
            forcedMiss
        );
    }

    private static void FireProjectileAtPoint(RTSUnit attacker, Vector3 groundPoint)
    {
        Vector3 spawnPosition = attacker.transform.position + Vector3.up * attacker.projectileSpawnHeight;
        Vector3 targetPosition = groundPoint + Vector3.up * 0.15f;

        GameObject projectileObject = Object.Instantiate(
            attacker.projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        RTSProjectile projectile = projectileObject.GetComponent<RTSProjectile>();

        if (projectile == null)
        {
            Debug.LogWarning(attacker.name + " projectile prefab has no RTSProjectile component.");
            Object.Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            attacker,
            null,
            targetPosition,
            attacker.attackDamage,
            attacker.projectileSpeed,
            false,
            attacker.splashRadius,
            attacker.projectileMovementMode,
            attacker.projectileArcHeight,
            attacker.splashImpactEffectPrefab,
            false
        );
    }
}