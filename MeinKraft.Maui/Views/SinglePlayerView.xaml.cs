using System.Collections.ObjectModel;

namespace MeinKraft.Maui.Views;

public partial class SinglePlayerView : ContentPage
{
    private readonly IWorldApiService _worldApi;
    private readonly ISessionApiService _sessionApi;

    public ObservableCollection<WorldInfo> Worlds { get; } = [];

    // Commands bound from CollectionView item templates via RelativeSource
    public Command<WorldInfo> PlaySlotCommand { get; }
    public Command<WorldInfo> DeleteSlotCommand { get; }

    public SinglePlayerView(IWorldApiService worldApi, ISessionApiService sessionApi)
    {
        InitializeComponent();
        _worldApi = worldApi;
        _sessionApi = sessionApi;

        PlaySlotCommand = new Command<WorldInfo>(async w => await PlayWorldAsync(w));
        DeleteSlotCommand = new Command<WorldInfo>(async w => await DeleteWorldAsync(w));

        BindingContext = this;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadWorldsAsync();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadWorldsAsync()
    {
        ShowLoading();

        try
        {
            IReadOnlyList<WorldInfo> worlds = await _worldApi.ListAsync();

            Worlds.Clear();
            foreach (WorldInfo? w in worlds)
            {
                Worlds.Add(w);
            }

            if (Worlds.Count == 0)
            {
                ShowEmpty();
            }
            else
            {
                ShowList();
            }
        }
        catch (Exception ex)
        {
            ShowError($"could not load worlds:\n{ex.Message}");
        }
    }

    // ── Play ──────────────────────────────────────────────────────────────────

    private async Task PlayWorldAsync(WorldInfo world)
    {
        ShowStatus($"starting \"{world.Name}\"...");

        try
        {
            StartSessionResponse? session = await _sessionApi.StartAsync(world.Name);
            if (session is null)
            {
                ShowStatus("failed to start session.", isError: true);
                return;
            }

            Microsoft.Maui.Storage.Preferences.Set("session_port", session.Port);
            Microsoft.Maui.Storage.Preferences.Set("session_id", session.SessionId.ToString());
            Microsoft.Maui.Storage.Preferences.Set("server_ip", "192.168.0.101");

            await Shell.Current.GoToAsync("//GameView");
        }
        catch (Exception ex)
        {
            ShowStatus($"error: {ex.Message}", isError: true);
        }
    }

    // ── New world ─────────────────────────────────────────────────────────────

    private async void OnNewWorldClicked(object sender, EventArgs e)
    {
        string defaultName = $"World {DateTime.Now:yyyyMMdd-HHmm}";

        string? name = await DisplayPromptAsync(
            title: "New World",
            message: "Name your world:",
            accept: "Create",
            cancel: "Cancel",
            placeholder: defaultName,
            maxLength: 32,
            keyboard: Keyboard.Text);

        if (name is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = defaultName;
        }

        ShowStatus("creating world...");

        try
        {
            WorldInfo? world = await _worldApi.CreateAsync(name);
            if (world is null)
            {
                ShowStatus("failed to create world.", isError: true);
                return;
            }

            await PlayWorldAsync(world);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            ShowStatus("a world with that name already exists.", isError: true);
        }
        catch (Exception ex)
        {
            ShowStatus($"error: {ex.Message}", isError: true);
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private async Task DeleteWorldAsync(WorldInfo world)
    {
        bool confirmed = await DisplayAlertAsync(
            "Delete world?",
            $"\"{world.Name}\" will be permanently deleted.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        ShowStatus("deleting...");

        try
        {
            await _worldApi.DeleteAsync(world.Name);
            Worlds.Remove(world);
            StatusLabel.IsVisible = false;

            if (Worlds.Count == 0)
            {
                ShowEmpty();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"could not delete: {ex.Message}", isError: true);
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async void OnBackClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//MainMenuView");

    private async void OnRetryClicked(object sender, EventArgs e)
        => await LoadWorldsAsync();

    // ── State helpers ─────────────────────────────────────────────────────────

    private void ShowLoading()
    {
        LoadingState.IsVisible = true;
        EmptyState.IsVisible = false;
        ErrorState.IsVisible = false;
        WorldList.IsVisible = false;
        StatusLabel.IsVisible = false;
    }

    private void ShowEmpty()
    {
        LoadingState.IsVisible = false;
        EmptyState.IsVisible = true;
        ErrorState.IsVisible = false;
        WorldList.IsVisible = false;
    }

    private void ShowList()
    {
        LoadingState.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorState.IsVisible = false;
        WorldList.IsVisible = true;
        WorldList.ItemsSource = Worlds;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        LoadingState.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorState.IsVisible = true;
        WorldList.IsVisible = false;
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError
            ? Color.FromArgb("#e05050")
            : Color.FromArgb("#e8a838");
        StatusLabel.IsVisible = true;
    }
}