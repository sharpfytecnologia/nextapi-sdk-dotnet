using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Sharpfy.Nextapi.Resources;

namespace Sharpfy.Nextapi;

/// <summary>
/// Cliente da NextAPI. Thread-safe: crie um e reutilize (ou registre com <c>services.AddNextApi()</c>).
/// </summary>
/// <example><code>
/// using var nextapi = new NextApiClient(); // lê NEXTAPI_KEY
/// var empresa = await nextapi.Cnpj.ConsultarAsync("11.222.333/0001-81");
/// </code></example>
public sealed class NextApiClient : IDisposable
{
    public static readonly string Version =
        typeof(NextApiClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    private static readonly HashSet<int> Retryable = [429, 502, 503, 504];
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly string _userAgent;
    private readonly string _clientInfo;

    /// <param name="options">Configuração; nulo usa os padrões e <c>NEXTAPI_KEY</c>.</param>
    /// <param name="httpClient">
    /// <see cref="HttpClient"/> próprio (ex.: o do <c>IHttpClientFactory</c>). Sem ele, o cliente cria um que não segue
    /// redirecionamento.
    /// </param>
    public NextApiClient(NextApiOptions? options = null, HttpClient? httpClient = null)
    {
        options ??= new NextApiOptions();
        var key = (options.ApiKey ?? System.Environment.GetEnvironmentVariable("NEXTAPI_KEY") ?? "").Trim();
        if (key.Length == 0)
            throw new NextApiAuthenticationException("Chave de API não informada: defina NextApiOptions.ApiKey ou NEXTAPI_KEY.");
        _apiKey = key;
        Environment = key.StartsWith("nxa_test_", StringComparison.Ordinal) ? NextApiEnvironment.Sandbox : NextApiEnvironment.Production;
        _baseUrl = CheckBaseUrl(options.BaseUrl);
        _timeout = options.Timeout;
        _maxRetries = Math.Max(0, options.MaxRetries);
        var app = SanitizeAppInfo(options.AppInfo);
        var runtime = $"dotnet/{System.Environment.Version}";
        var ua = $"nextapi-dotnet/{Version} {runtime}";
        _userAgent = app.Length == 0 ? ua : $"{ua} {app}";
        _clientInfo = $"{runtime}; {OsName()}" + (app.Length == 0 ? "" : $"; {app}");

        if (httpClient is null)
        {
            // Não segue redirecionamento: o cabeçalho com a chave iria junto.
            _http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
            _ownsHttp = true;
        }
        else
        {
            _http = httpClient;
        }

        Cnpj = new CnpjResource(this);
        Socios = new SociosResource(this);
        Ceps = new CepsResource(this);
        Empresas = new EmpresasResource(this);
        Cnaes = new CnaesResource(this);
        Estados = new EstadosResource(this);
        Municipios = new MunicipiosResource(this);
        Mercado = new MercadoResource(this);
        Creditos = new CreditosResource(this);
    }

    public CnpjResource Cnpj { get; }
    public SociosResource Socios { get; }
    public CepsResource Ceps { get; }
    public EmpresasResource Empresas { get; }
    public CnaesResource Cnaes { get; }
    public EstadosResource Estados { get; }
    public MunicipiosResource Municipios { get; }
    public MercadoResource Mercado { get; }
    public CreditosResource Creditos { get; }

    /// <summary>Sandbox para chaves <c>nxa_test_</c>. O endereço é o mesmo: a API decide pela chave.</summary>
    public NextApiEnvironment Environment { get; }

    public override string ToString() => $"NextApiClient {{ BaseUrl = {_baseUrl}, Environment = {Environment} }}";

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string CheckBaseUrl(Uri url)
    {
        if (!url.IsAbsoluteUri) throw new ArgumentException($"BaseUrl inválida: {url}");
        var local = url.IsLoopback;
        if (url.Scheme != Uri.UriSchemeHttps && !(url.Scheme == Uri.UriSchemeHttp && local))
            throw new ArgumentException("BaseUrl tem de usar https (http só para localhost): a chave vai no cabeçalho.");
        return url.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        // POST de lote consome créditos: nunca repete sozinho.
        var retries = method == HttpMethod.Get ? _maxRetries : 0;
        var payload = body is null ? null : JsonSerializer.Serialize(body, Json);

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, _baseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            request.Headers.TryAddWithoutValidation("X-NextApi-Client", $"dotnet/{Version}");
            request.Headers.TryAddWithoutValidation("X-NextApi-Client-Info", _clientInfo);
            if (payload is not null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeout);
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt < retries)
                {
                    await Task.Delay(Backoff(attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                var message = ex is OperationCanceledException
                    ? $"Sem resposta da NextAPI em {_timeout.TotalMilliseconds:0} ms."
                    : $"Falha de conexão com a NextAPI: {ex.Message}";
                throw new NextApiConnectionException(message, inner: ex);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NoContent) return default!;
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return (await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken).ConfigureAwait(false))!;
                }
                if (status is >= 300 and < 400)
                    throw new NextApiConnectionException(
                        $"A NextAPI respondeu com redirecionamento ({status}); o SDK não o segue para não reenviar a chave.", status);

                var wait = RetryAfter(response);
                if (Retryable.Contains(status) && attempt < retries)
                {
                    await Task.Delay(wait ?? Backoff(attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                throw await ToException(response, wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<NextApiException> ToException(HttpResponseMessage response, TimeSpan? wait, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        int? code = null;
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("code", out var c) && c.TryGetInt32(out var n)) code = n;
                if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) message = m.GetString();
            }
        }
        catch (JsonException)
        {
            // Corpo que não é JSON fica só em Body.
        }
        message = string.IsNullOrEmpty(message) ? $"HTTP {status}" : message;
        var requestId = response.Headers.TryGetValues("x-request-id", out var ids) ? ids.FirstOrDefault() : null;

        return code switch
        {
            NextApiErrorCodes.TokenNotProvided or NextApiErrorCodes.TokenInvalid or NextApiErrorCodes.SandboxDisabled
                => new NextApiAuthenticationException(message, status, code, text, requestId),
            NextApiErrorCodes.MissingCredits or NextApiErrorCodes.LiveSearchMissingCredits
                => new NextApiInsufficientCreditsException(message, status, code, text, requestId),
            NextApiErrorCodes.RequiresPaidPlan or NextApiErrorCodes.LiveSearchNotAllowed
                => new NextApiPaidPlanRequiredException(message, status, code, text, requestId),
            _ => status switch
            {
                401 or 403 => new NextApiAuthenticationException(message, status, code, text, requestId),
                404 => new NextApiNotFoundException(message, status, code, text, requestId),
                429 => new NextApiRateLimitException(message, status, code, text, requestId, wait),
                >= 500 => new NextApiServerException(message, status, code, text, requestId),
                400 or 422 => new NextApiValidationException(message, status, code, text, requestId),
                _ => new NextApiException(message, status, code, text, requestId),
            },
        };
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        TimeSpan? wait = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (wait is null) return null;
        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : wait;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var baseMs = Math.Min(8000, 500 * Math.Pow(2, attempt - 1));
        return TimeSpan.FromMilliseconds(baseMs / 2 + Random.Shared.NextDouble() * baseMs / 2);
    }
    /// <summary>Só caracteres seguros num cabeçalho: CR/LF ou espaço no AppInfo quebrariam a requisição.</summary>
    internal static string SanitizeAppInfo(string? info)
    {
        var sb = new StringBuilder();
        foreach (var ch in info ?? "")
            if (sb.Length < 64 && (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' || "._/@+-".IndexOf(ch) >= 0))
                sb.Append(ch);
        return sb.ToString();
    }

    private static string OsName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
        : "other";
}

public enum NextApiEnvironment
{
    Production,
    Sandbox,
}
