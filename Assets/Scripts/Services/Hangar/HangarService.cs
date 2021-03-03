using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Responses;
using System;
using System.Linq;
using UnityEngine;
using WebSocketSharp;

public class HangarService : AbstractService
{
    public static string ChannelName = $"/{ServerChannels.Hangar}";

    public HangarService()
    {
        Channel = ServerChannels.Hangar;
        ChannelString = ChannelName;
    }

    protected override void OnOpen()
    {
        MainThread.Instance().Enqueue(() =>
        {
            var jwt = Context.CookieCollection[nameof(AbstractCommand.JWToken)].Value;

            if (VerifyJWT(jwt, out var payload))
            {
                AddOrUpdateSession(jwt, payload);
            }
            else
            {
                Disconnected(ChannelString, ID, CloseStatusCode.InvalidData, "InvalidSignatureVerification");
            }
        });

        base.OnOpen();
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var command = DLLHelpers.Deserialize<AbstractCommand>(e.RawData);

        if (command == null)
            return;

        Debug.Log($"{Channel} -> OnMessage: {command.GetType().Name}");

        ExecuteCommand<LogoutCommand>(command, OnLogoutCommand);
        ExecuteCommand<PilotDataCommand>(command, OnPilotDataCommand);
        ExecuteCommand<OnlinePlayersCommand>(command, OnOnlinePlayersCommand);

        //base.OnMessage(e);
    }

    private void OnLogoutCommand(LogoutCommand command, Guid pilotId)
    {
        if (command.LogoutType == LogoutTypes.FromHome)
        {
            ClearSession();
            Disconnected(ChannelString, ID, CloseStatusCode.Normal, "LogoutCommand");
        }
    }

    private async void OnPilotDataCommand(PilotDataCommand command, Guid pilotId)
    {
        Pilot pilot;
        if (!Server.PilotsInGame.ContainsKey(pilotId))
            pilot = await Server.Database.PilotFindByGuid(pilotId);
        else
            pilot = Server.PilotsInGame[pilotId].Pilot;

        pilot.CalculateConfiguration();

        pilot.RemovePassword();
        SendToSocket(new PilotDataResponse(pilot), command);
    }

    private void OnOnlinePlayersCommand(OnlinePlayersCommand command, Guid pilotId)
    {
        SendToSocket(new OnlinePlayersResponse(Server.PilotSessions.Count(), 0), command);
    }
}