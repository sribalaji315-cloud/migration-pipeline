using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using Haworth.MdPipeline.Pipeline.Options;

namespace Haworth.MdPipeline.Pipeline.Duck;

public sealed partial class DuckDbExecutor : IDisposable
{
    private readonly DuckDBConnection connection;

    private DuckDbExecutor(DuckDBConnection connection) => this.connection = connection;

    public static DuckDbExecutor Open(string? databasePath, PipelineOptions options)
    {
        var source = databasePath is null ? ":memory:" : Path.GetFullPath(databasePath);
        var connection = new DuckDBConnection(
            $"Data Source={source};memory_limit={options.DuckDbMemoryLimit};threads={options.DuckDbThreads}");
        connection.Open();
        return new DuckDbExecutor(connection);
    }

    public static DuckDbExecutor OpenReadOnly(string databasePath, PipelineOptions options)
    {
        var connection = new DuckDBConnection(
            $"Data Source={Path.GetFullPath(databasePath)};access_mode=READ_ONLY;memory_limit={options.DuckDbMemoryLimit};threads={options.DuckDbThreads}");
        connection.Open();
        return new DuckDbExecutor(connection);
    }

    public void ExecuteScript(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void ExecuteFile(string fileName, IReadOnlyDictionary<string, string>? literals = null) =>
        ExecuteScript(SqlScriptLoader.Load(fileName, literals));

    public object? Scalar(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    public long CountRows(string table)
    {
        EnsureIdentifier(table);
        return Convert.ToInt64(Scalar($"SELECT count(*) FROM {table}"));
    }

    public List<Dictionary<string, object?>> Query(string sql, params object?[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new DuckDBParameter(parameter ?? DBNull.Value));
        }

        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    public long Append(string table, IEnumerable<object?[]> rows)
    {
        EnsureIdentifier(table);
        long count = 0;
        using var appender = connection.CreateAppender(table);
        foreach (var values in rows)
        {
            AppendRow(appender, values);
            count++;
        }

        return count;
    }

    public async Task<long> AppendAsync(string table, IAsyncEnumerable<object?[]> rows, CancellationToken ct)
    {
        EnsureIdentifier(table);
        long count = 0;
        using var appender = connection.CreateAppender(table);
        await foreach (var values in rows.WithCancellation(ct))
        {
            AppendRow(appender, values);
            count++;
        }

        return count;
    }

    private static void AppendRow(DuckDBAppender appender, object?[] values)
    {
        var row = appender.CreateRow();
        foreach (var value in values)
        {
            switch (value)
            {
                case null: row.AppendNullValue(); break;
                case string s: row.AppendValue(s); break;
                case bool b: row.AppendValue(b); break;
                case int i: row.AppendValue(i); break;
                case long l: row.AppendValue(l); break;
                case double d: row.AppendValue(d); break;
                case decimal m: row.AppendValue(m); break;
                case DateTime dt: row.AppendValue(dt); break;
                case DateOnly date: row.AppendValue(date); break;
                default: throw new NotSupportedException($"Cannot append value of type {value.GetType().Name}.");
            }
        }

        row.EndRow();
    }

    public static void EnsureIdentifier(string name)
    {
        if (!Identifier().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid table identifier.", nameof(name));
        }
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex Identifier();

    public void Dispose() => connection.Dispose();
}
