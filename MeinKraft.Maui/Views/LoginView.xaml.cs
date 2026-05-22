namespace MeinKraft.Maui.Views;

public partial class LoginView : ContentPage
{
    private readonly IPlayerApiService _playerApi;

    private bool _returningPlayerMode = false;
    private bool _justRegistered = false;
    private string? _registeredApiKey;

    public LoginView(IPlayerApiService playerApi)
    {
        InitializeComponent();
        _playerApi = playerApi;
        PreFillSavedCredentials();
    }

    // ── Footer tap — reveal returning player fields ───────────────────────────

    private void OnFooterTapped(object sender, EventArgs e)
    {
        if (_returningPlayerMode)
        {
            // Toggle back to new player mode
            _returningPlayerMode = false;
            _justRegistered = false;
            ApiKeyPanel.IsVisible = false;
            KeyResultPanel.IsVisible = false;
            ActionButton.Text = "CREATE ACCOUNT";
            FooterLabel.Text = "Already a player? Access your worlds!";
            UsernameEntry.Placeholder = "pick something legendary";
            StatusLabel.IsVisible = false;
        }
        else
        {
            // Reveal API key field for returning players
            _returningPlayerMode = true;
            ApiKeyPanel.IsVisible = true;
            ApiKeyEntry.Focus();
            ActionButton.Text = "PLAY";
            FooterLabel.Text = "New here? Create a free account!";
            UsernameEntry.Placeholder = "your username";
            StatusLabel.IsVisible = false;
        }
    }

    // ── Main action — context-sensitive ──────────────────────────────────────

    private async void OnActionClicked(object sender, EventArgs e)
    {
        // After registration the credentials are already saved — go straight in
        if (_justRegistered)
        {
            await NavigateToGame();
            return;
        }

        if (_returningPlayerMode)
            await Login();
        else
            await Register();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    private async Task Login()
    {
        string username = UsernameEntry.Text?.Trim() ?? string.Empty;
        string apiKey = ApiKeyEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey))
        {
            ShowStatus("fill in both fields.", isError: true);
            return;
        }

        SaveCredentials(username, apiKey);
        await NavigateToGame();
    }

    // ── Register ──────────────────────────────────────────────────────────────

    private async Task Register()
    {
        string username = UsernameEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("choose a username first.", isError: true);
            return;
        }

        ActionButton.IsEnabled = false;
        ShowStatus("checking availability...");

        bool exists = await _playerApi.ExistsAsync(username);
        if (exists)
        {
            ShowStatus("that name is taken, try another.", isError: true);
            ActionButton.IsEnabled = true;
            return;
        }

        ShowStatus("creating your account...");
        var result = await _playerApi.RegisterAsync(username);

        if (result is null)
        {
            ShowStatus("something went wrong, try again.", isError: true);
            ActionButton.IsEnabled = true;
            return;
        }

        _registeredApiKey = result.ApiKey;
        _justRegistered = true;
        SaveCredentials(result.Username, result.ApiKey);

        ApiKeyResultLabel.Text = result.ApiKey;
        KeyResultPanel.IsVisible = true;
        StatusLabel.IsVisible = false;
        ActionButton.Text = "PLAY";
        ActionButton.IsEnabled = true;
        FooterLabel.IsVisible = false;
    }

    // ── Copy key ──────────────────────────────────────────────────────────────

    private async void OnCopyKeyTapped(object sender, EventArgs e)
    {
        if (_registeredApiKey is null) return;
        await Clipboard.SetTextAsync(_registeredApiKey);
        ShowStatus("key copied!", isError: false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PreFillSavedCredentials()
    {
        string? savedKey = Microsoft.Maui.Storage.Preferences.Get("api_key", null);
        string? savedUsername = Microsoft.Maui.Storage.Preferences.Get("username", null);

        if (!string.IsNullOrEmpty(savedKey) && !string.IsNullOrEmpty(savedUsername))
        {
            // Returning player — pre-fill and show key field automatically
            UsernameEntry.Text = savedUsername;
            ApiKeyEntry.Text = savedKey;
            _returningPlayerMode = true;
            ApiKeyPanel.IsVisible = true;
            ActionButton.Text = "PLAY";
            FooterLabel.Text = "New here? Create a free account!";
            UsernameEntry.Placeholder = "your username";
        }
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError
            ? Color.FromArgb("#e05050")
            : Color.FromArgb("#e8a838");
        StatusLabel.IsVisible = true;
    }

    private static void SaveCredentials(string username, string apiKey)
    {
        Microsoft.Maui.Storage.Preferences.Set("api_key", apiKey);
        Microsoft.Maui.Storage.Preferences.Set("username", username);
    }

    private static async Task NavigateToGame()
        => await Shell.Current.GoToAsync("//SinglePlayerView");
}