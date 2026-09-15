using System.Threading;
using System.Windows;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;
using Application = System.Windows.Application;

namespace CodexBall.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private StatusBallViewModel? _viewModel;
    private TrayIconService? _trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Global\\CodexBall", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;
        var settingsService = new SettingsService();
        var settings = await settingsService.LoadAsync();
        var processMonitor = new CodexProcessMonitor();
        _viewModel = new StatusBallViewModel(processMonitor);

        var window = new MainWindow(_viewModel, settingsService, settings);
        MainWindow = window;
        _trayIconService = new TrayIconService(_viewModel, window);
        _viewModel.CodexActivityChanged += (_, isActive) =>
        {
            if (isActive)
            {
                window.ShowForCodex();
            }
            else
            {
                window.HideForCodex();
            }
        };

        await _viewModel.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }

        _trayIconService?.Dispose();

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
