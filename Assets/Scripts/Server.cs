using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Database_objects;
using NostalgiaOrbitDLL.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WebSocketSharp.Server;

public class Server : MonoBehaviour
{
    public static WebSocketServer WebSocketServer;
    public static Database Database;

    public static List<PilotSession> PilotSessions = new List<PilotSession>();

    public static Dictionary<MapTypes, MapWorker> MapInstances = new Dictionary<MapTypes, MapWorker>();
    public static Dictionary<Guid, PilotMapObject> PilotsInGame = new Dictionary<Guid, PilotMapObject>();
    public static Dictionary<Guid, Chat> ChatChannels = new Dictionary<Guid, Chat>();

    public const string JWTSecretKey = "GQDstcKsx0NHjPOuXOYg5MbeJ1XT0uFiwDVvVBrk";
    public const Servers ServerInstance = Servers.Poland;

    [SerializeField]
    public Transform MapTransform;

    [SerializeField]
    public GameObject MapPattern;

    public static int TargetFrames = 10;

    private void Awake()
    {
        Application.targetFrameRate = TargetFrames;
    }

    public static int ShotDelay = 1 * TargetFrames;
    public static int UnderAttackDelay = 10 * TargetFrames;
    public static int RepairDelay = 1 * TargetFrames;

    async void Start()
    {
        Database = new Database();

        ConfigureGame();

        WebSocketServer = new WebSocketServer($"ws://{GetLocalIPAddress()}:24231");

        WebSocketServer.AddWebSocketService<MainService>(MainService.ChannelName);
        WebSocketServer.AddWebSocketService<HangarService>(HangarService.ChannelName);
        WebSocketServer.AddWebSocketService<GameService>(GameService.ChannelName);
        WebSocketServer.AddWebSocketService<ChatService>(ChatService.ChannelName);

        WebSocketServer.Start();

        Debug.Log($"Status - {WebSocketServer.IsListening}");

        await Database.Log(LogOperations.ServerStart);

        InvokeRepeating(nameof(SaveAllPilotsInDatabase), 1, 60);
    }

    async void OnApplicationQuit()
    {
        SaveAllPilotsInDatabase();

        try
        {
            WebSocketServer.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Blad zatrzymania socketu : {ex.Message}");
            await Database.Log(LogOperations.ServerError, ex);
        }

        await Database.Log(LogOperations.ServerStop);
    }

    private void ConfigureGame()
    {
        foreach (MapTypes mapType in ((MapTypes[])Enum.GetValues(typeof(MapTypes))).Where(o => !DLLHelpers.IsGalaxyGateMap(o)))
        {
            GameObject mapGameObject = Instantiate(MapPattern, MapTransform);
            mapGameObject.transform.name = mapType.ToString();

            MapWorker mapWorker = mapGameObject.GetComponent<MapWorker>();
            mapWorker.AbstractMap = AbstractMap.GetMapByType(mapType);

            MapInstances.Add(mapType, mapWorker);
        }

        CreateChatChannel(ChatChannelTypes.Global);
        CreateChatChannel(ChatChannelTypes.MMO);
        CreateChatChannel(ChatChannelTypes.EIC);
        CreateChatChannel(ChatChannelTypes.VRU);
    }

    private void CreateChatChannel(ChatChannelTypes chatChannelType)
    {
        var channel = new Chat(chatChannelType);
        ChatChannels.Add(channel.ChannelId, channel);
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                Debug.Log($"Server IP - {ip}");
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    public static void PlayerDisconnected(PilotMapObject pilotMapObject)
    {
        SavePilotData(pilotMapObject.Pilot);
        PilotsInGame.Remove(pilotMapObject.Id);

        var session = PilotSessions.FirstOrDefault(o => o.ChannelSocketId.ContainsKey(ServerChannels.Game));
        if (session != null)
        {
            AbstractService.Disconnected(GameService.ChannelName, session.ChannelSocketId[ServerChannels.Game], WebSocketSharp.CloseStatusCode.Normal, nameof(LogoutCommand));
        }
    }

    public static void SavePilotData(Pilot pilot)
    {
        Debug.Log($"Database save - {pilot.PilotName}");
        Database.UpdatePilotFields(pilot);
    }

    public void SaveAllPilotsInDatabase()
    {
        foreach (var pilot in PilotsInGame)
        {
            _ = Database.UpdatePilotFields(pilot.Value.Pilot);
        }
    }
}