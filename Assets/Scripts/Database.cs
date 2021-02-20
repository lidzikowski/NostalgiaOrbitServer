using MongoDB.Driver;
using NostalgiaOrbitDLL;
using NostalgiaOrbitDLL.Core.Commands;
using NostalgiaOrbitDLL.Core.Database_objects;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class Database
{
    private const string MONGO_URI = "mongodb://127.0.0.1:27017";
    private const string DATABASE_NAME = "nostalgiaorbit";
    private MongoClient client;
    public IMongoDatabase db;

    public Database()
    {
        client = new MongoClient(MONGO_URI);
        db = client.GetDatabase(DATABASE_NAME);

        Debug.Log($"Database : {db.DatabaseNamespace.DatabaseName}");
    }

    #region Log

    private IMongoCollection<Log> logCollection { get => db.GetCollection<Log>("logs"); }

    public async Task Log(LogOperations operation)
    {
        await Log(new Log(operation));
    }
    public async Task Log(LogOperations operation, Exception exception)
    {
        await Log(new Log(operation, exception));
    }
    public async Task Log(LogOperations operation, string header, string socketId, string message = default, string data = default)
    {
        await Log(new Log(operation, header, socketId, message, data));
    }
    public async Task Log(LogOperations operation, string header, string socketId, Exception exception)
    {
        await Log(new Log(operation, header, socketId, exception));
    }
    public async Task Log(Log log)
    {
        await logCollection.InsertOneAsync(log);
    }

    #endregion Log

    #region Pilot

    private IMongoCollection<Pilot> pilotsCollection { get => db.GetCollection<Pilot>("pilots"); }

    public async Task<long> PilotsInDatabase()
    {
        return await pilotsCollection.EstimatedDocumentCountAsync();
    }
    public async Task<Pilot> PilotFindByUsername(string username)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Username == username);

        return await PilotFindFilter(filter);
    }
    public async Task<Pilot> PilotFindByEmail(string email)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Email == email);

        return await PilotFindFilter(filter);
    }
    public async Task<Pilot> PilotFindByNickName(string nickName)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.PilotName == nickName);

        return await PilotFindFilter(filter);
    }
    public async Task<Pilot> PilotFindByGuid(Guid guid)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Id == guid);

        return await PilotFindFilter(filter);
    }
    private async Task<Pilot> PilotFindFilter(FilterDefinition<Pilot> filter)
    {
        var data = await pilotsCollection.FindAsync(filter);
        return data.FirstOrDefault();
    }
    public async Task RegisterPilot(Pilot pilot)
    {
        await pilotsCollection.InsertOneAsync(pilot);
    }
    public async Task NewLoginPilot(Pilot pilot, Login login)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Id == pilot.Id);

        var update = Builders<Pilot>.Update
           .Push(x => x.Logins, login);

        await pilotsCollection.UpdateOneAsync(filter, update);
    }
    public async Task UpdateCompanyPilot(Pilot pilot)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Id == pilot.Id);

        var update = Builders<Pilot>.Update
           .Set(x => x.FirmType, pilot.FirmType)
           .Set(x => x.Map, pilot.Map)
           .Set(x => x.Position, pilot.Position);

        await pilotsCollection.UpdateOneAsync(filter, update);
    }

    public async Task UpdatePilotFields(Pilot pilot)
    {
        var filter = Builders<Pilot>.Filter
           .Where(x => x.Id == pilot.Id);

        var update = Builders<Pilot>.Update
           .Set(x => x.Level, pilot.Level)
           .Set(x => x.Experience, pilot.Experience)
           .Set(x => x.Honor, pilot.Honor)
           .Set(x => x.Select_Ammunition, pilot.Select_Ammunition)
           .Set(x => x.Select_Rocket, pilot.Select_Rocket)
           .Set(x => x.Resources, pilot.Resources)
           .Set(x => x.RankingPoints, pilot.RankingPoints)
           .Set(x => x.RankType, pilot.RankType)
           .Set(x => x.PremiumStatus, pilot.PremiumStatus)
           .Set(x => x.PremiumEndDate, pilot.PremiumEndDate)
           .Set(x => x.Drones, pilot.Drones)
           .Set(x => x.HaveShields, pilot.HaveShields)
           .Set(x => x.AccountType, pilot.AccountType)
           .Set(x => x.Items, pilot.Items)
           .Set(x => x.ConfigurationFirst, pilot.ConfigurationFirst)
           .Set(x => x.ShipType, pilot.ShipType)
           .Set(x => x.Map, pilot.Map)
           .Set(x => x.Position, pilot.Position)
           .Set(x => x.OwnedShips, pilot.OwnedShips);

        await pilotsCollection.UpdateOneAsync(filter, update);
    }
    #endregion Pilot
}