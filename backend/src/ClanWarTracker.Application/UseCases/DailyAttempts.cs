using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Счётчик попыток суточной задачи в базе («день|n»). Задача, которая падает или не
/// доходит до записи итога, иначе повторяется на каждом такте и после каждого рестарта
/// воркера - сутками: сбор топа так держал процессор и сеть под потолком, а разбор меты
/// так платил бы нейросети каждые пять минут.
/// </summary>
public static class DailyAttempts
{
    /// <summary>Отметить новую попытку. false - лимит на этот день уже исчерпан.</summary>
    public static async Task<bool> TryStartAsync(
        IServiceSettingRepository settings, string key, string day, int max, CancellationToken ct)
    {
        var raw = await settings.GetAsync(key, ct);
        var parts = raw?.Split('|');
        var tries = parts is [var d, var n] && d == day && int.TryParse(n, out var v) ? v : 0;
        if (tries >= max) return false;
        await settings.SetAsync(key, $"{day}|{tries + 1}", ct);
        return true;
    }
}
