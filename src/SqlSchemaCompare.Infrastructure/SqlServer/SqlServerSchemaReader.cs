using Microsoft.Data.SqlClient;
using SqlSchemaCompare.Core.Interfaces;
using SqlSchemaCompare.Core.Models;

namespace SqlSchemaCompare.Infrastructure.SqlServer;

public sealed class SqlServerSchemaReader(string connectionString) : ISchemaReader
{
    public async Task<DatabaseSchema> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var tables = new List<TableSchema>();
        var indexes = new List<IndexSchema>();
        var modules = new List<ModuleSchema>();
        var columns = new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase);

        await Query(connection, "SELECT s.name,t.name,c.name,ty.name,CASE WHEN ty.name IN ('nvarchar','nchar','varchar','char','varbinary') THEN c.max_length ELSE NULL END,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition,dc.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id ORDER BY s.name,t.name,c.column_id", reader =>
        {
            var column = new ColumnSchema(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt16(4), reader.IsDBNull(5) ? null : reader.GetByte(5), reader.IsDBNull(6) ? null : reader.GetByte(6), reader.GetBoolean(7), reader.GetBoolean(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10));
            var key = column.Schema + "." + column.Table;
            if (!columns.TryGetValue(key, out var list)) columns[key] = list = [];
            list.Add(column);
        }, cancellationToken);
        foreach (var group in columns.Values) tables.Add(new TableSchema(group[0].Schema, group[0].Table, group, []));

        // CASE is intentional: SQL Server does not accept `i.type=1` as a SELECT expression on all versions.
        await Query(connection, "SELECT s.name,t.name,i.name,i.is_unique,CASE WHEN i.type=1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,(SELECT STUFF((SELECT ',' + c2.name FROM sys.index_columns ic2 JOIN sys.columns c2 ON c2.object_id=ic2.object_id AND c2.column_id=ic2.column_id WHERE ic2.object_id=i.object_id AND ic2.index_id=i.index_id AND ic2.is_included_column=0 ORDER BY ic2.key_ordinal FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,'')),(SELECT STUFF((SELECT ',' + c3.name FROM sys.index_columns ic3 JOIN sys.columns c3 ON c3.object_id=ic3.object_id AND c3.column_id=ic3.column_id WHERE ic3.object_id=i.object_id AND ic3.index_id=i.index_id AND ic3.is_included_column=1 ORDER BY ic3.index_column_id FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,'')),i.filter_definition FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE i.name IS NOT NULL AND i.is_hypothetical=0", reader =>
            indexes.Add(new IndexSchema(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4), Split(reader, 5), Split(reader, 6), reader.IsDBNull(7) ? null : reader.GetString(7))), cancellationToken);

        await Query(connection, "SELECT o.type,s.name,o.name,m.definition FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id JOIN sys.sql_modules m ON m.object_id=o.object_id WHERE o.type IN ('V','P','FN','IF','TF','TR')", reader => modules.Add(new ModuleSchema(Map(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3))), cancellationToken);
        return new DatabaseSchema(tables, indexes, modules);
    }

    private static async Task Query(SqlConnection connection, string sql, Action<SqlDataReader> row, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) row(reader);
    }

    private static string[] Split(SqlDataReader reader, int index) => reader.IsDBNull(index) ? [] : reader.GetString(index).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static SchemaObjectType Map(string type) => type switch { "V" => SchemaObjectType.View, "P" => SchemaObjectType.StoredProcedure, "TR" => SchemaObjectType.Trigger, _ => SchemaObjectType.Function };
}
