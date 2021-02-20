using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Enemies;
using System;
using System.Linq;
using UnityEngine;

[Serializable]
public class StandardMapObject : AbstractMapObject
{
    public AbstractEnemy AbstractEnemy { get; set; }



    public override PrefabTypes PrefabType => AbstractEnemy.EnemyType;
    public override string ObjectName => AbstractEnemy.GetEnemyName(AbstractEnemy.EnemyType);
    public override Reward RewardForDead => AbstractEnemy.GetEnemyByType(AbstractEnemy.EnemyType).Reward;
    protected override float ShotRange => AbstractEnemy.GetEnemyByType(AbstractEnemy.EnemyType).ShotRange / 15;
    protected override bool AutoAttackRocket => true;



    public StandardMapObject(AbstractEnemy abstractEnemy, Vector2 position)
    {
        Id = Guid.NewGuid();

        AbstractEnemy = abstractEnemy;

        Position = TargetPosition = position;
        Hitpoints = MaxHitpoints;
        Shields = MaxShields;
    }



    #region Speed
    protected override long speed => AbstractEnemy.Speed;
    #endregion Speed



    #region Hitpoints / Shields
    protected override long maxHitpoints => AbstractEnemy.Hitpoints;



    protected override long maxShields => AbstractEnemy.Shields;

    protected override float shieldsAbsorption => AbstractEnemy.ShieldAbsorption;

    public override MapObject GetMapObject()
    {
        return CreateMapObject();
    }

    protected MapObject CreateMapObject()
    {
        return new MapObject()
        {
            Id = Id,
            Name = AbstractEnemy.GetEnemyName(AbstractEnemy.EnemyType),
            ShipType = AbstractEnemy.EnemyType,

            Position = Position.ToPositionVector(),
            TargetPosition = TargetPosition.ToPositionVector(),

            Hitpoints = Hitpoints,
            MaxHitpoints = MaxHitpoints,
            Shields = Shields,
            MaxShields = MaxShields,

            Speed = Speed,
        };
    }
    #endregion Hitpoints / Shields



    protected override bool CanAttackAmmunition => true;
    protected override bool CanAttackRocket => AbstractEnemy.UseRocket;

    protected override float ShieldAbsorption => 0.5f;

    protected override bool CanHitpointsRepair => false;
    protected override int HitpointsRepairTime => 60;

    protected override ResourceTypes ammunitionResource => AbstractEnemy.LaserPrefab;
    protected override ResourceTypes? rocketResource => AbstractEnemy.RocketPrefab;
    protected override long LaserDamage => MapWorker.RandomDamage(AbstractEnemy.LaserDamage);
    protected override long RocketDamage => MapWorker.RandomDamage(AbstractEnemy.RocketDamage);
    protected override int RocketCountdown => AbstractEnemy.RocketDelay * Application.targetFrameRate;


    public override void TakeReward(Reward reward, string mapObject) { }

    private Vector2 LastPositionTargetMapObject;
    private bool LastPositionTargetMapObjectCircle;



    public override void UpdateEvery1Second(MapWorker mapWorker)
    {
        base.UpdateEvery1Second(mapWorker);

        if (IsDead)
            return;

        if (AttackAmmunition && TargetMapObject != null && (Extensions.Distance(this, TargetMapObject) > 200 || TargetMapObject.IsSafe))
        {
            TargetMapObject = null;
            TargetPosition = MapWorker.RandomCircle(Position, UnityEngine.Random.Range(5, 30));
        }
        else if (TargetMapObject == null && AbstractEnemy.Aggresive && AreaObjects.Any())
        {
            var target = AreaObjects.FirstOrDefault(o => o.TryGetTarget(out var pilot) && pilot.IsPlayer && !pilot.IsSafe && Extensions.Distance(this, pilot) <= ShotRange);

            if (target != null && target.TryGetTarget(out var targetPlayer))
            {
                TargetMapObject = targetPlayer;
                AttackAmmunition = true;
            }
        }

        if (TargetMapObject != null)
        {
            if (LastPositionTargetMapObject != TargetMapObject.Position)
            {
                if (Extensions.Distance(this, TargetMapObject) > ShotRange)
                {
                    TargetPosition = MapWorker.RandomCircle(TargetMapObject.Position, 20);

                    LastPositionTargetMapObject = TargetMapObject.Position;
                    LastPositionTargetMapObjectCircle = false;
                }
            }
            else if (!LastPositionTargetMapObjectCircle && Extensions.Distance(this, TargetMapObject) < ShotRange)
            {
                TargetPosition = MapWorker.RandomCircle(TargetMapObject.Position, 20);

                LastPositionTargetMapObjectCircle = true;
            }
        }

        if (TargetMapObject == null && Position == TargetPosition && UnityEngine.Random.Range(0, 100) > 70)
        {
            TargetPosition = MapWorker.RandomCircle(Position, UnityEngine.Random.Range(10, 100), mapWorker.AbstractMap);
        }
    }

    public override void UpdateEvery10Second(MapWorker mapWorker)
    {
        base.UpdateEvery10Second(mapWorker);
    }

    public static void Logout(Guid pilotId, bool cancel = false)
    {
        if (Server.PilotsInGame.ContainsKey(pilotId))
        {
            var pilotMapObject = Server.PilotsInGame[pilotId];

            if (cancel || pilotMapObject.IsUnderAttack > 0)
            {
                pilotMapObject.IsWantLogout = false;
            }
            else
            {
                pilotMapObject.IsWantLogout = true;
            }
        }
    }

    public static void Reconnect(Guid pilotId)
    {
        if (Server.PilotsInGame.ContainsKey(pilotId))
        {
            var pilotMapObject = Server.PilotsInGame[pilotId];

            foreach (var item in pilotMapObject.AreaObjects)
            {
                if (item.TryGetTarget(out var mapObject))
                {
                    var spawnMapObjectResponse = new SpawnMapObjectResponse(mapObject.GetMapObject());

                    pilotMapObject.SynchronizeLocalPlayer(spawnMapObjectResponse);
                }
            }

            // TODO - bugs
            //var environments = Server.MapInstances[pilotMapObject.Pilot.Map].EnvironmentObjects.Where(e => e.AreaPlayers.Any(p => p.GetValue().Id == pilotMapObject.Id));

            //foreach (var environment in environments)
            //{
            //    var spawnEnvironmentObjectResponse = new SpawnEnvironmentObjectResponse(environment.GetEnvironmentObject(), environment.AbstractEnvironment.PrefabType);

            //    environment.SynchronizePlayer(pilotMapObject, spawnEnvironmentObjectResponse);
            //}
        }

    }
}