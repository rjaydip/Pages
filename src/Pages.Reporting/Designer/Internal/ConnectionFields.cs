using System.Data.Common;
using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// Structured editing model behind the designer's connection form: parses a plaintext
/// connection string into per-provider fields and builds it back, so users fill in
/// Server/Database/User boxes instead of writing key=value syntax. The raw string
/// stays editable under the form's "Connection string" disclosure for anything the
/// fields don't cover — unknown keys survive a parse→build round trip only there.
/// </summary>
internal sealed class ConnectionFields
{
    /// <summary>Host name — or the file path for SQLite.</summary>
    public string Server { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>SQL Server: Windows authentication instead of user/password.</summary>
    public bool IntegratedSecurity { get; set; }

    /// <summary>SQL Server: on by default — local/dev servers rarely have a trusted certificate.</summary>
    public bool TrustServerCertificate { get; set; } = true;

    public static ConnectionFields Parse(DatabaseProvider provider, string connectionString)
    {
        var fields = new ConnectionFields();
        if (string.IsNullOrWhiteSpace(connectionString))
            return fields;

        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return fields; // unparseable — leave fields empty; the raw editor still shows the value
        }

        string Get(params string[] keys)
        {
            foreach (var key in keys)
                if (builder.TryGetValue(key, out var value) && value?.ToString() is { } text)
                    return text;
            return string.Empty;
        }

        fields.Server = provider == DatabaseProvider.Sqlite
            ? Get("Data Source", "DataSource", "Filename")
            : Get("Server", "Data Source", "Host", "Address", "Addr");
        fields.Port = Get("Port");
        fields.Database = Get("Database", "Initial Catalog");
        fields.Username = Get("User ID", "UserID", "Username", "User", "UID");
        fields.Password = Get("Password", "PWD");

        // SQL Server folds the port into the server value ("host,1433").
        if (provider == DatabaseProvider.SqlServer && fields.Server.Split(',') is [var host, var port])
            (fields.Server, fields.Port) = (host.Trim(), port.Trim());

        var integrated = Get("Integrated Security", "Trusted_Connection");
        fields.IntegratedSecurity = integrated.Equals("true", StringComparison.OrdinalIgnoreCase)
            || integrated.Equals("sspi", StringComparison.OrdinalIgnoreCase)
            || integrated.Equals("yes", StringComparison.OrdinalIgnoreCase);

        var trust = Get("TrustServerCertificate", "Trust Server Certificate");
        if (trust.Length > 0)
            fields.TrustServerCertificate = trust.Equals("true", StringComparison.OrdinalIgnoreCase);

        return fields;
    }

    public string Build(DatabaseProvider provider)
    {
        var builder = new DbConnectionStringBuilder();
        void Add(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                builder[key] = value.Trim();
        }

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                Add("Data Source", Server);
                break;

            case DatabaseProvider.SqlServer:
                Add("Server", string.IsNullOrWhiteSpace(Port) ? Server : $"{Server.Trim()},{Port.Trim()}");
                Add("Database", Database);
                if (IntegratedSecurity)
                    builder["Integrated Security"] = "True";
                else
                {
                    Add("User ID", Username);
                    Add("Password", Password);
                }
                if (TrustServerCertificate)
                    builder["TrustServerCertificate"] = "True";
                break;

            case DatabaseProvider.PostgreSql:
                Add("Host", Server);
                Add("Port", Port);
                Add("Database", Database);
                Add("Username", Username);
                Add("Password", Password);
                break;

            case DatabaseProvider.MySql:
                Add("Server", Server);
                Add("Port", Port);
                Add("Database", Database);
                Add("User ID", Username);
                Add("Password", Password);
                break;
        }

        return builder.ConnectionString;
    }
}
