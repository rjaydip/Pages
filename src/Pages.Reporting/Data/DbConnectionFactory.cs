using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Data;

/// <summary>
/// Creates ADO.NET connections for every supported provider. The providers ship with this
/// library, so consuming applications install no database packages of their own.
/// </summary>
public static class DbConnectionFactory
{
    public static DbConnection Create(DatabaseProvider provider, string connectionString) => provider switch
    {
        DatabaseProvider.SqlServer => new SqlConnection(connectionString),
        DatabaseProvider.Sqlite => new SqliteConnection(connectionString),
        DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
        DatabaseProvider.MySql => new MySqlConnection(connectionString),
        _ => throw new NotSupportedException($"Unsupported database provider: {provider}"),
    };
}
