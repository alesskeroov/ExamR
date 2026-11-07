// Copies all ExamFlow data from the SQL Server database into an empty PostgreSQL database that already has the
// InitialPostgres migration applied. Source is only read. The target load runs in one transaction: on any error
// nothing is written. Connection strings come from environment variables and are never printed.
//
//   SOURCE_SQLSERVER=<sql server connection string>
//   TARGET_POSTGRES=<npgsql connection string, direct (non-pooled) endpoint>
//   dotnet run --project scripts/migrate-to-postgres            # copy + verify
//   dotnet run --project scripts/migrate-to-postgres -- --verify # only compare row counts
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql;

const string ExpectedSourceMigration = "20261107121520_AddExamAnswerLock";
const string ExpectedTargetMigration = "_InitialPostgres";
const string History = "__EFMigrationsHistory";

var verifyOnly = args.Contains("--verify");
var source = Environment.GetEnvironmentVariable("SOURCE_SQLSERVER");
var target = Environment.GetEnvironmentVariable("TARGET_POSTGRES");
if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
    return Fail("Set SOURCE_SQLSERVER and TARGET_POSTGRES environment variables.");

await using var sql = new SqlConnection(source);
await using var pg = new NpgsqlConnection(target);
await sql.OpenAsync();
await pg.OpenAsync();
Console.WriteLine($"Source: {sql.DataSource} / {sql.Database}");
Console.WriteLine($"Target: {pg.Host} / {pg.Database}");

var sourceMigration = (string?)await new SqlCommand($"SELECT TOP 1 MigrationId FROM [{History}] ORDER BY MigrationId DESC", sql).ExecuteScalarAsync();
if (sourceMigration != ExpectedSourceMigration)
    return Fail($"Source is at migration '{sourceMigration}', expected '{ExpectedSourceMigration}'. Update the source first.");
var targetMigration = (string?)await new NpgsqlCommand($"SELECT \"MigrationId\" FROM \"{History}\" ORDER BY \"MigrationId\" DESC LIMIT 1", pg).ExecuteScalarAsync();
if (targetMigration is null || !targetMigration.EndsWith(ExpectedTargetMigration))
    return Fail($"Target is at migration '{targetMigration}', expected '*{ExpectedTargetMigration}'. Run 'dotnet ef database update' on the target first.");

var tables = await TargetTablesAsync(pg);
if (!verifyOnly)
{
    foreach (var table in tables)
        if (await CountPgAsync(pg, table.Name) > 0)
            return Fail($"Target table '{table.Name}' is not empty. The copy only runs into an empty database.");

    await using var tx = await pg.BeginTransactionAsync();
    foreach (var table in tables)
    {
        var copied = await CopyTableAsync(sql, pg, table);
        Console.WriteLine($"  copied {table.Name,-28} {copied,8}");
    }
    foreach (var table in tables)
        foreach (var column in table.Columns.Where(c => c.IsIdentity))
            await new NpgsqlCommand($"SELECT setval(pg_get_serial_sequence('\"{table.Name}\"', '{column.Name}'), COALESCE(MAX(\"{column.Name}\"), 0) + 1, false) FROM \"{table.Name}\"", pg, tx).ExecuteScalarAsync();
    await tx.CommitAsync();
    Console.WriteLine("Committed.");
}

// Verification: every table must have the same row count on both sides.
var mismatches = 0;
long total = 0;
foreach (var table in tables)
{
    var s = System.Convert.ToInt64(await new SqlCommand($"SELECT COUNT_BIG(*) FROM [{table.Name}]", sql).ExecuteScalarAsync());
    var t = await CountPgAsync(pg, table.Name);
    total += t;
    if (s != t) { mismatches++; Console.WriteLine($"  MISMATCH {table.Name}: source {s}, target {t}"); }
}
Console.WriteLine(mismatches == 0 ? $"Verified: {tables.Count} tables, {total} rows, all counts match." : $"{mismatches} table(s) differ.");
return mismatches == 0 ? 0 : 1;

static int Fail(string message)
{
    Console.Error.WriteLine("ERROR: " + message);
    return 2;
}

