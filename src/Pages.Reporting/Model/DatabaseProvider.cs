namespace Pages.Reporting.Core.Model;

/// <summary>Database engines supported out of the box; the consuming app installs no ADO.NET packages.</summary>
public enum DatabaseProvider
{
    SqlServer,
    Sqlite,
    PostgreSql,
    MySql,
}
