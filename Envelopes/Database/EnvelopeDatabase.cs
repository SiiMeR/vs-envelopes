using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Config;

namespace Envelopes.Database;

public record Envelope
{
    public int Id { get; set; }
    public required string CreatorId { get; init; }
    public required byte[] ItemBlob { get; init; }
}

public class EnvelopeDatabase
{
    private const string CreateTableQuery =
        "CREATE TABLE IF NOT EXISTS Envelopes (Id INTEGER PRIMARY KEY, CreatorId TEXT, ItemBlob BLOB);";

    private const string InsertEnvelopeQuery =
        "INSERT INTO Envelopes (CreatorId, ItemBlob) VALUES (@CreatorId, @ItemBlob);SELECT last_insert_rowid();";

    private const string GetEnvelopeQuery =
        "SELECT Id, CreatorId, ItemBlob FROM Envelopes WHERE Id = @Id;";

    private readonly string _connectionString;

    public EnvelopeDatabase()
    {
        var path = Path.Combine(GamePaths.DataPath, "ModData", EnvelopesModSystem.Api.World.SavegameIdentifier,
            EnvelopesModSystem.ModId, "envelopes.db");

        _connectionString = $"Data Source={path};";

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(CreateTableQuery, connection);
        command.ExecuteNonQuery();
    }

    public long InsertEnvelope(Envelope envelope)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(InsertEnvelopeQuery, connection);

        command.Parameters.AddWithValue("@CreatorId", envelope.CreatorId);
        command.Parameters.AddWithValue("@ItemBlob", envelope.ItemBlob);

        var result = command.ExecuteScalar();
        if (result == null)
        {
            throw new InvalidOperationException("Failed to insert a new Envelope.");
        }

        var envelopeId = (long)result;

        return envelopeId;
    }

    public Envelope? GetEnvelope(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(GetEnvelopeQuery, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var identifier = (int)reader["Id"];
        var itemBlob = (byte[])reader["ItemBlob"];
        var creatorId = (string)reader["CreatorId"];

        return new Envelope
        {
            Id = identifier,
            ItemBlob = itemBlob,
            CreatorId = creatorId
        };
    }
}