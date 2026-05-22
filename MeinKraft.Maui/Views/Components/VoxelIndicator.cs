namespace MeinKraft.Maui.Views.Components;

public sealed class VoxelIndicator : ContentView
{
    private readonly GraphicsView _gv;
    private readonly VoxelDrawable _drawable = new();
    private readonly Label _label;
    private IDispatcherTimer? _timer;
    private DateTime _start;

    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(
            nameof(IsRunning), typeof(bool), typeof(VoxelIndicator), false,
            propertyChanged: (b, _, n) => ((VoxelIndicator)b).OnRunningChanged((bool)n));

    public static readonly BindableProperty StatusTextProperty =
     BindableProperty.Create(
         nameof(StatusText), typeof(string), typeof(VoxelIndicator), null,
         propertyChanged: (b, _, n) => ((VoxelIndicator)b)._label.Text = (string?)n);

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public string? StatusText
    {
        get => (string?)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public VoxelIndicator()
    {
        _gv = new GraphicsView
        {
            Drawable = _drawable,
            WidthRequest = 280,
            HeightRequest = 175,
        };

        _label = new Label
        {
            TextColor = Color.FromArgb("#3E3E3E"),
            FontSize = 10,
            CharacterSpacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            TextTransform = TextTransform.Uppercase,
        };

        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Children = { _gv, _label },
        };
    }

    private void OnRunningChanged(bool running)
    {
        if (running) StartAnimation();
        else StopAnimation();
    }

    private void StartAnimation()
    {
        StopAnimation();
        _start = DateTime.UtcNow;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _drawable.ElapsedMs = (DateTime.UtcNow - _start).TotalMilliseconds;
        _gv.Invalidate();
    }

    private void StopAnimation()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null) StopAnimation();
    }
}