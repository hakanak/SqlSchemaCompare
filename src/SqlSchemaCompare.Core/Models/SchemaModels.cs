namespace SqlSchemaCompare.Core.Models;

public enum SchemaObjectType { Table, Column, Index, PrimaryKey, ForeignKey, UniqueConstraint, DefaultConstraint, View, StoredProcedure, Function, Trigger }
public enum DifferenceType { Added, Removed, Modified }
public sealed record DatabaseSchema(IReadOnlyList<TableSchema> Tables, IReadOnlyList<IndexSchema> Indexes, IReadOnlyList<ModuleSchema> Modules);
public sealed record TableSchema(string Schema, string Name, IReadOnlyList<ColumnSchema> Columns, IReadOnlyList<ConstraintSchema> Constraints);
public sealed record ColumnSchema(string Schema, string Table, string Name, string TypeName, int? Length, byte? Precision, byte? Scale, bool IsNullable, bool IsIdentity, string? DefaultDefinition, string? DefaultName);
public sealed record IndexSchema(string Schema, string Table, string Name, bool IsUnique, bool IsClustered, IReadOnlyList<string> Columns, IReadOnlyList<string> IncludedColumns, string? FilterDefinition);
public sealed record ConstraintSchema(SchemaObjectType ObjectType, string Name, IReadOnlyList<string> Columns, string? Definition);
public sealed record ModuleSchema(SchemaObjectType ObjectType, string Schema, string Name, string Definition);
public sealed class SchemaDifference
{
    public SchemaObjectType ObjectType { get; init; }
    public string SchemaName { get; init; } = "dbo";
    public string ObjectName { get; init; } = "";
    public DifferenceType DifferenceType { get; init; }
    public string Description { get; init; } = "";
    public string? SourceDefinition { get; init; }
    public string? TargetDefinition { get; init; }
    public string? GeneratedSql { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsPotentiallyDestructive { get; init; }
}
