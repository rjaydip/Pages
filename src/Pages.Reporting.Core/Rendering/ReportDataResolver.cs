using System.Data.Common;
using Pages.Reporting.Core.Data;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Security;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Executes every data binding in a report: decrypts the named connection, opens the right
/// provider, runs the query (scalar for value/KPI elements, rows for tables/charts), and
/// returns a <see cref="ResolvedReport"/> ready for rendering or export.
/// </summary>
public sealed class ReportDataResolver(IReportCipher cipher)
{
    public async Task<ResolvedReport> ResolveAsync(
        Report report,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<ReportElement, ResolvedData>();
        var connections = new Dictionary<string, DbConnection>(StringComparer.OrdinalIgnoreCase);
        var dataSets = new Dictionary<string, ResolvedData>(StringComparer.OrdinalIgnoreCase);
        var values = EffectiveParameters(report, parameters);

        try
        {
            foreach (var element in Flatten(report.Elements))
            {
                try
                {
                    if (element is TextElement text)
                    {
                        data[element] = await RenderTextAsync(report, text, connections, dataSets, values, cancellationToken);
                        continue;
                    }

                    var (binding, scalar) = GetBinding(element);
                    if (binding is null)
                        continue;

                    data[element] = await ExecuteAsync(report, binding, scalar, connections, dataSets, values, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    data[element] = new ResolvedData { Error = ex.Message };
                }
            }
        }
        finally
        {
            foreach (var connection in connections.Values)
                await connection.DisposeAsync();
        }

        return new ResolvedReport(report, data);
    }

    /// <summary>
    /// Executes a single binding against the report's connections — used by designers for
    /// column discovery and data previews. Failures come back as ResolvedData.Error.
    /// </summary>
    public async Task<ResolvedData> ResolveBindingAsync(
        Report report, DataBinding binding, bool scalar = false, CancellationToken cancellationToken = default)
    {
        var connections = new Dictionary<string, DbConnection>(StringComparer.OrdinalIgnoreCase);
        var dataSets = new Dictionary<string, ResolvedData>(StringComparer.OrdinalIgnoreCase);
        var values = EffectiveParameters(report, overrides: null); // designer/discovery: defaults apply
        try
        {
            return await ExecuteAsync(report, binding, scalar, connections, dataSets, values, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ResolvedData { Error = ex.Message };
        }
        finally
        {
            foreach (var connection in connections.Values)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Opens (and immediately closes) the connection — the designer's "Test connection"
    /// button. Accepts plaintext or an encrypted (enc:v1:) connection string, so saved
    /// connections can be tested without exposing their value.
    /// Returns null on success, otherwise the failure message.
    /// </summary>
    public async Task<string?> TestConnectionAsync(ConnectionDefinition definition, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = cipher.IsEncrypted(definition.ConnectionString)
                ? cipher.Decrypt(definition.ConnectionString)
                : definition.ConnectionString;
            await using var connection = DbConnectionFactory.Create(definition.Provider, connectionString);
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex.Message;
        }
    }

    private static IEnumerable<ReportElement> Flatten(IEnumerable<ReportElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            if (element is SectionElement section)
                foreach (var child in Flatten(section.Children))
                    yield return child;
        }
    }

    private static (DataBinding? Binding, bool Scalar) GetBinding(ReportElement element) => element switch
    {
        ValueElement value => (value.Binding, true),
        KpiCardElement kpi => (kpi.Binding, true),
        TableElement table => (table.Binding, false),
        ChartElement chart => (chart.Binding, false),
        _ => (null, false),
    };

    private async Task<ResolvedData> ExecuteAsync(
        Report report,
        DataBinding binding,
        bool scalar,
        Dictionary<string, DbConnection> connections,
        Dictionary<string, ResolvedData> dataSets,
        Dictionary<string, string?> parameterValues,
        CancellationToken cancellationToken)
    {
        if (binding is DataSetBinding reference)
        {
            var data = await GetDataSetAsync(report, reference.Name, connections, dataSets, parameterValues, cancellationToken);
            if (data.Error is not null || !scalar)
                return data;
            return ExtractScalar(data.Table!, reference);
        }

        if (binding is InlineBinding inline)
        {
            var table = ToTable(inline);
            return scalar
                ? new ResolvedData { Scalar = table.Rows.Count > 0 ? table.Rows[0].Values.FirstOrDefault() : null }
                : new ResolvedData { Table = table };
        }

        var sql = (SqlBinding)binding;
        var connection = await GetOpenConnectionAsync(report, sql.Connection, connections, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Sql;
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sql.Parameters is not null)
        {
            foreach (var (name, value) in sql.Parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = JsonValues.ToClr(value) ?? DBNull.Value;
                command.Parameters.Add(parameter);
                bound.Add(name.TrimStart('@'));
            }
        }

        // Report parameters: bind only the ones the SQL actually references — providers
        // reject values for parameters that don't occur in the command text.
        foreach (var (name, value) in parameterValues)
        {
            if (bound.Contains(name) || !sql.Sql.Contains('@' + name, StringComparison.OrdinalIgnoreCase))
                continue;
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = ToDbValue(value);
            command.Parameters.Add(parameter);
        }

        if (scalar)
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return new ResolvedData { Scalar = value is DBNull ? null : value };
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return new ResolvedData { Table = new ReportDataTable(columns, rows) };
    }

    /// <summary>Declared defaults overridden by host-supplied values (undeclared names are ignored).</summary>
    private static Dictionary<string, string?> EffectiveParameters(
        Report report, IReadOnlyDictionary<string, string?>? overrides)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in report.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            var name = parameter.Name.TrimStart('@');
            values[name] = overrides is not null && overrides.TryGetValue(name, out var supplied)
                ? supplied
                : parameter.DefaultValue;
        }
        return values;
    }

