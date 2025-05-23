﻿using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Config;

namespace Envelopes.Database;

public record Stamp
{
    public long Id { get; set; }
    public required string Title { get; init; }
    public required string CreatorId { get; init; }
    public required byte[] Design { get; init; }
    public required long Dimensions { get; init; }
}

public class StampDatabase
{
    private const string CreateTableQuery =
        "CREATE TABLE IF NOT EXISTS Stamps (Id INTEGER PRIMARY KEY, CreatorId TEXT, Title TEXT DEFAULT '', Design BLOB, Dimensions INTEGER);";

    private const string InsertStampQuery =
        "INSERT INTO Stamps (Title, CreatorId, Design, Dimensions) VALUES (@Title, @CreatorId, @Design, @Dimensions);SELECT last_insert_rowid();";

    private const string GetStampQuery = "SELECT Id, CreatorId, Title, Design, Dimensions FROM Stamps WHERE Id = @Id;";

    private readonly string _connectionString;

    public StampDatabase()
    {
        if (EnvelopesModSystem.Api == null)
        {
            throw new InvalidOperationException("The EnvelopesModSystem has not been initialized yet.");
        }


        var databaseDirectory = Path.Combine(GamePaths.DataPath, "ModData",
            EnvelopesModSystem.Api.World.SavegameIdentifier,
            EnvelopesModSystem.ModId);
        var path = Path.Combine(databaseDirectory, "stamps.db");
        _connectionString = $"Data Source={path};";

        if (!Directory.Exists(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        using var connection = CreateConnection();
        new SqliteCommand(CreateTableQuery, connection).ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        return connection;
    }

    public long InsertStamp(Stamp stamp)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(InsertStampQuery, connection);

        command.Parameters.AddWithValue("@Title", stamp.Title);
        command.Parameters.AddWithValue("@CreatorId", stamp.CreatorId);
        command.Parameters.AddWithValue("@Dimensions", stamp.Dimensions);
        command.Parameters.AddWithValue("@Design", stamp.Design);

        var result = command.ExecuteScalar();
        if (result == null)
        {
            throw new InvalidOperationException("Failed to insert a new stamp.");
        }

        var stampId = (long)result;

        return stampId;
    }

    public Stamp? GetStamp(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(GetStampQuery, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var identifier = (long)reader["Id"];
        var title = (string)reader["Title"];
        var design = (byte[])reader["Design"];
        var dimensions = (long)reader["Dimensions"];
        var creatorId = (string)reader["CreatorId"];

        return new Stamp
        {
            Id = identifier,
            Title = title,
            Design = design,
            Dimensions = dimensions,
            CreatorId = creatorId
        };
    }
}