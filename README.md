# SQL Server Schema Compare

Internal .NET 8/WPF tool for the safe workflow: **compare first, generate script second, execute manually**.

## Build and test

```powershell
dotnet restore SqlSchemaCompare.sln
dotnet build SqlSchemaCompare.sln
dotnet test tests/SqlSchemaCompare.Tests/SqlSchemaCompare.Tests.csproj
```

Run the WPF project with `dotnet run --project src/SqlSchemaCompare.App`.

## Usage

Enter source and target SQL Server details, test both connections, compare, review/select differences, then generate and copy/save the SQL. The application only reads metadata; it never deploys changes. Target-only objects produce review comments, not DROP statements. Destructive generation is opt-in and should be reviewed manually.

## Supported objects

Tables and columns, indexes, views, stored procedures, scalar/table-valued functions and triggers are read. Definitions are whitespace-normalized for comparison. SQL Server-specific catalog views are used.

## Limitations

Constraint and trigger dependency scripting is intentionally conservative in this first version. Connection profiles, DPAPI storage, richer progress reporting and side-by-side definition diff are planned extensions. Credentials are not persisted or logged.

## Problems solved during the first implementation

- The initial screen had unlabeled inputs, so the UI now explains Server/Instance, Database, Username and Password and includes a guided workflow.
- Source and target can share the same SQL Server. The `Transfer access details` button copies server, username and password in one action without overwriting database names.
- Connection tests now show progress and a clear success/failure status. SQL errors are surfaced without logging passwords.
- Connection profiles are stored under `%LOCALAPPDATA%\SqlSchemaCompare\profiles.json`; passwords are encrypted with Windows DPAPI rather than stored as plain text.
- SQL Server 2016 does not support `STRING_AGG`. Index column metadata now uses the compatible `FOR XML PATH` approach, so the reader works without requiring SQL Server 2017+.
- The metadata query previously used `i.type=1` directly in a SELECT projection. It now uses a SQL Server-compatible `CASE` expression.
- Differences and generated SQL are displayed side by side so the generated script is easier to review.
- A single `Select all differences` checkbox controls whether selected differences are included in the generated script.

## Safety model

The tool reads source and target metadata only. It never executes CREATE, ALTER, DROP, UPDATE or DELETE against either database. Target-only objects are shown for review and do not generate DROP statements unless the user explicitly enables destructive changes. Always review and test the generated script before running it in SSMS.