    /// <summary>Parameter values travel as strings; bind numbers as numbers so SQL comparisons work.</summary>
    private static object ToDbValue(string? value)
    {
        if (value is null)
            return DBNull.Value;
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer))
            return integer;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            return number;
        return value;
    }

    /// <summary>
    /// Renders a text template: literal runs pass through, each {dataSet.Column:agg:fmt}
    /// expression reads the shared data set cache, {@param} reads the effective parameters.
    /// A failed expression renders inline (⚠) without breaking the rest of the text.
    /// </summary>
    private async Task<ResolvedData> RenderTextAsync(
        Report report,
        TextElement element,
        Dictionary<string, DbConnection> connections,
        Dictionary<string, ResolvedData> dataSets,
        Dictionary<string, string?> parameterValues,
        CancellationToken cancellationToken)
    {
        var result = new System.Text.StringBuilder();
        foreach (var token in TextTemplate.Tokenize(element.Content))
        {
            if (token.Expression is not { } expression)
            {
                result.Append(token.Raw);
                continue;
            }

            if (expression.Parameter is { } parameterName)
            {
                result.Append(parameterValues.TryGetValue(parameterName, out var value)
                    ? value
                    : $"⚠ unknown parameter '{parameterName}'");
                continue;
            }

            var data = await GetDataSetAsync(report, expression.DataSet!, connections, dataSets, parameterValues, cancellationToken);
            if (data.Error is not null)
            {
                result.Append($"⚠ {data.Error}");
                continue;
            }

            var scalar = ExtractScalar(data.Table!, new DataSetBinding
            {
                Name = expression.DataSet!,
                Field = expression.Column,
                Aggregate = expression.Aggregate,
            });
            result.Append(ValueFormatter.Format(scalar.Scalar, expression.Format));
        }

        return new ResolvedData { Scalar = result.ToString() };
    }

    /// <summary>Runs a data set's query at most once per resolution; every consumer shares the result.</summary>
    private async Task<ResolvedData> GetDataSetAsync(
        Report report,
        string name,
        Dictionary<string, DbConnection> connections,
        Dictionary<string, ResolvedData> dataSets,
        Dictionary<string, string?> parameterValues,
        CancellationToken cancellationToken)
    {
        if (dataSets.TryGetValue(name, out var cached))
            return cached;

        ResolvedData data;
        var definition = report.DataSets.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            data = new ResolvedData { Error = $"The report declares no data set named '{name}'." };
        }
        else if (definition.Binding is DataSetBinding)
        {
            data = new ResolvedData { Error = $"Data set '{name}' cannot reference another data set." };
        }
        else
        {
            try
            {
                data = await ExecuteAsync(report, definition.Binding, scalar: false, connections, dataSets, parameterValues, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                data = new ResolvedData { Error = ex.Message };
            }
        }

        dataSets[name] = data;
        return data;
    }

    /// <summary>Reduces a data set column to the single value a value/KPI element shows.</summary>
    private static ResolvedData ExtractScalar(ReportDataTable table, DataSetBinding reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Field))
            return new ResolvedData { Scalar = table.Rows.Count > 0 ? table.Rows[0].Values.FirstOrDefault() : null };

        var field = reference.Field;
        if (reference.Aggregate == ScalarAggregate.First)
            return new ResolvedData { Scalar = table.Rows.Count > 0 ? table.Rows[0].GetValueOrDefault(field) : null };

        var values = table.Rows
            .Select(r => r.GetValueOrDefault(field))
            .Where(v => v is not null)
            .ToList();

        if (reference.Aggregate == ScalarAggregate.Count)
            return new ResolvedData { Scalar = values.Count };

        var numbers = values.Where(ValueFormatter.IsNumeric).Select(ValueFormatter.ToDouble).ToList();
        if (numbers.Count == 0)
            return new ResolvedData { Scalar = null };

        return new ResolvedData
        {
            Scalar = reference.Aggregate switch
            {
                ScalarAggregate.Sum => numbers.Sum(),
                ScalarAggregate.Average => numbers.Average(),
                ScalarAggregate.Min => numbers.Min(),
                ScalarAggregate.Max => numbers.Max(),
                _ => numbers[0],
            },
        };
    }

    private async Task<DbConnection> GetOpenConnectionAsync(
        Report report,
        string name,
        Dictionary<string, DbConnection> connections,
        CancellationToken cancellationToken)
    {
        if (connections.TryGetValue(name, out var existing))
            return existing;

        var definition = report.Connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The report declares no connection named '{name}'.");

        var connectionString = cipher.IsEncrypted(definition.ConnectionString)
            ? cipher.Decrypt(definition.ConnectionString)
            : definition.ConnectionString;

        var connection = DbConnectionFactory.Create(definition.Provider, connectionString);
        await connection.OpenAsync(cancellationToken);
        connections[name] = connection;
        return connection;
    }

    private static ReportDataTable ToTable(InlineBinding inline)
    {
        var columns = inline.Rows.Count > 0 ? inline.Rows[0].Keys.ToList() : [];
        var rows = inline.Rows
            .Select(r => (IReadOnlyDictionary<string, object?>)r.ToDictionary(
                kv => kv.Key,
                kv => JsonValues.ToClr(kv.Value),
                StringComparer.OrdinalIgnoreCase))
            .ToList();
        return new ReportDataTable(columns, rows);
    }
}
