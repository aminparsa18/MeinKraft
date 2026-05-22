using System;
using System.Threading;
using System.Threading.Tasks;

namespace MeinKraft.Worker;

public sealed class SaveGameTask : IScheduledTask
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILanguageService _languageService;
    private readonly IGameLogger _gameLogger;

    public SaveGameTask(ISaveGameService saveGameService, ILanguageService languageService, IGameLogger gameLogger)
    {
        _saveGameService = saveGameService;
        _languageService = languageService;
        _gameLogger = gameLogger;
    }

    public TimeSpan Interval => TimeSpan.FromMinutes(2);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Run(_saveGameService.SaveGlobalData, ct);
        _gameLogger.Server.Information(_languageService.ServerGameSaved());
    }
}