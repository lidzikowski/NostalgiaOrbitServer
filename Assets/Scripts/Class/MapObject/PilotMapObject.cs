using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Items;
using NostalgiaOrbitDLL.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PilotMapObject : AbstractMapObject
{
    public Pilot Pilot;
    public override Guid Id { get => Pilot.Id; set => throw new Exception("no no no"); }



    public override PrefabTypes PrefabType
    {
        get => Pilot.ShipType;
        set => Pilot.ShipType = value; // Sync
    }
    public override string ObjectName => Pilot.PilotName;
    public override Reward RewardForDead => Pilot.Ship.Reward;
    protected override float ShotRange => Pilot.LaserShotRange / 15;
    public override int RequireLogoutTime => IsSafe ? 3 : 20;
    protected override bool AutoAttackRocket => false; // TODO extras



    public PilotMapObject(Pilot pilot)
    {
        Pilot = pilot;

        IsPlayer = true;

        if (pilot.AccountType == AccountTypes.Administrator && pilot.ShipType != PrefabTypes.Admin)
        {
            pilot.ConfigureNewAdministrator();
            Server.SavePilotData(pilot);
        }

        pilot.CalculateConfiguration();
    }



    #region Position / TargetPosition / Speed
    protected override Vector2 position
    {
        get => Pilot.Position.ToVector();
        set => Pilot.Position = value.ToPositionVector();
    }

    protected override long speed
    {
        get => Pilot.Equipment_Speed;
    }
    #endregion Position / TargetPosition / Speed



    #region Hitpoints / Shields
    protected override long hitpoints
    {
        get => Pilot.HaveHitpoints;
        set => Pilot.HaveHitpoints = value;
    }

    protected override long maxHitpoints
    {
        get => Pilot.Equipment_Hitpoints;
    }



    protected override long shields
    {
        get => Pilot.HaveShields;
        set => Pilot.HaveShields = value;
    }

    protected override long maxShields
    {
        get => Pilot.Equipment_Shields;
    }

    protected override float shieldsAbsorption
    {
        get => Pilot.Equipment_ShieldsAbsorption;
    }

    public override MapObject GetMapObject()
    {
        return new MapObject(Pilot, TargetPosition.ToPositionVector());
    }
    #endregion Hitpoints / Shields



    #region Resource - Ammunition / Rocket
    protected override ResourceTypes ammunitionResource
    {
        get => Pilot.Select_Ammunition;
        set => Pilot.Select_Ammunition = value;
    }

    protected override ResourceTypes? rocketResource
    {
        get => Pilot.Select_Rocket;
        set => Pilot.Select_Rocket = value ?? ResourceTypes.Rocket1;
    }

    private Func<ResourceTypes, PilotResource> ResourceQuantity => resource => Pilot.Resources.FirstOrDefault(o => o.ResourceType == resource);

    protected override void OnResourceUpdate(ResourceTypes resource)
    {
        if (!AreaObjects.Any())
            return;

        var resourceUpdateResponse = new ResourceUpdateResponse(new PilotResource(resource, ResourceQuantity.Invoke(resource).Quantity));

        Debug.Log($"{Id} OnResourceUpdate");

        SynchronizeLocalPlayer(resourceUpdateResponse);
    }
    protected override void OnResourceUpdate(List<ResourceTypes> resources)
    {
        if (!AreaObjects.Any())
            return;

        var list = new List<PilotResource>();

        foreach (var resource in resources)
        {
            list.Add(new PilotResource(resource, ResourceQuantity.Invoke(resource).Quantity));
        }

        var resourceUpdateResponse = new ResourceUpdateResponse(list);

        Debug.Log($"{Id} OnResourceUpdate list");

        SynchronizeLocalPlayer(resourceUpdateResponse);
    }
    #endregion Resource - Ammunition / Rocket



    protected override bool CanAttackAmmunition
    {
        get
        {
            var resource = ResourceQuantity(AmmunitionResource);

            if (resource == null)
                return false;

            return resource.Quantity >= Pilot.LasersQuantityInCurrentConfiguration;
        }
    }
    protected override bool CanAttackRocket
    {
        get
        {
            var resource = ResourceQuantity(RocketResource.Value);

            if (resource == null)
                return false;

            return resource.Quantity >= 1;
        }
    }



    protected override long LaserDamage => MapWorker.RandomDamage(Convert.ToInt64(Pilot.Equipment_Damage * GetLaserMultiply));
    private float GetLaserMultiply
    {
        get
        {
            return AmmunitionResource switch
            {
                ResourceTypes.Ammunition1 => 1,
                ResourceTypes.Ammunition2 => 2,
                ResourceTypes.Ammunition3 => 3,
                ResourceTypes.Ammunition4 => 4,
                ResourceTypes.AmmunitionSab => 2,
                _ => throw new NotImplementedException(),
            };
        }
    }
    protected override long RocketDamage => MapWorker.RandomDamage(AbstractResource.GetResourceByType(rocketResource.Value).Damage);
    protected override int RocketCountdown => (Pilot.PremiumStatus ? 2 : 4) * Application.targetFrameRate; // TODO turbo mina

    protected override float ShieldAbsorption => Pilot.Equipment_ShieldsAbsorption;

    private AbstractItem GetRepairRobot
    {
        get
        {
            AbstractItem abstractItem = default;
            Pilot.Items.FirstOrDefault(o =>
            {
                if ((Pilot.ConfigurationFirst && o.IsEquipConfiguration1) || (!Pilot.ConfigurationFirst && o.IsEquipConfiguration2))
                {
                    abstractItem = AbstractItem.GetItemByType(o.ItemType);
                    if (abstractItem.IsExtras && abstractItem.ExtrasCanRepair)
                        return true;
                }
                return false;
            });
            return abstractItem;
        }
    }
    protected override bool CanHitpointsRepair => GetRepairRobot != null;
    protected override int HitpointsRepairTime => GetRepairRobot.ExtrasRepairTime;


    public override void TakeReward(Reward reward, string mapObject)
    {
        Pilot.ApplyReward(reward);

        Server.SavePilotData(Pilot);

        var rewardResponse = new RewardResponse(reward, mapObject);

        Debug.Log($"{Id} TakeReward");

        SynchronizeLocalPlayer(rewardResponse);
    }
    public List<Cargo> TakeCargo(Reward reward, string environmentObject = nameof(EnvironmentObject))
    {
        var result = Pilot.ApplyCargo(reward.Cargos);

        Server.SavePilotData(Pilot);

        foreach (var res in result)
        {
            foreach (var item in reward.Cargos)
            {
                if (res.Resource == item.Resource)
                {
                    item.Quantity -= res.Quantity;
                }
            }
        }

        var rewardResponse = new RewardResponse(reward, environmentObject);

        Debug.Log($"{Id} TakeCargo");

        SynchronizeLocalPlayer(rewardResponse);

        return result;
    }


    #region Events
    public void OnSelectTargetCommand(SelectTargetCommand command)
    {
        if (command.TargetId.HasValue)
        {
            if (TargetMapObject == null)
            {
                SelectTargetCommand(command);
            }
            else
            {
                if (TargetMapObject.Id != command.TargetId.Value)
                {
                    SelectTargetCommand(command);
                }
            }

            if (command.AttackAmmunition.HasValue)
            {
                if (command.AttackAmmunition.Value && TargetMapObject != null && !TargetMapObject.IsSafe)
                {
                    AttackAmmunition = true;
                }
                else
                {
                    AttackAmmunition = false;
                }
            }

            if (command.AttackRocket.HasValue)
            {
                if (command.AttackRocket.Value && TargetMapObject != null && !TargetMapObject.IsSafe)
                {
                    AttackRocket = true;
                }
                else
                {
                    AttackRocket = false;
                }
            }
        }
    }
    private void SelectTargetCommand(SelectTargetCommand command)
    {
        var mapObject = Server.MapInstances[Pilot.Map].MapObjects.FirstOrDefault(o => o.Id == command.TargetId.Value);
        if (mapObject != null && mapObject.Id != Id && !mapObject.IsDead)
        {
            TargetMapObject = mapObject;
        }
    }

    public void OnChooseResourceCommand(ChooseResourceCommand command)
    {
        if (command.Ammunition.HasValue)
        {
            ammunitionResource = command.Ammunition.Value;
        }

        if (command.Rocket.HasValue)
        {
            rocketResource = command.Rocket.Value;
        }

        // DB Update
    }
    #endregion Events

    public bool RepairShip()
    {
        if (Pilot.HaveHitpoints > 0)
            return false;

        IsDead = false;
        Hitpoints = 1000;

        return true;
    }

    public void ChangeMap(MapTypes mapType, Vector2 position)
    {
        IsUnderAttack = 0;
        Pilot.Map = mapType;
        TargetPosition = this.position = position;

        SynchronizeLocalPlayer(new ChangeMapResponse(mapType));
    }
}