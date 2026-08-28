using Microsoft.Data.Sqlite;

namespace Pages.Reporting.Demo.Data;

/// <summary>
/// Sets up the demo's SQLite database: the sample sales data (the "business" data the library
/// queries) and an empty Reports table (the demo app playing the "caller" role — it stores the
/// report JSON on its side, exactly as a consuming application would).
///
/// The demo seeds no reports of its own. Reports are the user's: built in the designer, stored
/// here, and kept across runs. Only the sales data is generated, so a report the user builds
/// has something to query.
/// </summary>
public static class DemoDatabase
{
    public static string GetDbPath(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "demo.db");

    /// <summary>
    /// A second database with the same shape but different sales figures, so the
    /// "connection strings supplied at runtime" feature has somewhere visibly different to
    /// point at. A report whose connection is marked <c>SuppliedAtRuntime</c> and named
    /// <c>sales</c> renders against this when the endpoints are hit with <c>?env=staging</c>.
    /// </summary>
    public static string GetStagingDbPath(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "demo-staging.db");

    public static void Initialize(string dbPath) => Initialize(dbPath, seed: 42);

    public static void Initialize(string dbPath, int seed)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        Execute(connection,
            """
            CREATE TABLE IF NOT EXISTS Sales (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleDate TEXT NOT NULL,
                Region TEXT NOT NULL,
                Product TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                Revenue REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Json TEXT NOT NULL
            );
            """);

        SeedSales(connection, seed);
    }

    private static void SeedSales(SqliteConnection connection, int seed)
    {
        if (Scalar<long>(connection, "SELECT COUNT(*) FROM Sales") > 0)
            return;

        string[] regions = ["North", "South", "East", "West"];
        string[] products = ["Standard", "Pro", "Enterprise"];
        decimal[] unitPrices = [49m, 129m, 399m];
        var random = new Random(seed); // deterministic demo data

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO Sales (SaleDate, Region, Product, Quantity, Revenue) VALUES (@date, @region, @product, @qty, @revenue)";
        var date = command.Parameters.Add("@date", SqliteType.Text);
        var region = command.Parameters.Add("@region", SqliteType.Text);
        var product = command.Parameters.Add("@product", SqliteType.Text);
        var qty = command.Parameters.Add("@qty", SqliteType.Integer);
        var revenue = command.Parameters.Add("@revenue", SqliteType.Real);

        var start = new DateTime(2026, 1, 1);
        for (var month = 0; month < 6; month++)
        {
            for (var r = 0; r < regions.Length; r++)
            {
                for (var p = 0; p < products.Length; p++)
                {
                    var sales = random.Next(2, 6);
                    for (var s = 0; s < sales; s++)
                    {
                        var quantity = random.Next(1, 40);
                        date.Value = start.AddMonths(month).AddDays(random.Next(0, 28)).ToString("yyyy-MM-dd");
                        region.Value = regions[r];
                        product.Value = products[p];
                        qty.Value = quantity;
                        revenue.Value = (double)(quantity * unitPrices[p]);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
