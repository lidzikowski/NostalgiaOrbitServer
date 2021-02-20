using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class AbstractMapObject
{
    public virtual Guid Id { get; set; }
    public bool IsPlayer { get; set; }
    public string SocketId
    {
        get
        {
            var pilot = Server.PilotSessions.FirstOrDefault(o => o.PilotId == Id);

            if (pilot != null && pilot.ChannelSocketId.ContainsKey(ServerChannels.Game))
                return pilot.ChannelSocketId[ServerChannels.Game];

            return null;
        }
    }



    public List<WeakReference<AbstractMapObject>> AreaObjects = new List<WeakReference<AbstractMapObject>>();
    protected Func<AbstractMapObject, bool> ContainObject => (x) => AreaObjects.Any(o => o.TryGetTarget(out var result) && result.Id == x.Id);

    public virtual PrefabTypes PrefabType { get; set; }
    public abstract string ObjectName { get; }
    public abstract Reward RewardForDead { get; }



    #region Position / TargetPosition / Speed
    protected virtual Vector2 position { get; set; }
    public Vector2 Position
    {
        get => position;
        set
        {
            if (position == value)
                return;

            position = value;
            OnPositionUpdate();
        }
    }

    protected virtual Vector2 targetPosition { get; set; }
    public Vector2 TargetPosition
    {
        get => targetPosition;
        set
        {
            if (targetPosition == value)
                return;

            targetPosition = value;
            OnPositionUpdate();
        }
    }

    public virtual void Update(MapWorker mapWorker)
    {
        AttackUpdate();

        if (Position != TargetPosition)
        {
            Position = Vector2.MoveTowards(Position, TargetPosition, Time.deltaTime * (Speed / 20));
        }
    }

    public virtual void UpdateEvery1Second(MapWorker mapWorker)
    {

    }
    public virtual void UpdateEvery10Second(MapWorker mapWorker)
    {

    }

    protected virtual long speed { get; }
    public long Speed
    {
        get => speed;
    }

    private void OnPositionUpdate()
    {
        var changeMapObjectPositionResponse = new ChangeMapObjectPositionResponse(Id, Position.ToPositionVector(), TargetPosition.ToPositionVector(), Speed);

        SynchronizeWithPlayers(changeMapObjectPositionResponse, true);
    }
    #endregion Position / TargetPosition / Speed



    #region Hitpoints / Shields
    protected virtual long hitpoints { get; set; }

    public long Hitpoints
    {
        get => hitpoints;
        set
        {
            if (hitpoints == value)
                return;

            hitpoints = value;
        }
    }

    protected virtual long maxHitpoints { get; }
    public long MaxHitpoints
    {
        get => maxHitpoints;
    }



    protected virtual long shields { get; set; }
    public long Shields
    {
        get => shields;
        set
        {
            if (shields == value)
                return;

            shields = value;
        }
    }

    protected virtual long maxShields { get; set; }
    public long MaxShields
    {
        get => maxShields;
    }

    protected virtual float shieldsAbsorption { get; }
    public float ShieldsAbsorption
    {
        get => shieldsAbsorption;
    }

    private void OnLifeUpdate(long hitpoints, long shields)
    {
        Hitpoints = hitpoints;
        Shields = shields;

        var changeLifeResponse = new ChangeLifeResponse(Id, Hitpoints, MaxHitpoints, Shields, MaxShields);

        SynchronizeWithPlayers(changeLifeResponse, true);
    }
    #endregion Hitpoints / Shields



    #region TargetObject / TargetAttack
    private AbstractMapObject targetObject;
    public AbstractMapObject TargetObject
    {
        get => targetObject;
        set
        {
            targetObject = value;
        }
    }

    private int isUnderAttack;
    /// <summary>
    /// When == 0 -> can Repair / Change map on pvp map
    /// </summary>
    public int IsUnderAttack
    {
        get => isUnderAttack;
        set
        {
            if (isUnderAttack == value)
                return;

            isUnderAttack = value;
        }
    }

    private bool isSafe;
    /// <summary>
    /// Is safe on Portals / Bases
    /// </summary>
    public bool IsSafe
    {
        get => isSafe;
        set
        {
            if (isSafe == value)
                return;

            isSafe = value;

            OnSafeZone();
        }
    }

    private void OnSafeZone()
    {
        var safeZoneResponse = new SafeZoneResponse(Id, IsSafe);

        SynchronizeLocalPlayer(safeZoneResponse);
    }
    #endregion TargetObject / TargetAttack



    #region Resource - Ammunition / Rocket
    protected virtual ResourceTypes ammunitionResource { get; set; }
    public ResourceTypes AmmunitionResource
    {
        get => ammunitionResource;
        set
        {
            if (ammunitionResource == value)
                return;

            ammunitionResource = value;
            OnResourceUpdate(value);
        }
    }

    protected virtual ResourceTypes? rocketResource { get; set; }
    public ResourceTypes? RocketResource
    {
        get => rocketResource;
        set
        {
            if (rocketResource == value)
                return;

            rocketResource = value;
            if (value != null)
                OnResourceUpdate(value.Value);
        }
    }

    protected virtual void OnResourceUpdate(ResourceTypes resource)
    {

    }
    protected virtual void OnResourceUpdate(List<ResourceTypes> resources)
    {

    }
    #endregion Resource - Ammunition / Rocket



    #region Target / AttackAmmunition / AttackRocket / Attackers / IsDead
    protected AbstractMapObject targetMapObject { get; set; }
    public AbstractMapObject TargetMapObject
    {
        get => targetMapObject;
        set
        {
            if (targetMapObject == value)
                return;

            attackAmmunition = false;
            attackRocket = false;

            targetMapObject = value;
            OnAttackResponse(null);
        }
    }

    protected abstract bool CanAttackAmmunition { get; }
    protected bool attackAmmunition { get; set; }
    public bool AttackAmmunition
    {
        get => attackAmmunition;
        set
        {
            if (attackAmmunition == value)
                return;

            attackAmmunition = value;
            OnAttackResponse(null);
        }
    }

    protected abstract bool CanAttackRocket { get; }
    protected abstract bool AutoAttackRocket { get; }
    protected bool attackRocket { get; set; }
    public bool AttackRocket
    {
        get => attackRocket;
        set
        {
            if (attackRocket == value)
                return;

            attackRocket = value;
            OnAttackResponse(null);
        }
    }

    public void OnAttackResponse(ResourceTypes? resource, bool withLocalplayer = false)
    {
        if (!AreaObjects.Any() && !withLocalplayer)
            return;

        var attackResponse = new AttackResponse(Id, TargetMapObject?.Id, resource);

        SynchronizeWithPlayers(attackResponse, withLocalplayer);
    }

    public List<AbstractMapObject> attackers { get; } = new List<AbstractMapObject>();
    private Dictionary<Guid, int> attackersTime { get; set; } = new Dictionary<Guid, int>();
    public AbstractMapObject AttackOwner => attackers.FirstOrDefault(o => o.Id == attackersTime.FirstOrDefault().Key);
    public AbstractMapObject AttackPlayerOwner => attackers.FirstOrDefault(o => o.IsPlayer && o.Id == attackersTime.FirstOrDefault().Key);

    public void OnAddAttacker(AbstractMapObject abstractMapObject)
    {
        if (attackers.Contains(abstractMapObject))
        {
            attackersTime[abstractMapObject.Id] = 10;
            return;
        }

        attackers.Add(abstractMapObject);
        attackersTime.Add(abstractMapObject.Id, 10);

        if (!IsPlayer)
        {
            targetMapObject = abstractMapObject;
            AttackAmmunition = true;
            //AttackRocket = true;
        }
    }

    public void OnRemoveAttacker(Guid guid)
    {
        var attacker = attackers.FirstOrDefault(o => o.Id == guid);

        if (attacker == null)
            return;

        attackers.Remove(attacker);
        attackersTime.Remove(attacker.Id);

        // Can logout etc
    }


    public virtual bool IsDead { get; set; }

    public void OnDead()
    {
        GiveRewardOnDead();

        if (!AreaObjects.Any())
            return;

        var destroyMapObjectResponse = new DestroyMapObjectResponse(Id, AttackOwner?.Id, AttackOwner?.ObjectName);

        SynchronizeWithPlayers(destroyMapObjectResponse, false);
    }

    private void GiveRewardOnDead()
    {
        AttackPlayerOwner?.TakeReward(RewardForDead, ObjectName);
    }

    public abstract void TakeReward(Reward reward, string mapObject);
    #endregion Target / AttackAmmunition / AttackRocket / Attackers / IsDead



    #region Laser / Rocket / Attacks / Deal damage / Receive damage / On dead event - give reward
    protected abstract long LaserDamage { get; }
    protected abstract long RocketDamage { get; }
    protected abstract int RocketCountdown { get; }

    protected abstract float ShieldAbsorption { get; }


    protected abstract float ShotRange { get; }

    public virtual void AttackUpdate()
    {
        if (IsDead)
            return;

        if (shotDelay > 0)
            shotDelay--;

        if (rocketDelay > 0)
            rocketDelay--;

        if ((AttackAmmunition || AttackRocket || AutoAttackRocket) && TargetMapObject != null && Extensions.Distance(this, TargetMapObject) <= ShotRange)
        {
            if (TargetMapObject.IsSafe)
            {
                attackRocket = false;
                AttackAmmunition = false;
            }
            else
            {
                UseAmmunition();
                UseRocket();
            }
        }
        else
        {
            if (IsUnderAttack == 0)
            {
                if (repairTimer >= Server.RepairDelay)
                {
                    repairTimer = 0;
                    Repair();
                }
                else
                    repairTimer++;
            }
        }

        if (IsUnderAttack > 0)
        {
            IsUnderAttack--;
        }

        foreach (var attacker in attackersTime.ToList())
        {
            if (attacker.Value == 0)
            {
                OnRemoveAttacker(attacker.Key);
            }
            else
            {
                attackersTime[attacker.Key]--;
            }
        }
    }

    private int shotDelay;
    private void UseAmmunition()
    {
        if (AttackAmmunition && shotDelay == 0)
        {
            if (CanAttackAmmunition)
            {
                IsSafe = false;
                shotDelay = Server.ShotDelay;
                DealDamage(LaserDamage, AmmunitionResource);
            }
        }
    }

    private int rocketDelay;
    private void UseRocket()
    {
        if ((AttackRocket || AutoAttackRocket) && rocketDelay == 0)
        {
            if (CanAttackRocket)
            {
                IsSafe = false;
                rocketDelay = RocketCountdown;
                DealDamage(RocketDamage, RocketResource.Value);

                if (!AutoAttackRocket)
                    attackRocket = false;
            }
        }
    }

    private void DealDamage(long damage, ResourceTypes resourceType)
    {
        TargetMapObject.ReceiveDamage(this, damage, resourceType);

        OnAttackResponse(resourceType, true);

        if (TargetMapObject.IsDead)
        {
            TargetMapObject = null;
        }
    }

    private void ReceiveDamage(AbstractMapObject abstractMapObject, long damage, ResourceTypes resourceType)
    {
        OnAddAttacker(abstractMapObject);

        IsUnderAttack = Server.UnderAttackDelay;

        TakeDamage(abstractMapObject, damage, resourceType);
    }

    private void TakeDamage(AbstractMapObject abstractMapObject, long damage, ResourceTypes resourceType)
    {
        long dmgHp = 0;

        long hitpoints = Hitpoints;
        long shields = Shields;

        if (DLLHelpers.IsAmmunitionType(resourceType) && AbstractResource.GetResourceByType(resourceType).ShotInOnlyShield)
        {
            long repairShd = 0;

            if (shields > 0 && ShieldAbsorption > 0)
            {
                if (shields - damage >= 0)
                {
                    //if (IsPlayer)
                    //    (this as PilotServer).AddAchievement(o => o.ShieldDestroy, dmgShd);

                    repairShd = damage;
                    shields -= damage;
                }
                else
                {
                    //if (IsPlayer)
                    //    (this as PilotServer).AddAchievement(o => o.ShieldDestroy, Shields);

                    repairShd = damage - shields;
                    shields = 0;
                }
            }

            if (repairShd > 0 && abstractMapObject.Shields < abstractMapObject.MaxShields)
            {
                long objectShd = abstractMapObject.Shields + repairShd;
                long repair = 0;
                if (objectShd >= abstractMapObject.MaxShields)
                {
                    repair = objectShd;
                }
                else
                {
                    repair = abstractMapObject.MaxShields - abstractMapObject.Shields;
                }

                if (repair > 0)
                    abstractMapObject.OnLifeUpdate(abstractMapObject.Hitpoints, repair);
            }
        }
        else
        {
            if (shields > 0 && ShieldAbsorption > 0)
            {
                long dmgShd = Convert.ToInt64(damage * ShieldAbsorption);
                if (shields - dmgShd >= 0)
                {
                    //if (IsPlayer)
                    //    (this as PilotServer).AddAchievement(o => o.ShieldDestroy, dmgShd);

                    dmgHp = damage - dmgShd;
                    shields -= dmgShd;
                }
                else
                {
                    //if (IsPlayer)
                    //    (this as PilotServer).AddAchievement(o => o.ShieldDestroy, Shields);

                    dmgHp = damage - shields;
                    shields = 0;
                }
            }
            else
                dmgHp = damage;

            if (hitpoints - dmgHp >= 0)
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.HitpointDestroy, dmgHp);

                hitpoints -= dmgHp;
            }
            else
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.HitpointDestroy, Hitpoints);

                hitpoints = 0;
            }
        }

        OnLifeUpdate(hitpoints, shields);

        if (hitpoints <= 0)
        {
            IsDead = true;
            IsUnderAttack = 0;
        }
    }
    #endregion Laser / Rocket / Attacks / Deal damage / Receive damage / On dead event - give reward

    protected abstract bool CanHitpointsRepair { get; }
    protected abstract int HitpointsRepairTime { get; }
    protected static int ShieldRepairTime = 20;

    private int repairTimer;
    private void Repair()
    {
        long hitpoints = Hitpoints;
        long shields = Shields;
        bool repair = false;

        if (CanHitpointsRepair && hitpoints < MaxHitpoints)
        {
            long hitpoint = Convert.ToInt64(MaxHitpoints / HitpointsRepairTime);

            if (hitpoints + hitpoint <= MaxHitpoints)
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.HitpointRepair, hitpoint);

                hitpoints += hitpoint;
            }
            else
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.HitpointRepair, MaxHitpoints - Hitpoints);

                hitpoints = MaxHitpoints;
            }

            repair = true;
        }

        if (shields > MaxShields)
        {
            shields = MaxShields;

            repair = true;
        }
        else if (shields < MaxShields)
        {
            long shield = Convert.ToInt64(MaxShields / ShieldRepairTime);
            if (shields + shield <= MaxShields)
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.ShieldRepair, shield);

                shields += shield;
            }
            else
            {
                //if (IsPlayer)
                //    (this as PilotServer).AddAchievement(o => o.ShieldRepair, MaxShields - Shields);

                shields = MaxShields;
            }

            repair = true;
        }

        if (repair)
            OnLifeUpdate(hitpoints, shields);
    }




    #region Logout section for player
    private bool isWantLogout;
    public bool IsWantLogout
    {
        get => isWantLogout;
        set
        {
            if (isWantLogout == value)
                return;

            isWantLogout = value;
            logoutTimer = 0;

            if (IsPlayer)
            {
                var logoutResponse = new LogoutResponse(IsWantLogout, RequireLogoutTime, LogoutTimer);

                SynchronizeLocalPlayer(logoutResponse);
            }
        }
    }
    public virtual int RequireLogoutTime { get; }
    protected int logoutTimer;
    public int LogoutTimer
    {
        get => logoutTimer;
        set
        {
            if (logoutTimer == value)
                return;

            logoutTimer = value;

            if (IsPlayer && IsWantLogout)
            {
                var logoutResponse = new LogoutResponse(IsWantLogout, RequireLogoutTime, value);

                SynchronizeLocalPlayer(logoutResponse);
            }
        }
    }
    #endregion

    public abstract MapObject GetMapObject();
    public void AddMapObjectToSynchronize(AbstractMapObject mapObject, bool relation = true)
    {
        if (!IsPlayer && !mapObject.IsPlayer)
            return;

        if (ContainObject(mapObject))
            return;

        AreaObjects.Add(new WeakReference<AbstractMapObject>(mapObject));
        if (relation)
            mapObject.AddMapObjectToSynchronize(this, false);

        var spawnMapObjectResponse = new SpawnMapObjectResponse(mapObject.GetMapObject());

        SynchronizeLocalPlayer(spawnMapObjectResponse);
    }

    public void RemoveMapObjectToSynchronize(AbstractMapObject mapObject, bool relation = true)
    {
        if (!ContainObject(mapObject))
            return;

        var mapObj = AreaObjects.First(o => o.GetValue()?.Id == mapObject.Id);
        AreaObjects.Remove(mapObj);
        if (relation)
            mapObject.RemoveMapObjectToSynchronize(this, false);

        if (TargetMapObject == mapObj.GetValue())
        {
            TargetMapObject = null;
        }

        var disposeMapObjectResponse = new DisposeMapObjectResponse(mapObject.Id, false);

        SynchronizeLocalPlayer(disposeMapObjectResponse);
    }



    protected void SynchronizeWithPlayers(AbstractResponse abstractResponse, bool withLocalPlayer)
    {
        byte[] data = DLLHelpers.Serialize(abstractResponse);

        foreach (var weakPlayer in AreaObjects)
        {
            var player = weakPlayer.GetValue();

            if (player == null || !player.IsPlayer)
                continue;

            var pilot = Server.PilotsInGame[player.Id];
            var pilotSession = Server.PilotSessions.FirstOrDefault(o => o.PilotId == pilot.Id);

            if (pilotSession != null && pilotSession.ChannelSocketId.ContainsKey(ServerChannels.Game))
                AbstractService.SendToSocket(GameService.ChannelName, pilotSession.ChannelSocketId[ServerChannels.Game], data);
        }

        if (withLocalPlayer)
            SynchronizeLocalPlayer(data);
    }
    public void SynchronizeLocalPlayer(AbstractResponse abstractResponse)
    {
        if (IsPlayer)
        {
            SynchronizeLocalPlayer(DLLHelpers.Serialize(abstractResponse));
        }
    }
    protected void SynchronizeLocalPlayer(byte[] data)
    {
        if (IsPlayer)
        {
            AbstractService.SendToSocket(GameService.ChannelName, SocketId, data);
        }
    }
}