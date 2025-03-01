
using Microsoft.Data.Sqlite;

namespace Envelopes.Database;

public record Envelope(string ContentsId, byte[] ItemBlob);

public class EnvelopeDatabase
{
    private readonly string connectionString = "Data Source=Envelopes.db;Version=3;";

    public EnvelopeDatabase()
    {
        using var connection = new SqliteConnection(connectionString);
        
        connection.Open();
        const string createTableQuery = "CREATE TABLE IF NOT EXISTS Envelopes (ID TEXT PRIMARY KEY,ItemBlob BLOB);";
        
        using var command = new SqliteCommand(createTableQuery, connection);
        command.ExecuteNonQuery();
    }
}