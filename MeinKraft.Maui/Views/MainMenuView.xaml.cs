using MeinKraft.Maui.Services;

namespace MeinKraft.Maui.Views;

public partial class MainMenuView : ContentPage
{
	public MainMenuView(IGameWindowService gameWindowService)
	{
		InitializeComponent();
#if WINDOWS
        ((MauiGameWindowService) gameWindowService).ReleaseCursor();
#endif
    }

    private async void OnSinglePlayerClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//SinglePlayerView");

    private void OnMultiPlayerClicked(object sender, EventArgs e)
    {

    }

    private void OnExitClicked(object sender, EventArgs e) => Application.Current?.Quit();
}