using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Exceptions;
using NostalgiaOrbitDLL.Core.Responses;
using NostalgiaOrbitDLL.Core.Validators;
using System;
using System.Linq;
using UnityEngine;
using WebSocketSharp;

public class MainService : AbstractService
{
    public static string ChannelName = $"/{ServerChannels.Main}";

    public MainService()
    {
        Channel = ServerChannels.Main;
        ChannelString = ChannelName;
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var command = DLLHelpers.Deserialize<AbstractCommand>(e.RawData);

        if (command == null)
            return;

        Debug.Log($"{Channel} -> OnMessage: {command.GetType().Name}");

        ExecuteCommand<OnlinePlayersCommand>(command, OnOnlinePlayersCommand, false);
        ExecuteCommand<RegisterCommand>(command, OnRegisterCommand, false);
        ExecuteCommand<LoginCommand>(command, OnLoginCommand, false);
        ExecuteCommand<ChooseFirmCommand>(command, OnChooseFirmCommand);

        //base.OnMessage(e);
    }

    private async void OnOnlinePlayersCommand(OnlinePlayersCommand command, Guid pilotId)
    {
        var accounts = await Server.Database.PilotsInDatabase();

        SendToSocket(new OnlinePlayersResponse(Server.PilotsInGame.Count(), accounts), command);
    }

    private async void OnRegisterCommand(RegisterCommand command, Guid pilotId)
    {
        var exceptions = new RegisterCommandValidator(command).IsValid();

        if (exceptions.Any())
        {
            SendToSocket(new RegisterResponse()
            {
                Exceptions = exceptions
            }, command);
            return;
        }

        // DB Username
        var findUsername = await Server.Database.PilotFindByUsername(command.Username);
        if (findUsername != null)
        {
            SendToSocket(new RegisterResponse()
            {
                Exceptions = NostalgiaOrbitException.One(new OccupiedUsernameException())
            }, command);
            return;
        }

        // DB Email
        var findEmail = await Server.Database.PilotFindByEmail(command.Email);
        if (findEmail != null)
        {
            SendToSocket(new RegisterResponse()
            {
                Exceptions = NostalgiaOrbitException.One(new OccupiedEmailException())
            }, command);
            return;
        }

        // DB NickName
        var findNickName = await Server.Database.PilotFindByNickName(command.PilotName);
        if (findNickName != null)
        {
            SendToSocket(new RegisterResponse()
            {
                Exceptions = NostalgiaOrbitException.One(new OccupiedPilotNameException())
            }, command);
            return;
        }

        // Save
        var pilot = new Pilot(command);
        pilot.ConfigureNewPilot();

        await Server.Database.RegisterPilot(pilot);

        await Server.Database.NewLoginPilot(pilot, new Login(DateTime.Now, HeadersToString, true));

        string jwt = CreateJWT(pilot, out var payload);
        AddOrUpdateSession(jwt, payload);

        pilot.RemovePassword();
        SendToSocket(new RegisterResponse(pilot, jwt), command);
    }

    private async void OnLoginCommand(LoginCommand command, Guid pilotId)
    {
        var exceptions = new LoginCommandValidator(command).IsValid();

        if (exceptions.Any())
        {
            SendToSocket(new LoginResponse()
            {
                Exceptions = exceptions
            }, command);
            return;
        }

        // DB Pilot
        var findPilot = await Server.Database.PilotFindByUsername(command.Username);
        if (findPilot == null || findPilot.Password != command.Password)
        {
            if (findPilot != null)
                await Server.Database.NewLoginPilot(findPilot, new Login(DateTime.Now, HeadersToString, false));

            SendToSocket(new LoginResponse()
            {
                Exceptions = NostalgiaOrbitException.One(new IncorrectUsernameOrPasswordException())
            }, command);
            return;
        }

        await Server.Database.NewLoginPilot(findPilot, new Login(DateTime.Now, HeadersToString, true));

        string jwt = CreateJWT(findPilot, out var payload);
        AddOrUpdateSession(jwt, payload);

        findPilot.RemovePassword();
        SendToSocket(new LoginResponse(findPilot, jwt), command);
    }

    private async void OnChooseFirmCommand(ChooseFirmCommand command, Guid pilotId)
    {
        var exceptions = new ChooseFirmCommandValidator(command).IsValid();

        if (exceptions.Any())
        {
            SendToSocket(new ChooseFirmResponse()
            {
                Exceptions = exceptions
            }, command);
            return;
        }

        Pilot pilot = await Server.Database.PilotFindByGuid(pilotId);
        // Check if pilot have firm 'None'
        if (pilot.FirmType != FirmTypes.None)
        {
            SendToSocket(new ChooseFirmResponse()
            {
                Exceptions = NostalgiaOrbitException.One(new BugHandleException())
            }, command);
            return;
        }

        pilot.ConfigureCompany(command.FirmType);

        // Update basic company
        await Server.Database.UpdateCompanyPilot(pilot);

        SendToSocket(new ChooseFirmResponse(command), command);
    }
}