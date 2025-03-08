using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Config;

namespace Envelopes.Database;

public record EnvelopeContents
{
    public string? Id { get; set; }
    public required string CreatorId { get; init; }
    public required byte[] ItemBlob { get; init; }
}

public class EnvelopeContentsDatabase
{
    private const string CreateTableQuery =
        "CREATE TABLE IF NOT EXISTS Envelopes (Id TEXT PRIMARY KEY, CreatorId TEXT, ItemBlob BLOB);";

    private const string InsertEnvelopeQuery =
        "INSERT INTO Envelopes (Id, CreatorId, ItemBlob) VALUES (newguid(), @CreatorId, @ItemBlob) RETURNING Id;";

    private const string GetEnvelopeQuery =
        "SELECT Id, CreatorId, ItemBlob FROM Envelopes WHERE Id = @Id;";

    private readonly string _connectionString;

    public EnvelopeContentsDatabase()
    {
        if (EnvelopesModSystem.Api == null)
        {
            throw new InvalidOperationException("The EnvelopesModSystem has not been initialized yet.");
        }

        var path = Path.Combine(GamePaths.DataPath, "ModData", EnvelopesModSystem.Api.World.SavegameIdentifier,
            EnvelopesModSystem.ModId, "envelopes.db");

        _connectionString = $"Data Source={path};";

        using var connection = CreateConnection();
        new SqliteCommand(CreateTableQuery, connection).ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.CreateFunction("newguid", () => Guid.NewGuid().ToString("n"));

        return connection;
    }

    public string InsertEnvelope(EnvelopeContents envelopeContents)
    {
        using var connection = CreateConnection();
        using var command = new SqliteCommand(InsertEnvelopeQuery, connection);

        command.Parameters.AddWithValue("@CreatorId", envelopeContents.CreatorId);
        command.Parameters.AddWithValue("@ItemBlob", envelopeContents.ItemBlob);

        var result = command.ExecuteScalar();
        if (result == null)
        {
            throw new InvalidOperationException("Failed to insert a new Envelope.");
        }

        var envelopeId = (string)result;

        return envelopeId;
    }

    public EnvelopeContents? GetEnvelope(string id)
    {
        using var connection = CreateConnection();
        using var command = new SqliteCommand(GetEnvelopeQuery, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var identifier = (string)reader["Id"];
        var itemBlob = (byte[])reader["ItemBlob"];
        var creatorId = (string)reader["CreatorId"];

        return new EnvelopeContents
        {
            Id = identifier,
            ItemBlob = itemBlob,
            CreatorId = creatorId
        };
    }
}