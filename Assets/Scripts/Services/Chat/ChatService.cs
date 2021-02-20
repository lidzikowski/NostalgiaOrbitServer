using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using System;
using System.Linq;
using UnityEngine;
using WebSocketSharp;

public class ChatService : AbstractService
{
    public static string ChannelName = $"/{ServerChannels.Chat}";

    public ChatService()
    {
        Channel = ServerChannels.Chat;
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

                Pilot pilot;

                if (Server.PilotsInGame.ContainsKey(pilotId))
                {
                    pilot = Server.PilotsInGame[pilotId].Pilot;
                }
                else
                {
                    pilot = await Server.Database.PilotFindByGuid(pilotId);
                }


                Chat.ConnectToChannel(ChatChannelTypes.Global, pilotId);

                switch (pilot.FirmType)
                {
                    case FirmTypes.MMO:
                        Chat.ConnectToChannel(ChatChannelTypes.MMO, pilotId);
                        break;
                    case FirmTypes.EIC:
                        Chat.ConnectToChannel(ChatChannelTypes.EIC, pilotId);
                        break;
                    case FirmTypes.VRU:
                        Chat.ConnectToChannel(ChatChannelTypes.VRU, pilotId);
                        break;
                }
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
                Chat.DisconnectFromAllChannels(pilotSession.PilotId);
            }
            else
            {
                base.OnClose(e);
            }
        });
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var command = DLLHelpers.Deserialize<AbstractCommand>(e.RawData);

        if (command == null)
            return;

        Debug.Log($"{Channel} -> OnMessage: {command.GetType().Name}");

        ExecuteCommand<ChannelMessageCommand>(command, OnChannelMessageCommand);

        base.OnMessage(e);
    }

    private void OnChannelMessageCommand(ChannelMessageCommand command, Guid pilotId)
    {
        if (Server.ChatChannels.ContainsKey(command.ChannelId))
            Server.ChatChannels[command.ChannelId].SendMessage(pilotId, command);
    }
}