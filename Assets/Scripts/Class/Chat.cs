using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Responses;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Chat
{
    public Guid ChannelId { get; set; }
    public ChatChannelTypes ChannelType { get; set; }



    public ChatUser AdministratorId { get; set; }
    public List<ChatUser> Users { get; set; } = new List<ChatUser>();
    public Func<Guid, PilotMapObject> GetPilot => o =>
    {
        if (Server.PilotsInGame.ContainsKey(o))
            return Server.PilotsInGame[o];

        return null;
    };
    public Func<Guid, string> GetPilotSocketId => guid => Server.PilotSessions.First(o => o.PilotId == guid).ChannelSocketId[ServerChannels.Chat];
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();



    public Chat(ChatChannelTypes channelType)
    {
        ChannelId = Guid.NewGuid();
        ChannelType = channelType;
    }



    public void SendMessage(Guid userId, ChannelMessageCommand command)
    {
        var user = Users.FirstOrDefault(o => o.Id == userId);
        var message = new ChatMessage(ChannelId, ChannelType, user, command.Message);

        Messages.Add(message);

        SynchronizeWitHAll(new ChatMessageResponse(message));
    }



    public static void ConnectToChannel(ChatChannelTypes chatChannelType, Guid userId)
    {
        Server.ChatChannels.Values.First(o => o.ChannelType == chatChannelType).ConnectToChannel(userId);
    }
    public bool ConnectToChannel(Guid userId)
    {
        var user = Users.FirstOrDefault(o => o.Id == userId);
        if (user != null && Users.Contains(user))
            return false;

        user = new ChatUser(userId, GetPilot(userId).ObjectName);

        Users.Add(user);

        SynchronizeWitHAll(new ConnectToChannelResponse(ChannelId, ChannelType, user));
        Synchronize(user.Id, new ChatMessageResponse(Messages));

        return true;
    }
    public static void DisconnectFromAllChannels(Guid userId)
    {
        foreach (var channel in Server.ChatChannels.Values.Where(o => o.Users.Any(u => u.Id == userId)))
        {
            channel.DisconnectFromChannel(userId);
        }
    }
    public bool DisconnectFromChannel(Guid userId)
    {
        var user = Users.FirstOrDefault(o => o.Id == userId);
        if (user == null || !Users.Contains(user))
            return false;

        Users.Remove(user);
        return true;
    }

    public void SynchronizeWitHAll(AbstractResponse abstractResponse)
    {
        if (Users.Count <= 0)
            return;

        var data = DLLHelpers.Serialize(abstractResponse);

        foreach (var user in Users)
        {
            Synchronize(user.Id, data);
        }
    }
    public void Synchronize(Guid guid, AbstractResponse abstractResponse)
    {
        Synchronize(guid, DLLHelpers.Serialize(abstractResponse));
    }
    public void Synchronize(Guid guid, byte[] data)
    {
        var pilotSocketId = GetPilotSocketId(guid);

        if (pilotSocketId != null)
            AbstractService.SendToSocket(ChatService.ChannelName, pilotSocketId, data);
    }
}