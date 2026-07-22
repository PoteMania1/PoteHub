using System.Text.Json;
using PoteHub.Api.Models;

namespace PoteHub.Api.Clients;

public class NinjaSagaApiClient : IDisposable
{
    private const string ClanRankingUrl =
        "https://static.ninjasaga.cc/" +
        "data/clan_rankings.json";

    private readonly HttpClient _httpClient;
    private readonly int _maxAttempts;

    public NinjaSagaApiClient(
        TimeSpan timeout,
        int maxAttempts)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts));
        }

        _maxAttempts = maxAttempts;

        _httpClient = new HttpClient
        {
            Timeout = timeout
        };
    }

    public async Task<ApiResponse>
        GetClanRankingsAsync(
            CancellationToken cancellationToken =
                default)
    {
        Exception? lastException = null;

        for (int attempt = 1;
             attempt <= _maxAttempts;
             attempt++)
        {
            try
            {
                return await DownloadAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (IsTransient(exception))
            {
                lastException = exception;

                if (attempt >= _maxAttempts)
                {
                    break;
                }

                TimeSpan delay =
                    TimeSpan.FromSeconds(
                        Math.Pow(2, attempt));

                await Task.Delay(
                    delay,
                    cancellationToken);
            }
        }

        throw new HttpRequestException(
            $"No se pudo descargar la API " +
            $"después de {_maxAttempts} intentos.",
            lastException);
    }

    private async Task<ApiResponse> DownloadAsync(
        CancellationToken cancellationToken)
    {
        long cacheBuster =
        DateTimeOffset.UtcNow
            .ToUnixTimeMilliseconds();

            string requestUrl =
                $"{ClanRankingUrl}?t={cacheBuster}";

            using HttpRequestMessage request =
                new(
                    HttpMethod.Get,
                    requestUrl);

            request.Headers.TryAddWithoutValidation(
                "Cache-Control",
                "no-cache, no-store");

            request.Headers.TryAddWithoutValidation(
                "Pragma",
                "no-cache");

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                HttpCompletionOption
                    .ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        await using Stream contentStream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken);

        ApiResponse? result =
            await JsonSerializer
                .DeserializeAsync<ApiResponse>(
                    contentStream,
                    cancellationToken:
                        cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "La API devolvió una respuesta vacía.");
        }

        return result;
    }

    private static bool IsTransient(
        Exception exception)
    {
        return exception is HttpRequestException
            or TaskCanceledException
            or JsonException;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}