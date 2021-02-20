using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Environments;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class AbstractEnvironmentObject
{
    public Guid Id { get; protected set; }
    public AbstractEnvironment AbstractEnvironment { get; protected set; }

    public List<WeakReference<AbstractMapObject>> AreaPlayers = new List<WeakReference<AbstractMapObject>>();
    protected Func<AbstractMapObject, bool> ContainPlayer => (x) => AreaPlayers.Any(o => o.TryGetTarget(out var result) && result.Id == x.Id);
    public List<Cargo> ExtraCargos { get; set; }



    public virtual PositionVector PositionObj { get; set; }
    public virtual Vector2 Position { get; set; }
    public PrefabTypes PrefabType { get; set; }


    public virtual Guid? OwnerId { get; set; }
    private float collectingTime;
    public bool StartCollecting { get; set; }


    public void LateUpdate(MapWorker mapWorker)
    {
        if (!OwnerId.HasValue)
            return;

        if (!StartCollecting)
            return;

        if (Server.PilotsInGame.ContainsKey(OwnerId.Value))
        {
            var pilot = Server.PilotsInGame[OwnerId.Value];

            if (pilot.Position == Position)
            {
                if (AbstractEnvironment.CollectingTime > 0)
                    collectingTime += Time.deltaTime;

                if (collectingTime >= AbstractEnvironment.CollectingTime)
                {
                    OwnerId = default;

                    Reward reward = new Reward();
                    if (AbstractEnvironment.RandomCargoReward)
                    {
                        reward.Cargos = new List<Cargo>() { AbstractEnvironment.Cargos[UnityEngine.Random.Range(0, AbstractEnvironment.Cargos.Count - 1)] };
                    }
                    else if (ExtraCargos?.Any() ?? false)
                    {
                        reward.Cargos = ExtraCargos;
                    }
                    else
                    {
                        reward.Cargos = AbstractEnvironment.Cargos.DeepClone();
                    }

                    var result = pilot.TakeCargo(reward);

                    if (result.Any())
                    {
                        ExtraCargos = result;
                    }
                    else
                    {
                        mapWorker.DestroyEnvironment(this);
                    }
                }
            }
            else
            {
                collectingTime = 0;
                OwnerId = default;
                StartCollecting = default;
            }
        }
        else
        {
            collectingTime = 0;
            OwnerId = default;
            StartCollecting = default;
        }
    }


    private bool changePrefab;
    private int timeToChangePrefab;
    private int timeToDead;
    public void UpdateEverySecond(MapWorker mapWorker)
    {
        if (AbstractEnvironment.OccupiedLifeTime.HasValue && !changePrefab)
        {
            timeToChangePrefab++;

            if (timeToChangePrefab > AbstractEnvironment.OccupiedLifeTime.Value)
            {
                changePrefab = true;

                PrefabType = AbstractEnvironment.PrefabType;

                OwnerId = default;

                OnChangeEnvironmentObject();
            }
        }
        else if (AbstractEnvironment.LifeTime.HasValue)
        {
            timeToDead++;

            if (timeToDead > AbstractEnvironment.LifeTime.Value)
            {
                mapWorker.DestroyEnvironment(this);
            }
        }
    }

    public void OnChangeEnvironmentObject()
    {
        var changeEnvironmentObjectResponse = new ChangeEnvironmentObjectResponse(Id, PrefabType);

        SynchronizeWithPlayers(changeEnvironmentObjectResponse);
    }



    public EnvironmentObject GetEnvironmentObject()
    {
        return new EnvironmentObject(Id, AbstractEnvironment, PositionObj, PrefabType, OwnerId);
    }

    public void AddEnvironmentObjectToSynchronize(AbstractMapObject mapObject)
    {
        if (ContainPlayer(mapObject))
            return;

        AreaPlayers.Add(new WeakReference<AbstractMapObject>(mapObject));

        var spawnEnvironmentObjectResponse = new SpawnEnvironmentObjectResponse(GetEnvironmentObject(), AbstractEnvironment.PrefabType);

        SynchronizePlayer(mapObject, spawnEnvironmentObjectResponse);
    }

    public void RemoveEnvironmentObjectToSynchronize(AbstractMapObject mapObject)
    {
        if (!ContainPlayer(mapObject))
            return;

        var mapObj = AreaPlayers.First(o => o.GetValue()?.Id == mapObject.Id);
        AreaPlayers.Remove(mapObj);

        var disposeEnvironmentObjectResponse = new DisposeEnvironmentObjectResponse(Id);

        SynchronizePlayer(mapObject, disposeEnvironmentObjectResponse);
    }

    protected void SynchronizeWithPlayers(AbstractResponse abstractResponse)
    {
        byte[] data = DLLHelpers.Serialize(abstractResponse);

        foreach (var weakPlayer in AreaPlayers)
        {
            var player = weakPlayer.GetValue();

            if (player == null)
                continue;

            var pilot = Server.PilotsInGame[player.Id];
            var pilotSession = Server.PilotSessions.FirstOrDefault(o => o.PilotId == pilot.Id);

            if (pilotSession != null && pilotSession.ChannelSocketId.ContainsKey(ServerChannels.Game))
                AbstractService.SendToSocket(GameService.ChannelName, pilotSession.ChannelSocketId[ServerChannels.Game], data);
        }
    }
    public void SynchronizePlayer(AbstractMapObject abstractMapObject, AbstractResponse abstractResponse)
    {
        AbstractService.SendToSocket(GameService.ChannelName, abstractMapObject.SocketId, abstractResponse);
    }
}