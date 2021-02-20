using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Exceptions;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Maps;
using System;
using System.Linq;
using UnityEngine;
using WebSocketSharp;

public class GameService : AbstractService
{
    public static string ChannelName = $"/{ServerChannels.Game}";

    public GameService()
    {
        Channel = ServerChannels.Game;
        ChannelString = ChannelName;
    }

    protected override void OnOpen()
    {
        MainThread.Instance().Enqueue(async () =>
        {
            var jwt = Context.CookieCollection[nameof(AbstractCommand.JWToken)].Value;

            if (VerifyJWT(jwt, out var payload))
            {
                AddOrUpdateSession(jwt, payload);

                var pilotId = GetPilotIdFromPayload(payload);

                if (!Server.PilotsInGame.ContainsKey(pilotId))
                {
                    var pilot = await Server.Database.PilotFindByGuid(GetPilotIdFromPayload(payload));

                    pilot.RemovePassword();

                    var pilotMapObject = new PilotMapObject(pilot);
                    Server.PilotsInGame.Add(pilot.Id, pilotMapObject);

                    Server.MapInstances[pilot.Map].PilotJoinToMap(pilotMapObject, pilotMapObject.Position);
                }
                else
                {
                    Server.PilotsInGame[pilotId].IsWantLogout = false;
                    Server.PilotsInGame[pilotId].LogoutTimer = 0;
                }

                SendToSocket(new JoinToMapResponse(Server.PilotsInGame[pilotId].Pilot));
            }
            else
            {
                Disconnected(ChannelString, ID, CloseStatusCode.InvalidData, "InvalidSignatureVerification");
            }
        });

        base.OnOpen();
    }

    protected override void OnClose(CloseEventArgs e)
    {
        MainThread.Instance().Enqueue(() =>
        {
            var pilotSession = Server.PilotSessions.FirstOrDefault(o => o.ChannelSocketId.ContainsKey(Channel) && o.ChannelSocketId[Channel].Contains(ID));

            if (pilotSession != null)
            {
                if (Server.PilotsInGame.ContainsKey(pilotSession.PilotId))
                {
                    var pilotMapObject1 = Server.PilotsInGame[pilotSession.PilotId];

                    pilotMapObject1.IsWantLogout = true;
                    pilotMapObject1.LogoutTimer = 0;

                    Debug.Log($"OnClose : {pilotMapObject1.Id} want logout.");
                }
            }

            base.OnClose(e);
        });
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var command = DLLHelpers.Deserialize<AbstractCommand>(e.RawData);

        if (command == null)
            return;

        Debug.Log($"{Channel} -> OnMessage: {command.GetType().Name}");

        //ExecuteCommand<LogoutCommand>(command, OnLogoutCommand);
        ExecuteCommand<PilotDataCommand>(command, OnPilotDataCommand);
        ExecuteCommand<ChangePositionCommand>(command, OnChangePositionCommand);
        ExecuteCommand<ChooseResourceCommand>(command, OnChooseResourceCommand);
        ExecuteCommand<SelectTargetCommand>(command, OnSelectTargetCommand);
        ExecuteCommand<UseJumpPortalCommand>(command, OnUseJumpPortalCommand);
        ExecuteCommand<CollectEnvironmentCommand>(command, OnCollectEnvironmentCommand);

        //base.OnMessage(e);
    }

    private void OnCollectEnvironmentCommand(CollectEnvironmentCommand command, Guid pilotId)
    {
        var pilot = Server.PilotsInGame[pilotId];
        var mapWorker = Server.MapInstances[pilot.Pilot.Map];

        mapWorker.CollectEnvironment(command.EnvironmentId, pilot);
    }

    private void OnUseJumpPortalCommand(UseJumpPortalCommand command, Guid pilotId)
    {
        var pilot = Server.PilotsInGame[pilotId];
        var map = AbstractMap.GetMapByType(pilot.Pilot.Map);

        foreach (var portal in map.Portals)
        {
            if (FindPortal(portal))
                return;
        }

        SendToSocket(new ChangeMapResponse()
        {
            Exceptions = NostalgiaOrbitException.One(new PortalNotFoundException())
        }, command);

        bool FindPortal(Portal portal)
        {
            if (Vector2.Distance(pilot.Pilot.Position.ToVector(), portal.Position.ToVector()) <= Portal.JumpDistance)
            {
                var mapInstance = Server.MapInstances[pilot.Pilot.Map];

                if (portal.RandomMapAndPosition)
                {
                    if (mapInstance.CanPilotLeaveFromMap(pilot))
                    {
                        MapTypes targetRandomMap;
                        do
                        {
                            targetRandomMap = RandomMap();
                            Debug.LogWarning($"Random map is {targetRandomMap}");
                        }
                        while (!Server.MapInstances[targetRandomMap].CanPilotJoinToMap(pilot));

                        mapInstance.PilotLeaveFromMap(pilot);
                        Server.MapInstances[targetRandomMap].PilotJoinToMap(pilot, pilot.Position);

                        SendToSocket(new ChangeMapResponse(targetRandomMap), command);
                        return true;

                        MapTypes RandomMap()
                        {
                            var keys = Server.MapInstances.Keys.ToList();
                            return keys[UnityEngine.Random.Range(0, keys.Count - 1)];
                        }
                    }
                    else
                    {
                        SendToSocket(new ChangeMapResponse()
                        {
                            Exceptions = NostalgiaOrbitException.One(new PilotIsAttackedException())
                        }, command);
                        return true;
                    }
                }
                else if (Server.MapInstances[portal.Target_MapType].CanPilotJoinToMap(pilot))
                {
                    if (mapInstance.CanPilotLeaveFromMap(pilot))
                    {
                        mapInstance.PilotLeaveFromMap(pilot);
                        Server.MapInstances[portal.Target_MapType].PilotJoinToMap(pilot, portal.Target_Position.ToVector());

                        SendToSocket(new ChangeMapResponse(portal.Target_MapType), command);
                        return true;
                    }
                    else
                    {
                        SendToSocket(new ChangeMapResponse()
                        {
                            Exceptions = NostalgiaOrbitException.One(new PilotIsAttackedException())
                        }, command);
                        return true;
                    }
                }
                else
                {
                    SendToSocket(new ChangeMapResponse()
                    {
                        Exceptions = NostalgiaOrbitException.One(new PortalRequiredLevelException())
                    }, command);
                    return true;
                }
            }
            return false;
        }
    }

    private void OnSelectTargetCommand(SelectTargetCommand command, Guid pilotId)
    {
        Server.PilotsInGame[pilotId].OnSelectTargetCommand(command);
    }

    private void OnChooseResourceCommand(ChooseResourceCommand command, Guid pilotId)
    {
        Server.PilotsInGame[pilotId].OnChooseResourceCommand(command);
    }

    private void OnChangePositionCommand(ChangePositionCommand command, Guid pilotId)
    {
        Server.PilotsInGame[pilotId].TargetPosition = command.TargetPosition.ToVector();
    }

    private void OnPilotDataCommand(PilotDataCommand command, Guid pilotId)
    {
        SendToSocket(new PilotDataResponse(Server.PilotsInGame[pilotId].Pilot), command);
    }
}