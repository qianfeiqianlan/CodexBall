using System.Threading;
using System.Windows;
using CodexBall.App.Services;
using CodexBall.App.ViewModels;

namespace CodexBall.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private StatusBallViewModel? _viewModel;

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
        _viewModel = new StatusBallViewModel();

        var window = new MainWindow(_viewModel, settingsService, settings);
        MainWindow = window;
        window.Show();

        await _viewModel.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
