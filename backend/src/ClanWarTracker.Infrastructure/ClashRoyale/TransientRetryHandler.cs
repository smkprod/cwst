using System.Net;

namespace ClanWarTracker.Infrastructure.ClashRoyale;

/// <summary>
/// Повтор разовых сбоев CR API. Публичное API Supercell периодически отвечает 5xx
/// или обрывает соединение без всякой причины — один такой ответ не должен доходить
/// до пользователя как «ошибка», потому что следующая попытка почти всегда проходит.
///
/// Повторяем только безопасное: сетевые сбои, таймауты, 5xx и 429. Ответы 4xx
/// (кроме 429) — это осмысленный отказ (нет такого тега, протух токен), их повтор
/// ничего не изменит, только задержит ответ.
/// </summary>
public class TransientRetryHandler : DelegatingHandler
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(600),
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; ; attempt++)
        {
            var delay = Delays[Math.Min(attempt, Delays.Length - 1)];
            try
            {
                response?.Dispose();
                response = await base.SendAsync(request, ct);
                if (!IsTransient(response.StatusCode) || attempt >= Delays.Length) return response;
                // 429 - ключ упёрся в лимит. Повтор через 200 мс только добавлял запросов
                // ровно тогда, когда их и так слишком много: ждём, сколько просит API.
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    delay = Clamp(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt + 1));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Отмену со стороны вызывающего не глушим — это не сбой сети
                if (ct.IsCancellationRequested || attempt >= Delays.Length) throw;
            }

            await Task.Delay(delay, ct);
        }
    }

    private static TimeSpan Clamp(TimeSpan t) =>
        t < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : t > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : t;

    private static bool IsTransient(HttpStatusCode code) =>
        code is HttpStatusCode.RequestTimeout
             or HttpStatusCode.TooManyRequests
             or >= HttpStatusCode.InternalServerError;
}
