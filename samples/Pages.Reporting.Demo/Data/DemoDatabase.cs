using Microsoft.Data.Sqlite;
using Pages.Reporting.Core.Data;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Serialization;

namespace Pages.Reporting.Demo.Data;

/// <summary>
/// Sets up the demo's SQLite database: sample sales data (the "business" data the library
/// queries) and a Reports table (the demo app playing the "caller" role — it stores the
/// report JSON on its side, exactly as a consuming application would).
/// </summary>
public static class DemoDatabase
{
    public static string GetDbPath(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "demo.db");

    public static void Initialize(string dbPath, ReportJson reportJson)
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

        SeedSales(connection);
        SeedSampleReport(connection, dbPath, reportJson);
    }

    private static void SeedSales(SqliteConnection connection)
    {
        if (Scalar<long>(connection, "SELECT COUNT(*) FROM Sales") > 0)
            return;

        string[] regions = ["North", "South", "East", "West"];
        string[] products = ["Standard", "Pro", "Enterprise"];
        decimal[] unitPrices = [49m, 129m, 399m];
        var random = new Random(42); // deterministic demo data

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

    private static void SeedSampleReport(SqliteConnection connection, string dbPath, ReportJson reportJson)
    {
        if (Scalar<long>(connection, "SELECT COUNT(*) FROM Reports") > 0)
            return;

        var report = BuildSampleReport(dbPath);
        var json = reportJson.Serialize(report); // connection string gets encrypted here

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Reports (Name, Json) VALUES (@name, @json)";
        command.Parameters.AddWithValue("@name", report.Name);
        command.Parameters.AddWithValue("@json", json);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// The sample definition exercising every component. Data comes from shared,
    /// report-level data sets — e.g. the "sales" set feeds all three KPI cards and
    /// "regionRevenue" feeds both the bar chart and the best-region value.
    /// </summary>
    private static Report BuildSampleReport(string dbPath) => new()
    {
        Name = "Monthly Sales Report",
        Connections =
        [
            new ConnectionDefinition
            {
                Name = "demo",
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = $"Data Source={dbPath}",
            },
        ],
        Parameters =
        [
            new ParameterDefinition { Name = "minRevenue", DefaultValue = "0" },
        ],
        DataSets =
        [
            new DataSetDefinition
            {
                Name = "sales",
                Binding = new SqlBinding
                {
                    Connection = "demo",
                    Sql = "SELECT SaleDate, Region, Product, Quantity, Revenue FROM Sales",
                },
            },
            new DataSetDefinition
            {
                Name = "topSales",
                // @minRevenue comes from the report's parameters — override it per request,
                // e.g. /reports/1?minRevenue=5000
                Binding = new SqlBinding
                {
                    Connection = "demo",
                    Sql = "SELECT SaleDate, Region, Product, Quantity, Revenue FROM Sales WHERE Revenue >= @minRevenue ORDER BY Revenue DESC LIMIT 25",
                },
            },
            new DataSetDefinition
            {
                Name = "monthlyRevenue",
                Binding = new SqlBinding
                {
                    Connection = "demo",
                    Sql = "SELECT strftime('%Y-%m', SaleDate) AS Month, SUM(Revenue) AS Revenue FROM Sales GROUP BY Month ORDER BY Month",
                },
            },
            new DataSetDefinition
            {
                Name = "regionRevenue",
                Binding = new SqlBinding
                {
                    Connection = "demo",
                    Sql = "SELECT Region, SUM(Revenue) AS Revenue FROM Sales GROUP BY Region ORDER BY Revenue DESC",
                },
            },
            new DataSetDefinition
            {
                Name = "productRevenue",
                Binding = new SqlBinding
                {
                    Connection = "demo",
                    Sql = "SELECT Product, SUM(Revenue) AS Revenue FROM Sales GROUP BY Product ORDER BY Revenue DESC",
                },
            },
        ],
        Elements =
        [
            new TextElement
            {
                Content = "Monthly Sales Report",
                Style = new ElementStyle { FontSize = 24, Bold = true },
            },
            new TextElement
            {
                Content = "January – June 2026 · demo data generated by Pages.Reporting",
                Style = new ElementStyle { FontSize = 13, Color = "#6a6f76" },
            },
            new SectionElement
            {
                Direction = SectionDirection.Row,
                Children =
                [
                    new KpiCardElement
                    {
                        Label = "Total revenue",
                        Format = "C0",
                        Binding = new DataSetBinding { Name = "sales", Field = "Revenue", Aggregate = ScalarAggregate.Sum },
                    },
                    new KpiCardElement
                    {
                        Label = "Units sold",
                        Format = "#,0",
                        Accent = "#1baf7a",
                        Binding = new DataSetBinding { Name = "sales", Field = "Quantity", Aggregate = ScalarAggregate.Sum },
                    },
                    new KpiCardElement
                    {
                        Label = "Average sale",
                        Format = "C2",
                        Accent = "#4a3aa7",
                        Binding = new DataSetBinding { Name = "sales", Field = "Revenue", Aggregate = ScalarAggregate.Average },
                    },
                ],
            },
            new TextElement
            {
                // regionRevenue is ordered by revenue DESC, so the first row is the best.
                Content = "Best performing region: {regionRevenue.Region} · {sales.Revenue:count} sales recorded",
            },
            new SectionElement
            {
                Direction = SectionDirection.Row,
                Children =
                [
                    new ChartElement
                    {
                        Title = "Revenue by month",
                        ChartType = ChartType.Line,
                        LabelField = "Month",
                        ValueFields = ["Revenue"],
                        Height = 300,
                        Binding = new DataSetBinding { Name = "monthlyRevenue" },
                    },
                ],
            },
            new SectionElement
            {
                Direction = SectionDirection.Row,
                Children =
                [
                    new ChartElement
                    {
                        Title = "Revenue by region",
                        ChartType = ChartType.Bar,
                        LabelField = "Region",
                        ValueFields = ["Revenue"],
                        Height = 280,
                        Binding = new DataSetBinding { Name = "regionRevenue" },
                    },
                    new ChartElement
                    {
                        Title = "Revenue share by product",
                        ChartType = ChartType.Pie,
                        LabelField = "Product",
                        ValueFields = ["Revenue"],
                        Format = "C0",
                        Height = 280,
                        Binding = new DataSetBinding { Name = "productRevenue" },
                    },
                ],
            },
            new TableElement
            {
                Title = "Largest sales",
                Binding = new DataSetBinding { Name = "topSales" },
                Columns =
                [
                    new ColumnDefinition { Field = "SaleDate", Header = "Date" },
                    new ColumnDefinition { Field = "Region" },
                    new ColumnDefinition { Field = "Product" },
                    new ColumnDefinition { Field = "Quantity", Format = "#,0", Aggregate = ColumnAggregate.Sum },
                    new ColumnDefinition { Field = "Revenue", Format = "N2", Aggregate = ColumnAggregate.Sum },
                ],
            },
        ],
    };

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
