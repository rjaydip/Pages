using Microsoft.Data.Sqlite;

namespace Pages.Reporting.Demo.Data;

public sealed record StoredReport(int Id, string Name, string Json);

/// <summary>
/// The demo's own storage for report JSON — this lives in the APP, not the library.
/// A real consumer would do the same against their database, files, or blob storage.
/// </summary>
public sealed class ReportRepository(string dbPath)
{
    private readonly string _connectionString = $"Data Source={dbPath}";

    public List<StoredReport> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Json FROM Reports ORDER BY Id";
        using var reader = command.ExecuteReader();
        var reports = new List<StoredReport>();
        while (reader.Read())
            reports.Add(new StoredReport(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return reports;
    }

    public StoredReport? Get(int id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Json FROM Reports WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new StoredReport(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)) : null;
    }

    public int Insert(string name, string json)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Reports (Name, Json) VALUES (@name, @json); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@json", json);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Save(int id, string name, string json)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Reports SET Name = @name, Json = @json WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@json", json);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reports WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
