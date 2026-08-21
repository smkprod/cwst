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
            try
            {
                response?.Dispose();
                response = await base.SendAsync(request, ct);
                if (!IsTransient(response.StatusCode) || attempt >= Delays.Length) return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Отмену со стороны вызывающего не глушим — это не сбой сети
                if (ct.IsCancellationRequested || attempt >= Delays.Length) throw;
            }

            await Task.Delay(Delays[attempt], ct);
        }
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code is HttpStatusCode.RequestTimeout
             or HttpStatusCode.TooManyRequests
             or >= HttpStatusCode.InternalServerError;
}
