using System.Data.Common;
using Microsoft.Extensions.Logging;
using Pages.Reporting.Core.Data;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Security;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Executes every data binding in a report: decrypts the named connection, opens the right
/// provider, runs the query, and returns a <see cref="ResolvedReport"/> ready for rendering or
/// export. Every binding returns rows; the single values a text token shows are reduced from
/// those rows by <see cref="ScalarValues"/>, not fetched separately.
/// </summary>
public sealed class ReportDataResolver(IReportCipher cipher, ILogger<ReportDataResolver> logger)
{
    public async Task<ResolvedReport> ResolveAsync(
        Report report,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // One instant for the whole resolution, so every {now} in the report agrees — including
        // a text element inside a group band, which is re-rendered per group at render time.
        var generatedAt = DateTime.Now;
        var data = new Dictionary<ReportElement, ResolvedData>();
        var parameters = EffectiveParameters(report, options?.Parameters);
        var context = new ResolveContext(
            report.Name,
            parameters.Values,
            parameters.Types,
            parameters.Invalid,
            EffectiveConnectionStrings(report, options?.ConnectionStrings),
            generatedAt);

        var elements = Flatten(report.Bands.SelectMany(band => band.Elements)).ToList();

        try
        {
            // Text tokens are substituted synchronously — and again, per group, at render time
            // (see ScopedValues) — so every data set a token names must already be in the
            // cache. Same pattern as the group pre-fetch below. GetDataSetAsync caches, so a
            // data set a table binding also uses is not queried twice.
            foreach (var name in TextDataSetNames(elements))
                await GetDataSetAsync(report, name, context, cancellationToken);

            // Every element lives in a band now — bands are the report.
            foreach (var element in elements)
            {
                try
                {
                    if (element is TextElement text)
                    {
                        data[element] = RenderText(text, context);
                        continue;
                    }

                    if (GetBinding(element) is not { } binding)
                        continue;

                    data[element] = await ExecuteAsync(report, binding, context, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    data[element] = new ResolvedData { Error = ex.Message };
                }
            }

            // A group's data set is named by the band, not by an element binding, so the
            // element walk above may never have resolved it — and BandPlan reads it from this
            // same cache at render time. Without this the group would silently print once,
            // ungrouped. GetDataSetAsync caches, so a data set an element already bound is
            // not queried a second time.
            foreach (var band in report.Bands)
            {
                if (band.Type != BandType.GroupHeader || band.Group is not { } group
                    || string.IsNullOrWhiteSpace(group.DataSet)
                    || string.IsNullOrWhiteSpace(group.Field))
                    continue;
                // GetDataSetAsync already records failures as ResolvedData.Error, which
                // Partition treats as "nothing to partition" — no separate catch needed here.
                await GetDataSetAsync(report, group.DataSet, context, cancellationToken);
            }
        }
        finally
        {
            await context.DisposeConnectionsAsync();
        }

        return new ResolvedReport(
            report, data, context.Failures(), context.DataSets,
            context.ParameterValues, generatedAt, parameters.Problems);
    }

    /// <summary>
    /// Executes a single binding against the report's connections — used by designers for
    /// column discovery and data previews. Failures come back as ResolvedData.Error.
    /// </summary>
    public async Task<ResolvedData> ResolveBindingAsync(
        Report report, DataBinding binding, CancellationToken cancellationToken = default)
    {
        // designer/discovery: parameter defaults apply, and connections are always the
        // authored ones — there is no runtime channel here. A default that doesn't match its
        // type just binds NULL here; the designer surfaces that separately (ParameterChecks).
        var parameters = EffectiveParameters(report, overrides: null);
        var context = new ResolveContext(
            report.Name,
            parameters.Values,
            parameters.Types,
            parameters.Invalid,
            EffectiveConnectionStrings(report, overrides: null),
            DateTime.Now);
        try
        {
            return await ExecuteAsync(report, binding, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ResolvedData { Error = ex.Message };
        }
        finally
        {
            await context.DisposeConnectionsAsync();
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
        if (definition.SuppliedAtRuntime)
            return "This connection is supplied at generation time — there is nothing stored to test here.";

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

    /// <summary>The rows binding an element renders from, or null when it reads no data.</summary>
    private static DataBinding? GetBinding(ReportElement element) => element switch
    {
        TableElement table => table.Binding,
        ChartElement chart => chart.Binding,
        _ => null,
    };

    private async Task<ResolvedData> ExecuteAsync(
        Report report,
        DataBinding binding,
        ResolveContext context,
        CancellationToken cancellationToken)
    {
        if (binding is DataSetBinding reference)
            return await GetDataSetAsync(report, reference.Name, context, cancellationToken);

        if (binding is InlineBinding inline)
            return new ResolvedData { Table = ToTable(inline) };

        var sql = (SqlBinding)binding;
        var connection = await GetOpenConnectionAsync(report, sql.Connection, context, cancellationToken);

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
        foreach (var (name, value) in context.ParameterValues)
        {
            if (bound.Contains(name) || !sql.Sql.Contains('@' + name, StringComparison.OrdinalIgnoreCase))
                continue;
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = BindParameter(name, value, context);
            command.Parameters.Add(parameter);
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

    /// <summary>
    /// The per-name effective parameter state for one resolution. <see cref="Values"/> holds
    /// the canonical string for a valid typed value, the raw string otherwise; <see cref="Types"/>
    /// records the type of a valid typed value (so SQL binding can convert it);
    /// <see cref="Invalid"/> names typed values that failed to parse (bound as NULL);
    /// <see cref="Problems"/> is the reader-facing explanation for each.
    /// </summary>
    private sealed record ParameterResolution(
        Dictionary<string, string?> Values,
        Dictionary<string, ParameterType> Types,
        HashSet<string> Invalid,
        IReadOnlyList<ParameterProblem> Problems);

    /// <summary>
    /// Declared defaults overridden by host-supplied values (undeclared names are ignored). A
    /// declared type is parsed here: a valid value is canonicalised (so <c>{@name}</c> and SQL
    /// binding agree), an invalid one keeps its raw string for display but is recorded so it
    /// binds NULL and shows a banner.
    /// </summary>
    private static ParameterResolution EffectiveParameters(
        Report report, IReadOnlyDictionary<string, string?>? overrides)
    {
        var supplied = NormalizeKeys(overrides);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, ParameterType>(StringComparer.OrdinalIgnoreCase);
        var invalid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<ParameterProblem>();

        foreach (var parameter in report.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            var name = parameter.Name.TrimStart('@');
            var raw = supplied.TryGetValue(name, out var value) ? value : parameter.DefaultValue;

            if (parameter.Type is ParameterType.Text)
            {
                values[name] = raw;
                continue;
            }

            var parsed = ParameterParsing.TryParse(raw, parameter.Type, parameter.AllowedValues);
            if (!parsed.Valid)
            {
                values[name] = raw;   // {@name} shows what was passed
                invalid.Add(name);
                problems.Add(new ParameterProblem(name, DescribeInvalidParameter(name, raw, parameter.Type)));
            }
            else if (parsed.Canonical is { } canonical)
            {
                values[name] = canonical;
                types[name] = parameter.Type;
            }
            else
            {
                values[name] = raw;   // null/empty — the resolver already applied the default
            }
        }

        return new ParameterResolution(values, types, invalid, problems);
    }

    private static string DescribeInvalidParameter(string name, string? raw, ParameterType type) => type switch
    {
        ParameterType.Number => $"Parameter '{name}' = '{raw}' is not a valid number — filtered as NULL.",
        ParameterType.Date => $"Parameter '{name}' = '{raw}' is not a valid date (expected YYYY-MM-DD) — filtered as NULL.",
        ParameterType.Boolean => $"Parameter '{name}' = '{raw}' is not true/false or 1/0 — filtered as NULL.",
        ParameterType.List => $"Parameter '{name}' = '{raw}' is not one of the allowed values — filtered as NULL.",
        _ => $"Parameter '{name}' = '{raw}' is not valid — filtered as NULL.",
    };

    /// <summary>
    /// Turns a parameter's effective value into the CLR value bound to a data set's SQL. A
    /// valid <c>number</c>/<c>boolean</c> becomes its CLR type; a <c>date</c> stays the
    /// canonical ISO string (every provider converts it for a date column); an invalid typed
    /// value binds NULL; everything else keeps the <see cref="ToDbValue"/> string heuristic.
    /// </summary>
    private static object BindParameter(string name, string? canonical, ResolveContext context)
    {
        if (context.InvalidTypedParameters.Contains(name))
            return DBNull.Value;

        return context.ParameterTypes.GetValueOrDefault(name) switch
        {
            ParameterType.Number => NumberDbValue(canonical!),
            ParameterType.Boolean => canonical == "1" ? 1L : 0L,   // canonical is "1" / "0"
            ParameterType.Date => canonical!,
            _ => ToDbValue(canonical),
        };
    }

    /// <summary>A canonical number string (produced by <c>ParameterParsing</c>) as long or decimal.</summary>
    private static object NumberDbValue(string canonical) =>
        long.TryParse(canonical, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var integer)
            ? integer
            : decimal.Parse(canonical, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Runtime connection-string overrides, for declared connections marked
    /// <see cref="ConnectionDefinition.SuppliedAtRuntime"/> only. Unknown keys are ignored and
    /// a whitespace-only value counts as "not supplied".
    /// </summary>
    private static Dictionary<string, string?> EffectiveConnectionStrings(
        Report report, IReadOnlyDictionary<string, string?>? overrides)
    {
        var supplied = NormalizeKeys(overrides);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in report.Connections)
        {
            if (!connection.SuppliedAtRuntime || string.IsNullOrWhiteSpace(connection.Name))
                continue;
            if (supplied.TryGetValue(connection.Name, out var value) && !string.IsNullOrWhiteSpace(value))
                values[connection.Name] = value;
        }
        return values;
    }

    /// <summary>
    /// Rebuilds a host-supplied map with a case-insensitive comparer and a leading <c>@</c>
    /// stripped from each key — the caller's dictionary is usually ordinal (e.g. projected
    /// straight from a query string), and a caller passing <c>"@region"</c> should hit the
    /// same parameter as <c>"region"</c>. A last-wins loop rather than the
    /// <see cref="Dictionary{TKey,TValue}"/> copy constructor, which throws when two keys
    /// collide only under the new comparer.
    /// </summary>
    private static Dictionary<string, string?> NormalizeKeys(IReadOnlyDictionary<string, string?>? source)
    {
        var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (source is not null)
            foreach (var (key, value) in source)
                normalized[key.TrimStart('@')] = value;
        return normalized;
    }

    /// <summary>
    /// Parameter values travel as strings; bind numbers as numbers so SQL comparisons work
    /// (a TEXT-bound "5000" never satisfies <c>Revenue &gt; @min</c> in SQLite).
    ///
    /// A value only counts as a number when the text is exactly how that number writes
    /// itself. "02" is a zero-padded code, not the number 2 — binding it as 2 makes it stop
    /// matching the TEXT column it came from, and the query silently returns nothing. The
    /// same guard rejects " 2", "+2" and "1,000". The round trip goes through decimal
    /// rather than double so scale survives: "1.50" is still a number, and stays "1.50".
    /// </summary>
    private static object ToDbValue(string? value)
    {
        if (value is null)
            return DBNull.Value;

        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var canonical)
            || !string.Equals(canonical.ToString(System.Globalization.CultureInfo.InvariantCulture), value, StringComparison.Ordinal))
            return value;

        // Bound as long/double, never decimal: Microsoft.Data.Sqlite stores a decimal
        // parameter as TEXT, which would reintroduce exactly the mismatch above.
        return long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer)
            ? integer
            : double.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Renders a text template against the resolver's caches: literal runs pass through, each
    /// {dataSet.Column:agg:fmt} expression reads the shared data set cache, {@name} reads the
    /// effective parameter map. A failed expression renders inline (⚠) without breaking the
    /// rest of the text. The same tokens are re-rendered per group at render time by
    /// <see cref="ScopedValues"/> — both go through <see cref="TextRenderer"/>.
    /// </summary>
    private static ResolvedData RenderText(TextElement element, ResolveContext context)
    {
        var text = TextRenderer.Render(
            TextTemplate.Tokenize(element.Content),
            new ResolveTimeScope(context),
            element.RenderHtml);
        return new ResolvedData { Scalar = text };
    }

    /// <summary>Distinct data set names referenced by a text token (the reserved "group" excluded).</summary>
    private static IEnumerable<string> TextDataSetNames(IEnumerable<ReportElement> elements) =>
        elements
            .OfType<TextElement>()
            .SelectMany(element => TextTemplate.Tokenize(element.Content))
            .Select(token => token.Expression)
            .OfType<TextTemplate.DataSetExpr>()
            .Where(expression => !string.Equals(
                expression.DataSet, ScopedValues.GroupDataSet, StringComparison.OrdinalIgnoreCase))
            .Select(expression => expression.DataSet)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Runs a data set's query at most once per resolution; every consumer shares the result.</summary>
    private async Task<ResolvedData> GetDataSetAsync(
        Report report,
        string name,
        ResolveContext context,
        CancellationToken cancellationToken)
    {
        if (context.DataSets.TryGetValue(name, out var cached))
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
                data = await ExecuteAsync(report, definition.Binding, context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                data = new ResolvedData { Error = ex.Message };
            }
        }

        context.DataSets[name] = data;
        return data;
    }

    private async Task<DbConnection> GetOpenConnectionAsync(
        Report report,
        string name,
        ResolveContext context,
        CancellationToken cancellationToken)
    {
        if (context.Connections.TryGetValue(name, out var existing))
            return existing;

        // A connection that already failed stays failed for this resolution. Without this,
        // every remaining binding re-attempts the open and pays the provider's connect
        // timeout again (15s by default) — a dead server turned one slow report into a
        // minutes-long hang.
        if (context.ConnectionErrors.TryGetValue(name, out var failure))
            throw new InvalidOperationException(failure);

        var definition = report.Connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The report declares no connection named '{name}'.");

        string connectionString;
        if (definition.SuppliedAtRuntime)
        {
            // Runtime-supplied: the value comes from the caller and is used verbatim — the
            // cipher is never run on it. The provider still comes from the definition.
            if (!context.ConnectionOverrides.TryGetValue(name, out var overrideValue)
                || string.IsNullOrWhiteSpace(overrideValue))
            {
                var missing = $"Connection '{name}' must be supplied at generation time.";
                context.ConnectionErrors[name] = missing;
                throw new InvalidOperationException(missing);
            }
            connectionString = overrideValue;
        }
        else
        {
            connectionString = cipher.IsEncrypted(definition.ConnectionString)
                ? cipher.Decrypt(definition.ConnectionString)
                : definition.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var missing = $"Connection '{name}' has no connection string.";
                context.ConnectionErrors[name] = missing;
                throw new InvalidOperationException(missing);
            }
        }

        var connection = DbConnectionFactory.Create(definition.Provider, connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The connection never joined the dictionary, so the finally-block cleanup in
            // ResolveAsync would not have disposed it.
            await connection.DisposeAsync();
            // The provider message can carry the connection string (host, user, sometimes
            // the password), and this text reaches a report banner — so keep it to the
            // declared name and log the detail server-side instead.
            logger.LogWarning(ex, "Could not open report connection '{Connection}'.", name);
            var message = $"Could not connect to '{name}'.";
            context.ConnectionErrors[name] = message;
            throw new InvalidOperationException(message);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        context.Connections[name] = connection;
        return connection;
    }

    /// <summary>
    /// Per-resolution state: the report name, the open connections, the data-set cache every
    /// consumer shares, the effective parameter values, the runtime connection-string
    /// overrides, the one generation instant, and the connections that failed to open.
    /// Single-threaded by construction — one resolution at a time — which is why the runtime
    /// inputs can be plain method arguments.
    /// </summary>
    private sealed class ResolveContext(
        string reportName,
        Dictionary<string, string?> parameterValues,
        Dictionary<string, ParameterType> parameterTypes,
        HashSet<string> invalidTypedParameters,
        Dictionary<string, string?> connectionOverrides,
        DateTime generatedAt)
    {
        public string ReportName { get; } = reportName;

        public Dictionary<string, DbConnection> Connections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ResolvedData> DataSets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ConnectionErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Runtime connection strings by connection name, for SuppliedAtRuntime connections only.</summary>
        public Dictionary<string, string?> ConnectionOverrides { get; } = connectionOverrides;

        public Dictionary<string, string?> ParameterValues { get; } = parameterValues;

        /// <summary>Declared type of each valid typed parameter — drives SQL binding conversion.</summary>
        public Dictionary<string, ParameterType> ParameterTypes { get; } = parameterTypes;

        /// <summary>Typed parameters whose value failed to parse — bound as NULL.</summary>
        public HashSet<string> InvalidTypedParameters { get; } = invalidTypedParameters;

        /// <summary>The single instant every <c>{now}</c> in this resolution renders.</summary>
        public DateTime GeneratedAt { get; } = generatedAt;

        public IReadOnlyList<ConnectionFailure> Failures() =>
            ConnectionErrors.Count == 0
                ? []
                : [.. ConnectionErrors.Select(kv => new ConnectionFailure(kv.Key, kv.Value))];

        public async Task DisposeConnectionsAsync()
        {
            foreach (var connection in Connections.Values)
                await connection.DisposeAsync();
            Connections.Clear();
        }
    }

    /// <summary>
    /// <see cref="IValueScope"/> over the resolver's caches: the whole data set, no group. The
    /// text pre-pass has already populated <see cref="ResolveContext.DataSets"/> for every name
    /// a token references, so column lookups are cache hits.
    /// </summary>
    private sealed class ResolveTimeScope(ResolveContext context) : IValueScope
    {
        public bool TryParameter(string name, out string? value) =>
            context.ParameterValues.TryGetValue(name, out value);

        public ColumnResult Column(string dataSet, string? column, ScalarAggregate aggregate)
        {
            if (!context.DataSets.TryGetValue(dataSet, out var resolved))
                return ColumnResult.UnknownDataSet;
            if (resolved.Error is not null)
                return ColumnResult.Failed(resolved.Error);
            if (resolved.Table is null)
                return ColumnResult.Found(null);
            return ColumnResult.Found(ScalarValues.Extract(resolved.Table, column, aggregate));
        }

        // Resolve time has no group — {group.x} is resolved per group at render time.
        public GroupStatus Group(string? column, out object? value)
        {
            value = null;
            return GroupStatus.NotInGroup;
        }

        public string ReportName => context.ReportName;

        public DateTime Now => context.GeneratedAt;
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
