using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Enemies;
using NostalgiaOrbitDLL.Environments;
using NostalgiaOrbitDLL.Maps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapWorker : MonoBehaviour
{
    public AbstractMap AbstractMap;
    public List<AbstractMapObject> MapObjects = new List<AbstractMapObject>();
    public Dictionary<PrefabTypes, int> LifeEnemyObject = new Dictionary<PrefabTypes, int>();
    public List<AbstractEnvironmentObject> EnvironmentObjects = new List<AbstractEnvironmentObject>();

    private void Start()
    {
        InvokeRepeating(nameof(UpdateEverySecond), 1, 1);
        InvokeRepeating(nameof(UpdateEvery10Second), 1, 10);

        CheckEnvironments();
    }

    private void UpdateEverySecond()
    {
        foreach (var mapObject in MapObjects.ToList())
        {
            // Obrazenia za promieniowanie bool

            if (mapObject.IsPlayer)
            {
                SearchEnvironmentObjectsToSynchronize(mapObject);

                SearchMapObjectsToSynchronize(mapObject);

                SearchSafeZone(mapObject);
            }

            mapObject.UpdateEvery1Second(this);

            TryLogoutPilot(mapObject);
        }

        foreach (var environmentObject in EnvironmentObjects.ToList())
        {
            environmentObject.UpdateEverySecond(this);
        }

        CheckEnemies();
    }

    private static Func<AbstractMapObject, bool> checkMapObject = o => o.IsUnderAttack == 0 && !o.AttackAmmunition && !o.AttackRocket;
    private void SearchSafeZone(AbstractMapObject mapObject)
    {
        bool find = false;

        if (mapObject.IsUnderAttack == 0 && !mapObject.AttackAmmunition && !mapObject.AttackRocket && !AbstractMap.IsPvp && mapObject is PilotMapObject pilotMapObject && DLLHelpers.IsCompanyMap(pilotMapObject.Pilot.FirmType, AbstractMap.MapType))
        {
            foreach (var portal in AbstractMap.Portals)
            {
                if (checkMapObject(mapObject) && Extensions.Distance(portal.Position.ToVector(), mapObject.Position) <= Portal.SafeZoneDistance)
                {
                    find = true;
                    break;
                }
            }

            if (AbstractMap.Base_PrefabType.HasValue)
            {
                if (checkMapObject(mapObject) && Extensions.Distance(AbstractMap.Base_Position.ToVector(), mapObject.Position) <= AbstractMap.BaseSafeZoneDistance)
                {
                    find = true;
                }
            }
        }

        mapObject.IsSafe = find;
    }

    private void UpdateEvery10Second()
    {
        foreach (var mapObject in MapObjects.ToList())
        {
            mapObject.UpdateEvery10Second(this);

            // Promieniowanie check
        }
    }

    private void Update()
    {
        foreach (var mapObject in MapObjects.ToList())
        {
            if (mapObject.IsDead)
            {
                if (mapObject.IsPlayer)
                {
                    Debug.LogWarning($"Player dead {mapObject.Id}");
                    mapObject.OnDead();

                    if (mapObject is PilotMapObject pilot)
                    {
                        var respawnWorker = Server.MapInstances[DLLHelpers.GetRespawnMap(AbstractMap.MapType, pilot.Pilot.FirmType)];

                        if (CanPilotLeaveFromMap(pilot) && respawnWorker.CanPilotJoinToMap(pilot) && pilot.RepairShip())
                        {
                            PilotLeaveFromMap(pilot);
                            respawnWorker.PilotJoinToMap(pilot, respawnWorker.AbstractMap.Base_Position.ToVector());
                        }
                    }
                }
                else
                {
                    StartCoroutine(DestroyEnemy(mapObject));
                }
            }

            mapObject.Update(this);
        }
    }

    private void LateUpdate()
    {
        foreach (var environmentObject in EnvironmentObjects.ToList())
        {
            environmentObject.LateUpdate(this);
        }
    }

    public void CollectEnvironment(Guid environmentId, PilotMapObject pilot)
    {
        var environmnt = EnvironmentObjects.FirstOrDefault(o => o.Id == environmentId);

        if (environmnt != null)
        {
            if (environmnt.OwnerId.HasValue && environmnt.OwnerId != pilot.Id)
            {
                pilot.TakeCargo(new Reward()
                {
                    Honor = -100
                });
            }

            environmnt.OwnerId = pilot.Id;
            environmnt.StartCollecting = true;
        }
    }

    private void SearchEnvironmentObjectsToSynchronize(AbstractMapObject abstractMapObject)
    {
        foreach (var environmentObject in EnvironmentObjects)
        {
            if (Extensions.Distance(environmentObject.Position, abstractMapObject.Position) <= AbstractMap.SynchronizeDistance)
            {
                environmentObject.AddEnvironmentObjectToSynchronize(abstractMapObject);
            }
            else
            {
                environmentObject.RemoveEnvironmentObjectToSynchronize(abstractMapObject);
            }
        }
    }

    private void SearchMapObjectsToSynchronize(AbstractMapObject abstractMapObject)
    {
        foreach (var mapObject in MapObjects)
        {
            if (mapObject.Id == abstractMapObject.Id)
                continue;

            if (Extensions.Distance(mapObject, abstractMapObject) <= AbstractMap.SynchronizeDistance || mapObject.TargetMapObject == abstractMapObject || abstractMapObject.TargetMapObject == mapObject || abstractMapObject.PrefabType == PrefabTypes.Admin)
            {
                abstractMapObject.AddMapObjectToSynchronize(mapObject);
            }
            else
            {
                abstractMapObject.RemoveMapObjectToSynchronize(mapObject);
            }
        }
    }

    private void TryLogoutPilot(AbstractMapObject mapObject)
    {
        if (mapObject.IsWantLogout && mapObject is PilotMapObject pilotMapObject)
        {
            if (pilotMapObject.IsUnderAttack > 0)
            {
                pilotMapObject.LogoutTimer = 0;
            }
            else
            {
                pilotMapObject.LogoutTimer++;

                if (pilotMapObject.LogoutTimer >= pilotMapObject.RequireLogoutTime)
                    PilotLeaveFromMap(pilotMapObject);
            }
        }
    }



    #region Enemy
    private void CheckEnemies()
    {
        foreach (var enemyOnMap in AbstractMap.Enemies)
        {
            if (!LifeEnemyObject.ContainsKey(enemyOnMap.EnemyType))
            {
                LifeEnemyObject.Add(enemyOnMap.EnemyType, 0);
            }

            int count = enemyOnMap.Quantity - LifeEnemyObject[enemyOnMap.EnemyType];

            SpawnEnemies(enemyOnMap, count);
        }
    }

    private void SpawnEnemies(EnemyMap enemyMap, int count)
    {
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                LifeEnemyObject[enemyMap.EnemyType]++;

                var position = RandomPosition(enemyMap.AreaSpawn_Min, enemyMap.AreaSpawn_Max);

                var standardMapObject = new StandardMapObject(AbstractEnemy.GetEnemyByType(enemyMap.EnemyType), position);
                MapObjects.Add(standardMapObject);

                standardMapObject.UpdateEvery10Second(this);
            }

            Debug.Log($"[{AbstractMap.MapType}] Spawning {enemyMap.EnemyType} x {count}");
        }
    }
    #endregion Enemy



    #region Environment
    private void CheckEnvironments()
    {
        foreach (var resource in AbstractMap.Resources)
        {
            SpawnEnvironment(resource, resource.Quantity);
        }
    }

    private void SpawnEnvironment(ResourceMap resource, int count, Guid? ownerId = null)
    {
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var abstractEnvironment = AbstractEnvironment.GetEnvironmentByType(resource.PrefabType);

                var position = RandomPosition(resource.AreaSpawn_Min, resource.AreaSpawn_Max);

                var standardEnvironmentObject = new StandardEnvironmentObject(abstractEnvironment, position);

                if (ownerId != null)
                {
                    standardEnvironmentObject.OwnerId = ownerId;
                }

                EnvironmentObjects.Add(standardEnvironmentObject);
            }

            Debug.Log($"[{AbstractMap.MapType}] Spawning environment {resource.PrefabType} x {count} for Guid? {ownerId}");
        }
    }

    public void DestroyEnvironment(AbstractEnvironmentObject environmentObject)
    {
        EnvironmentObjects.Remove(environmentObject);

        foreach (var item in environmentObject.AreaPlayers.ToList())
        {
            if (item.TryGetTarget(out var pilot))
                environmentObject.RemoveEnvironmentObjectToSynchronize(pilot);
        }
    }
    #endregion Environment



    #region Random position or circle
    public static Vector2 RandomPosition(PositionVector a, PositionVector b)
    {
        return new Vector2(UnityEngine.Random.Range(a.Position_X, b.Position_X), UnityEngine.Random.Range(a.Position_Y, b.Position_Y));
    }

    public static Vector2 RandomCircle(Vector2 center, float radius)
    {
        var ang = UnityEngine.Random.value * 360;
        var position = new Vector2()
        {
            x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad),
            y = center.y + radius * Mathf.Cos(ang * Mathf.Deg2Rad),
        };

        return position;
    }
    public static Vector2 RandomCircle(Vector2 center, float radius, AbstractMap abstractMap)
    {
        var position = RandomCircle(center, radius);

        if (position.x < 0 || position.x > abstractMap.MapSize.Position_X || position.y > 0 || position.y < -abstractMap.MapSize.Position_Y)
        {
            return new Vector2(UnityEngine.Random.Range(0, abstractMap.MapSize.Position_X), -UnityEngine.Random.Range(0, abstractMap.MapSize.Position_Y));
        }

        return position;
    }
    #endregion Random position or circle



    private static float DamageDispersion = 0.1f;
    private static float DamagePrecision = 0.8f;
    private static int DamagePrecisionPercentage = Convert.ToInt32(DamagePrecision * 100);
    public static long RandomDamage(long damage, float extraPrecision = 0)
    {
        if (DamagePrecision + extraPrecision < 1 && UnityEngine.Random.Range(1, 101) >= DamagePrecisionPercentage)
            return 0;

        return Convert.ToInt64(UnityEngine.Random.Range(damage * (1 - DamageDispersion), damage * (1 + DamageDispersion)));
    }

    private IEnumerator DestroyEnemy(AbstractMapObject mapObject)
    {
        mapObject.OnDead();
        MapObjects.Remove(mapObject);

        var prefabType = mapObject.PrefabType;

        var owner = mapObject.AttackPlayerOwner;
        if (owner != null)
        {
            SpawnCargo(owner.Id, prefabType, mapObject.Position);
        }

        var enemyOnMap = AbstractMap.Enemies.FirstOrDefault(o => o.EnemyType == prefabType);

        if (enemyOnMap != null)
        {
            if (enemyOnMap.SpawnEverySecond > 0)
                yield return new WaitForSeconds(enemyOnMap.SpawnEverySecond);
        }

        if (LifeEnemyObject.ContainsKey(prefabType))
        {
            LifeEnemyObject[prefabType]--;

            if (LifeEnemyObject[prefabType] <= 0)
                LifeEnemyObject.Remove(prefabType);
        }
    }

    public void SpawnCargo(Guid ownerId, PrefabTypes enemyType, Vector2 position)
    {
        var cargoBox = new StandardEnvironmentObject(CargoBox.Instance, position);

        cargoBox.OwnerId = ownerId;
        cargoBox.ExtraCargos = AbstractEnemy.GetEnemyByType(enemyType).Cargo;

        EnvironmentObjects.Add(cargoBox);
    }

    public Func<PilotMapObject, bool> CanPilotJoinToMap => pilot => pilot.Pilot.Level >= AbstractMap.RequiredLevel;
    public bool PilotJoinToMap(PilotMapObject pilotMapObject, Vector2 position)
    {
        if (!CanPilotJoinToMap(pilotMapObject))
            return false;

        MapObjects.Add(pilotMapObject);

        pilotMapObject.ChangeMap(AbstractMap.MapType, position);

        return true;
    }

    public Func<PilotMapObject, bool> CanPilotLeaveFromMap => pilot => !AbstractMap.IsPvp || (AbstractMap.IsPvp && pilot.IsUnderAttack == 0);
    public bool PilotLeaveFromMap(PilotMapObject pilotMapObject)
    {
        if (!CanPilotLeaveFromMap(pilotMapObject))
            return false;

        MapObjects.Remove(pilotMapObject);

        foreach (var mapObject in MapObjects)
        {
            mapObject.RemoveMapObjectToSynchronize(pilotMapObject);
        }

        foreach (var environmentObject in EnvironmentObjects)
        {
            environmentObject.RemoveEnvironmentObjectToSynchronize(pilotMapObject);
        }

        if (pilotMapObject.IsWantLogout)
        {
            Server.PlayerDisconnected(pilotMapObject);
        }

        return true;
    }
}