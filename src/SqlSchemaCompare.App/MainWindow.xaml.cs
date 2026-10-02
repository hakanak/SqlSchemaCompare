using System.Windows;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using SqlSchemaCompare.Core.Comparers;
using SqlSchemaCompare.Core.Models;
using SqlSchemaCompare.Core.ScriptGeneration;
using SqlSchemaCompare.Infrastructure.SqlServer;

namespace SqlSchemaCompare.App;

public partial class MainWindow : Window
{
    private List<SchemaDifference> differences = [];
    private static readonly string ProfileFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlSchemaCompare", "profiles.json");
    private readonly record struct ConnectionProfile(string Name, string SourceServer, string SourceDatabase, string SourceUser, string SourcePassword, string TargetServer, string TargetDatabase, string TargetUser, string TargetPassword);

    public MainWindow() { InitializeComponent(); RefreshProfiles(); }

    private void SaveProfile(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProfileName.Text)) { MessageBox.Show("Enter a profile name first.", "Profile"); return; }
        var profiles = ReadProfiles().Where(x => !x.Name.Equals(ProfileName.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        profiles.Add(new ConnectionProfile(ProfileName.Text.Trim(), SourceServer.Text, SourceDatabase.Text, SourceUser.Text, Protect(SourcePassword.Password), TargetServer.Text, TargetDatabase.Text, TargetUser.Text, Protect(TargetPassword.Password)));
        Directory.CreateDirectory(Path.GetDirectoryName(ProfileFile)!);
        File.WriteAllText(ProfileFile, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
        RefreshProfiles(); SetReady($"Profile '{ProfileName.Text.Trim()}' saved locally.");
    }

    private void LoadProfile(object sender, RoutedEventArgs e)
    {
        if (Profiles.SelectedItem is not string name) { MessageBox.Show("Select a profile first.", "Profile"); return; }
        var profile = ReadProfiles().FirstOrDefault(x => x.Name == name);
        SourceServer.Text = profile.SourceServer; SourceDatabase.Text = profile.SourceDatabase; SourceUser.Text = profile.SourceUser; SourcePassword.Password = Unprotect(profile.SourcePassword);
        TargetServer.Text = profile.TargetServer; TargetDatabase.Text = profile.TargetDatabase; TargetUser.Text = profile.TargetUser; TargetPassword.Password = Unprotect(profile.TargetPassword); ProfileName.Text = profile.Name;
        SetReady($"Profile '{name}' loaded.");
    }

    private void RefreshProfiles() { Profiles.ItemsSource = ReadProfiles().Select(x => x.Name).OrderBy(x => x).ToList(); }
    private static List<ConnectionProfile> ReadProfiles() => File.Exists(ProfileFile) ? JsonSerializer.Deserialize<List<ConnectionProfile>>(File.ReadAllText(ProfileFile)) ?? [] : [];
    private static string Protect(string value) => Convert.ToBase64String(ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    private static string Unprotect(string value) { try { return System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); } catch { return ""; } }

    private static string ConnectionString(string server, string database, string user, string password) =>
        new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, UserID = user, Password = password, IntegratedSecurity = string.IsNullOrWhiteSpace(user), TrustServerCertificate = true, Encrypt = false, ConnectTimeout = 10 }.ConnectionString;

    private void SetBusy(string message) { ActivityProgress.Visibility = Visibility.Visible; StatusText.Text = message; }
    private void SetReady(string message) { ActivityProgress.Visibility = Visibility.Collapsed; StatusText.Text = message; }
    private void TransferSourceAccess(object sender, RoutedEventArgs e)
    {
        TargetServer.Text = SourceServer.Text;
        TargetUser.Text = SourceUser.Text;
        TargetPassword.Password = SourcePassword.Password;
        SetReady("Server, username and password transferred to target.");
    }

    private void SelectAllToggle(object sender, RoutedEventArgs e)
    {
        var selectAll = (sender as System.Windows.Controls.CheckBox)?.IsChecked == true;
        foreach (var difference in differences) difference.IsSelected = selectAll;
        Differences.Items.Refresh();
        SetReady(selectAll ? "All differences selected." : "All differences unselected.");
    }

    private async void TestSource(object sender, RoutedEventArgs e) => await TestConnection("source", SourceServer, SourceDatabase, SourceUser, SourcePassword);
    private async void TestTarget(object sender, RoutedEventArgs e) => await TestConnection("target", TargetServer, TargetDatabase, TargetUser, TargetPassword);

    private async Task TestConnection(string side, System.Windows.Controls.TextBox server, System.Windows.Controls.TextBox database, System.Windows.Controls.TextBox user, System.Windows.Controls.PasswordBox password)
    {
        SetBusy($"Testing {side} connection...");
        try
        {
            await using var connection = new SqlConnection(ConnectionString(server.Text.Trim(), database.Text.Trim(), user.Text.Trim(), password.Password));
            await connection.OpenAsync();
            SetReady($"✓ {side} connection successful");
            MessageBox.Show($"✓ {side} connection successful", "Connection test", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (SqlException ex)
        {
            SetReady($"✕ {side} connection failed");
            MessageBox.Show($"Database connection failed.\n\nServer: {server.Text}\nDatabase: {database.Text}\n\nReason:\n{ex.Message}", "Connection test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            SetReady($"✕ {side} connection failed");
            MessageBox.Show($"Unexpected error while testing the connection.\n\n{ex.Message}", "Connection test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Compare(object sender, RoutedEventArgs e)
    {
        SetBusy("Reading source and target schemas...");
        try
        {
            var sourceReader = new SqlServerSchemaReader(ConnectionString(SourceServer.Text.Trim(), SourceDatabase.Text.Trim(), SourceUser.Text.Trim(), SourcePassword.Password));
            var targetReader = new SqlServerSchemaReader(ConnectionString(TargetServer.Text.Trim(), TargetDatabase.Text.Trim(), TargetUser.Text.Trim(), TargetPassword.Password));
            var source = await sourceReader.ReadAsync();
            SetBusy("Reading target schema...");
            var target = await targetReader.ReadAsync();
            differences = new SchemaComparer().Compare(source, target).ToList();
            Differences.ItemsSource = differences;
            SetReady($"Compare complete — {differences.Count} difference(s) found.");
        }
        catch (SqlException ex)
        {
            SetReady("Compare failed.");
            MessageBox.Show($"SQL Server returned an error while reading schema metadata.\n\n{ex.Message}", "Compare", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            SetReady("Compare failed.");
            MessageBox.Show($"Unexpected error while comparing schemas.\n\n{ex.Message}", "Compare", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Generate(object sender, RoutedEventArgs e)
    {
        Script.Text = new SqlScriptGenerator().Generate(differences, SourceDatabase.Text.Trim(), TargetDatabase.Text.Trim(), AllowDestructive.IsChecked == true);
        SetReady("SQL script generated. Review it before executing.");
    }

    private void Copy(object sender, RoutedEventArgs e) { Clipboard.SetText(Script.Text); SetReady("Generated SQL copied to clipboard."); }
    private void Clear(object sender, RoutedEventArgs e) { Script.Clear(); SetReady("Generated SQL cleared."); }
    private void Save(object sender, RoutedEventArgs e) { var dialog = new SaveFileDialog { Filter = "SQL files|*.sql", DefaultExt = ".sql" }; if (dialog.ShowDialog() == true) { File.WriteAllText(dialog.FileName, Script.Text); SetReady("SQL file saved."); } }
}