static async Task<long> CountPgAsync(NpgsqlConnection pg, string table) =>
    System.Convert.ToInt64(await new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", pg).ExecuteScalarAsync());

// Public tables in foreign-key order (parents first) with their columns as PostgreSQL sees them.
static async Task<List<Table>> TargetTablesAsync(NpgsqlConnection pg)
{
    var columns = new Dictionary<string, List<Column>>();
    await using (var reader = await new NpgsqlCommand("""
        SELECT c.table_name, c.column_name, c.udt_name, c.is_identity = 'YES'
        FROM information_schema.columns c JOIN information_schema.tables t
          ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
        WHERE c.table_schema = 'public' AND c.table_name <> '__EFMigrationsHistory'
        ORDER BY c.table_name, c.ordinal_position
        """, pg).ExecuteReaderAsync())
        while (await reader.ReadAsync())
        {
            if (!columns.TryGetValue(reader.GetString(0), out var list)) columns[reader.GetString(0)] = list = [];
            list.Add(new Column(reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        }

    var parents = columns.Keys.ToDictionary(k => k, _ => new HashSet<string>());
    await using (var reader = await new NpgsqlCommand("""
        SELECT DISTINCT cl.relname, pr.relname FROM pg_constraint co
        JOIN pg_class cl ON cl.oid = co.conrelid JOIN pg_class pr ON pr.oid = co.confrelid
        JOIN pg_namespace n ON n.oid = cl.relnamespace
        WHERE co.contype = 'f' AND n.nspname = 'public'
        """, pg).ExecuteReaderAsync())
        while (await reader.ReadAsync())
            if (reader.GetString(0) != reader.GetString(1) && parents.ContainsKey(reader.GetString(0)))
                parents[reader.GetString(0)].Add(reader.GetString(1));

    var ordered = new List<Table>();
    while (parents.Count > 0)
    {
        var ready = parents.Where(p => p.Value.All(d => !parents.ContainsKey(d))).Select(p => p.Key).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (ready.Count == 0) throw new InvalidOperationException("Foreign-key cycle between: " + string.Join(", ", parents.Keys));
        foreach (var name in ready) { ordered.Add(new Table(name, columns[name])); parents.Remove(name); }
    }
    return ordered;
}

static async Task<long> CopyTableAsync(SqlConnection sql, NpgsqlConnection pg, Table table)
{
    // Columns missing in PostgreSQL (e.g. SQL Server rowversion, now the xmin system column) are not copied;
    // a PostgreSQL column missing in the source is an error.
    var sourceColumns = new HashSet<string>(StringComparer.Ordinal);
    await using (var reader = await new SqlCommand("SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@t)", sql) { Parameters = { new("@t", table.Name) } }.ExecuteReaderAsync())
        while (await reader.ReadAsync()) sourceColumns.Add(reader.GetString(0));
    if (sourceColumns.Count == 0) throw new InvalidOperationException($"Source table '{table.Name}' does not exist.");
    var missing = table.Columns.Where(c => !sourceColumns.Contains(c.Name)).Select(c => c.Name).ToList();
    if (missing.Count > 0) throw new InvalidOperationException($"Source table '{table.Name}' has no column(s): {string.Join(", ", missing)}");

    var select = $"SELECT {string.Join(", ", table.Columns.Select(c => $"[{c.Name}]"))} FROM [{table.Name}]";
    await using var source = await new SqlCommand(select, sql) { CommandTimeout = 0 }.ExecuteReaderAsync();
    await using var writer = await pg.BeginBinaryImportAsync(
        $"COPY \"{table.Name}\" ({string.Join(", ", table.Columns.Select(c => $"\"{c.Name}\""))}) FROM STDIN (FORMAT BINARY)");
    while (await source.ReadAsync())
    {
        await writer.StartRowAsync();
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            if (source.IsDBNull(i)) { await writer.WriteNullAsync(); continue; }
            await writer.WriteAsync(ToTarget(source.GetValue(i), column, table.Name), column.DataTypeName);
        }
    }
    return (long)await writer.CompleteAsync();
}

static object ToTarget(object value, Column column, string table) => column.Udt switch
{
    // EF primitive collections: JSON text on SQL Server, native arrays on PostgreSQL.
    "_uuid" => JsonSerializer.Deserialize<Guid[]>((string)value) ?? [],
    "timestamptz" => value switch
    {
        DateTimeOffset d => d.ToUniversalTime(),
        DateTime d => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)),
        _ => throw Unexpected(value, column, table),
    },
    "int2" => System.Convert.ToInt16(value),
    "int4" => System.Convert.ToInt32(value),
    "int8" => System.Convert.ToInt64(value),
    "float8" => System.Convert.ToDouble(value),
    "numeric" => System.Convert.ToDecimal(value),
    "bool" => System.Convert.ToBoolean(value),
    "uuid" => value is Guid ? value : throw Unexpected(value, column, table),
    "bytea" => value is byte[] ? value : throw Unexpected(value, column, table),
    "text" or "varchar" or "bpchar" or "jsonb" => value is string ? value : throw Unexpected(value, column, table),
    _ => throw new InvalidOperationException($"{table}.{column.Name}: unsupported PostgreSQL type '{column.Udt}'."),
};

static InvalidOperationException Unexpected(object value, Column column, string table) =>
    new($"{table}.{column.Name}: cannot convert {value.GetType().Name} to '{column.Udt}'.");

sealed record Table(string Name, List<Column> Columns);

sealed record Column(string Name, string Udt, bool IsIdentity)
{
    public string DataTypeName => Udt.StartsWith('_') ? Udt[1..] + "[]" : Udt;
}
