using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Config;

namespace Envelopes.Database;


public record Stamp(string Id, string Title, byte[] Design);

public class StampDatabase
{
    private const string CreateTableQuery = "CREATE TABLE IF NOT EXISTS Stamps (Id TEXT PRIMARY KEY, Title TEXT DEFAULT '', Design BLOB);";
    private const string InsertStampQuery = "INSERT INTO Stamps (Id, Title, Design) VALUES (@Id, @Title, @Design);";
    private const string GetStampQuery = "SELECT Id, Name, Design FROM Stamps WHERE Id = @Id;";

    private readonly string _connectionString;

    public StampDatabase()
    {
        var path = Path.Combine(GamePaths.DataPath, "ModData", EnvelopesModSystem.Api.World.SavegameIdentifier,EnvelopesModSystem.ModId, "stamps.db");
        _connectionString = $"Data Source={path};";

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        using var command = new SqliteCommand(CreateTableQuery, connection);
        command.ExecuteNonQuery();
    }
    
    public void InsertStamp(Stamp stamp)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(InsertStampQuery, connection);
        
        command.Parameters.AddWithValue("@Id", stamp.Id);
        command.Parameters.AddWithValue("@Title", stamp.Title);
        command.Parameters.AddWithValue("@Design", stamp.Design);
        
        command.ExecuteNonQuery();
    }

    public Stamp? GetStamp(string id)
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

        var identifier = reader["Id"].ToString();
        var title = reader["Title"]?.ToString() ?? string.Empty;
        var design = (byte[])reader["Design"];

        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }
            
        return new Stamp(identifier,title,design);
    }
}