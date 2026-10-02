using SqlSchemaCompare.Core.Models;
namespace SqlSchemaCompare.Core.Interfaces;
public interface ISchemaReader { Task<DatabaseSchema> ReadAsync(CancellationToken cancellationToken = default); }
public interface ISchemaComparer { IReadOnlyList<SchemaDifference> Compare(DatabaseSchema source, DatabaseSchema target); }
public interface IScriptGenerator { string Generate(IEnumerable<SchemaDifference> differences, string sourceDatabase, string targetDatabase, bool allowDestructiveChanges = false); }
