using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Database_objects;
using NostalgiaOrbitDLL.Core.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class AbstractService : WebSocketBehavior
{
    public ServerChannels Channel { get; protected set; }
    protected string ChannelString;

    protected void AddOrUpdateSession(string jwt, IDictionary<string, object> payload)
    {
        var pilotId = GetPilotIdFromPayload(payload);

        var user = Server.PilotSessions.FirstOrDefault(o => o.PilotId == pilotId);

        if (user == null)
        {
            Server.PilotSessions.Add(new PilotSession(jwt, pilotId, Channel, ID));
            Debug.Log($"AddSession {pilotId} Sessions: {Channel} [All in server: {Server.PilotSessions.Count}]");
        }
        else
        {
            //Dictionary<ServerChannels, string> oldSessions = null;

            //if (user.JWToken != jwt && user.ChannelSocketId.ContainsKey(Channel))
            //{
            //    oldSessions = user.ChannelSocketId.Where(o => o.Key != ServerChannels.Main).ToDictionary(k => k.Key, v => v.Value);
            //}

            user.AddOrUpdateChannel(Channel, jwt, ID);
            Debug.Log($"AddOrUpdateSession {user.PilotId} Sessions: {user.ChannelSocketId.Count}");

            //if (oldSessions?.Any() ?? false)
            //{
            //    foreach (var session in oldSessions)
            //    {
            //        if (Disconnected($"/{session.Key}", session.Value, CloseStatusCode.ProtocolError, "AddOrUpdateSession"))
            //        {
            //            Debug.Log($"Disconnected {Channel} for {user.PilotId}");
            //        }
            //    }
            //}
        }
    }
    protected void ClearSession()
    {
        var user = Server.PilotSessions.FirstOrDefault(o => o.ChannelSocketId.ContainsKey(Channel) && o.ChannelSocketId[Channel].Contains(ID));

        if (user != null)
        {
            user.RemoveChannel(Channel);
            Debug.Log($"ClearSession {user.PilotId} Sessions: {user.ChannelSocketId.Count}");
            if (!user.ChannelSocketId.Any())
                Server.PilotSessions.Remove(user);
        }
    }

    protected string HeadersToString
    {
        get
        {
            var headers = Context.Headers.Cast<string>().Select(o => Context.Headers[o]);
            return string.Join(",", headers);
        }
    }

    protected async override void OnOpen()
    {
        await Server.Database.Log(LogOperations.SocketOpen, HeadersToString, ID);
    }

    protected async override void OnClose(CloseEventArgs e)
    {
        ClearSession();

        await Server.Database.Log(LogOperations.SocketClose, HeadersToString, ID, e.Code.ToString(), e.Reason);
    }

    protected async override void OnError(ErrorEventArgs e)
    {
        ClearSession();

        await Server.Database.Log(LogOperations.SocketError, HeadersToString, ID, e.Exception);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        //await Server.Database.Log(LogOperations.SocketMessage, HeadersToString, ID, "TODO Operation", "TODO Data");
    }

    protected void ExecuteCommand<T>(AbstractCommand abstractCommand, Action<T, Guid> action, bool authorize = true) where T : AbstractCommand
    {
        if (abstractCommand is T command)
        {
            MainThread.Instance().Enqueue(() =>
            {
                Guid pilotId;

                if (authorize)
                {
                    if (VerifyJWT(command.JWToken, out var payload))
                    {
                        var pilot = Server.PilotSessions.FirstOrDefault(o => o.JWToken == command.JWToken);
                        if (pilot == null)
                        {
                            AddOrUpdateSession(command.JWToken, payload);
                        }

                        pilotId = GetPilotIdFromPayload(payload);
                    }
                    else
                        throw new Exception("Token wygasl");
                }

                action.Invoke(command, pilotId);
            });
        }
    }

    protected Guid GetPilotIdFromJWT(string jwt)
    {
        VerifyJWT(jwt, out var payload);
        return new Guid(payload[nameof(Pilot.Id)].ToString());
    }
    protected Guid GetPilotIdFromPayload(IDictionary<string, object> payload)
    {
        return new Guid(payload[nameof(Pilot.Id)].ToString());
    }
    protected bool VerifyJWT(string jwt, out IDictionary<string, object> payload)
    {
        try
        {
            payload = JWT.JsonWebToken.DecodeToObject(jwt, Server.JWTSecretKey) as IDictionary<string, object>;
            return true;
        }
        catch (JWT.SignatureVerificationException)
        {
            payload = null;
            return false;
        }
    }
    protected string CreateJWT(Pilot pilot, out IDictionary<string, object> payload)
    {
        var now = Math.Round((DateTime.UtcNow.AddDays(1) - JWT.JsonWebToken.UnixEpoch).TotalSeconds);
        payload = new Dictionary<string, object>()
        {
            { "exp", now },
            { nameof(Pilot.Id), pilot.Id },
        };
        string token = JWT.JsonWebToken.Encode(payload, Server.JWTSecretKey, JWT.JwtHashAlgorithm.HS512);
        return token;
    }

    public void SendToSocket(AbstractResponse abstractResponse, AbstractCommand command = null)
    {
        SendToSocket(ChannelString, ID, abstractResponse, command);
    }
    public static void SendToSocket(string channel, string socketId, AbstractResponse abstractResponse, AbstractCommand command = null)
    {
        if (command != null)
            abstractResponse.SetResponseId(command);

        SendToSocket(channel, socketId, DLLHelpers.Serialize(abstractResponse));
    }
    public static void SendToSocket(string channel, string socketId, byte[] data)
    {
        try
        {
            if (Server.WebSocketServer.WebSocketServices[channel].Sessions.TryGetSession(socketId, out IWebSocketSession session))
            {
                Server.WebSocketServer.WebSocketServices[channel].Sessions.SendTo(data, socketId);
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Blad komunikacji z {channel} : {ex.Message}");
        }
    }
    public static bool Disconnected(string channel, string socketId, CloseStatusCode closeStatusCode, string reason)
    {
        try
        {
            if (Server.WebSocketServer.WebSocketServices[channel].Sessions.TryGetSession(socketId, out IWebSocketSession session))
            {
                Server.WebSocketServer.WebSocketServices[channel].Sessions.CloseSession(socketId, closeStatusCode, reason);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Blad rozlaczenia z '{channel}' dla '{socketId}' : {ex.Message}");
        }
        return false;
    }
}