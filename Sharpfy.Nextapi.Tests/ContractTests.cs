using System.Net;
using System.Text.Json;
using Sharpfy.Nextapi.Resources;

namespace Sharpfy.Nextapi.Tests;

/// <summary>Contrato compartilhado pelos seis SDKs (sdks/contract/cases.json).</summary>
public class ContractTests
{
    private const string Key = "nxa_test_0123456789abcdef";
    private static readonly JsonElement Contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cases.json"))).RootElement;

    private sealed class Fake(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Seen { get; } = [];
        public Func<HttpResponseMessage> Respond { get; set; } = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return Respond();
        }
    }

    private static HttpResponseMessage Json(string body, int status = 200, Dictionary<string, string>? headers = null)
    {
        var r = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        foreach (var (k, v) in headers ?? []) r.Headers.TryAddWithoutValidation(k, v);
        return r;
    }

    private static NextApiClient Client(HttpMessageHandler handler, int maxRetries = 2, TimeSpan? timeout = null) =>
        new(new NextApiOptions { ApiKey = Key, MaxRetries = maxRetries, Timeout = timeout ?? TimeSpan.FromSeconds(30) },
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });

    private static string? S(JsonElement o, string n) => o.TryGetProperty(n, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;
    private static int? I(JsonElement o, string n) => o.TryGetProperty(n, out var v) ? v.GetInt32() : null;
    private static double D(JsonElement o, string n) => o.GetProperty(n).GetDouble();
    private static bool? B(JsonElement o, string n) => o.TryGetProperty(n, out var v) ? v.GetBoolean() : null;
    private static DateOnly? Date(JsonElement o, string n) => S(o, n) is { } s ? DateOnly.Parse(s) : null;

    /// <summary>Traduz a chamada canônica (camelCase, argumentos em JSON) para a API do SDK .NET.</summary>
    private static Task Invoke(NextApiClient api, string call, JsonElement args)
    {
        var a = args.EnumerateArray().ToArray();
        JsonElement o = a.Length > 0 && a[^1].ValueKind == JsonValueKind.Object ? a[^1] : JsonDocument.Parse("{}").RootElement;
        string Str(int i) => a[i].GetString()!;
        IEnumerable<string> List(int i) => a[i].EnumerateArray().Select(x => x.GetString()!);
        return call switch
        {
            "cnpj.consultar" => api.Cnpj.ConsultarAsync(Str(0), I(o, "toleranceDays")),
            "cnpj.consultarAoVivo" => api.Cnpj.ConsultarAoVivoAsync(Str(0)),
            "cnpj.obterSimples" => api.Cnpj.ObterSimplesAsync(Str(0), I(o, "toleranceDays")),
            "cnpj.obterInscricoesEstaduais" => api.Cnpj.ObterInscricoesEstaduaisAsync(Str(0)),
            "cnpj.obterConformidade" => api.Cnpj.ObterConformidadeAsync(Str(0)),
            "cnpj.obterRelacionamentos" => api.Cnpj.ObterRelacionamentosAsync(Str(0), I(o, "page"), I(o, "pageSize"), B(o, "incluirInativas")),
            "cnpj.consultarLote" => api.Cnpj.ConsultarLoteAsync(List(0)),
            "cnpj.obterSimplesLote" => api.Cnpj.ObterSimplesLoteAsync(List(0)),
            "socios.listarEmpresas" => api.Socios.ListarEmpresasAsync(Str(0), I(o, "page"), I(o, "pageSize"), B(o, "incluirInativas")),
            "ceps.consultar" => api.Ceps.ConsultarAsync(Str(0)),
            "ceps.buscar" => api.Ceps.BuscarAsync(new CepFiltro
            {
                Uf = S(o, "uf"), IdMunicipio = I(o, "idMunicipio"), NomeMunicipio = S(o, "nomeMunicipio"),
                Logradouro = S(o, "logradouro"), Q = S(o, "q"), Page = I(o, "page"), PageSize = I(o, "pageSize"),
            }),
            "ceps.buscarProximos" => api.Ceps.BuscarProximosAsync(D(o, "latitude"), D(o, "longitude"), D(o, "raioMetro"), I(o, "page"), I(o, "pageSize")),
            "ceps.buscarProximosDoCep" => api.Ceps.BuscarProximosDoCepAsync(Str(0), D(o, "raioMetro"), I(o, "page"), I(o, "pageSize")),
            "empresas.pesquisar" => api.Empresas.PesquisarAsync(new EmpresaFiltro
            {
                Q = S(o, "q"), Uf = S(o, "uf"), CodigoMunicipio = S(o, "codigoMunicipio"), CnpjRaiz = S(o, "cnpjRaiz"),
                Cnpj = S(o, "cnpj"), DataAberturaInicio = Date(o, "dataAberturaInicio"), DataAberturaFim = Date(o, "dataAberturaFim"),
                CodigoSituacaoCadastral = S(o, "codigoSituacaoCadastral"), Ceps = S(o, "ceps"), Cnaes = S(o, "cnaes"),
                Page = I(o, "page"), PageSize = I(o, "pageSize"),
            }),
            "cnaes.listar" => api.Cnaes.ListarAsync(I(o, "page"), I(o, "pageSize")),
            "estados.listar" => api.Estados.ListarAsync(I(o, "page"), I(o, "pageSize")),
            "municipios.listar" => api.Municipios.ListarAsync(S(o, "uf"), I(o, "page"), I(o, "pageSize")),
            "municipios.obterPorId" => api.Municipios.ObterPorIdAsync(a[0].GetInt32()),
            "mercado.obterCnae" => api.Mercado.ObterCnaeAsync(Str(0), S(o, "uf"), I(o, "meses")),
            "mercado.obterMunicipio" => api.Mercado.ObterMunicipioAsync(a[0].GetInt32()),
            "mercado.obterIndicador" => api.Mercado.ObterIndicadorAsync(Str(0), S(o, "inicio"), S(o, "fim")),
            "creditos.consultar" => api.Creditos.ConsultarAsync(),
            _ => throw new InvalidOperationException($"SDK sem {call}"),
        };
    }

    public static IEnumerable<object[]> Cases() => Contract.GetProperty("cases").EnumerateArray().Select(c => new object[] { S(c, "id")!, c.GetRawText() });
    public static IEnumerable<object[]> Invalid() => Contract.GetProperty("invalid").EnumerateArray().Select(c => new object[] { S(c, "reason")!, c.GetRawText() });
    public static IEnumerable<object[]> Errors() => Contract.GetProperty("errors").EnumerateArray().Select(c => new object[] { $"{I(c, "status")}/{I(c.GetProperty("body"), "code")}", c.GetRawText() });

    [Theory, MemberData(nameof(Cases))]
    public async Task Caso(string id, string raw)
    {
        var c = JsonDocument.Parse(raw).RootElement;
        var fake = new Fake(() => Json(c.GetProperty("response").GetRawText()));
        await Invoke(Client(fake), S(c, "call") ?? id, c.GetProperty("args"));

        var (request, body) = Assert.Single(fake.Seen);
        Assert.Equal(S(c, "method"), request.Method.Method);
        Assert.Equal("https://api.nextapi.com.br", request.RequestUri!.GetLeftPart(UriPartial.Authority));
        Assert.Equal(S(c, "path"), request.RequestUri.AbsolutePath);
        var expected = c.GetProperty("query").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        var actual = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.Equal(expected.OrderBy(x => x.Key), actual.AllKeys.Select(k => new KeyValuePair<string, string?>(k!, actual[k])).OrderBy(x => x.Key));
        Assert.Equal($"Bearer {Key}", request.Headers.Authorization!.ToString());
        Assert.Matches(@"^nextapi-dotnet/\d+\.\d+\.\d+ dotnet/", string.Join(" ", request.Headers.UserAgent));
        Assert.Matches(S(Contract.GetProperty("headers"), "client")!, Assert.Single(request.Headers.GetValues("X-NextApi-Client")));
        Assert.Matches(S(Contract.GetProperty("headers"), "info")!, Assert.Single(request.Headers.GetValues("X-NextApi-Client-Info")));
        if (c.TryGetProperty("body", out var expectedBody))
        {
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(JsonSerializer.Serialize(expectedBody), JsonSerializer.Serialize(JsonDocument.Parse(body!).RootElement));
        }
        else Assert.Null(body);
    }

    [Fact]
    public async Task AppInfoEFiltradoAntesDosCabecalhos()
    {
        var spec = Contract.GetProperty("headers").GetProperty("appInfo");
        var fake = new Fake(() => Json("{}"));
        var api = new NextApiClient(new NextApiOptions { ApiKey = Key, AppInfo = S(spec, "input") }, new HttpClient(fake));
        await api.Creditos.ConsultarAsync();
        var (request, _) = Assert.Single(fake.Seen);
        var sanitized = S(spec, "sanitized")!;
        Assert.EndsWith(" " + sanitized, string.Join(" ", request.Headers.GetValues("User-Agent")));
        var info = Assert.Single(request.Headers.GetValues("X-NextApi-Client-Info"));
        Assert.EndsWith("; " + sanitized, info);
        Assert.Matches(S(Contract.GetProperty("headers"), "info")!, info);
    }

    [Theory, MemberData(nameof(Invalid))]
    public async Task EntradaInvalidaNaoChegaAApi(string reason, string raw)
    {
        _ = reason;
        var c = JsonDocument.Parse(raw).RootElement;
        var fake = new Fake(() => Json("{}"));
        var error = await Assert.ThrowsAsync<NextApiValidationException>(() => Invoke(Client(fake), S(c, "call")!, c.GetProperty("args")));
        Assert.Equal(0, error.Status);
        Assert.Empty(fake.Seen);
    }

    [Fact]
    public async Task EntradasAceitas()
    {
        foreach (var c in Contract.GetProperty("valid").EnumerateArray())
        {
            var fake = new Fake(() => Json("{}"));
            await Invoke(Client(fake), S(c, "call")!, c.GetProperty("args"));
            Assert.Equal(S(c, "path"), fake.Seen[0].Request.RequestUri!.AbsolutePath);
        }
    }

    private static readonly Dictionary<string, Type> Classes = new()
    {
        ["authentication"] = typeof(NextApiAuthenticationException),
        ["insufficientCredits"] = typeof(NextApiInsufficientCreditsException),
        ["paidPlanRequired"] = typeof(NextApiPaidPlanRequiredException),
        ["notFound"] = typeof(NextApiNotFoundException),
        ["validation"] = typeof(NextApiValidationException),
        ["rateLimit"] = typeof(NextApiRateLimitException),
        ["server"] = typeof(NextApiServerException),
    };

    [Theory, MemberData(nameof(Errors))]
    public async Task Erro(string name, string raw)
    {
        _ = name;
        var c = JsonDocument.Parse(raw).RootElement;
        var body = c.GetProperty("body");
        var fake = new Fake(() => Json(body.GetRawText(), I(c, "status")!.Value, new() { ["x-request-id"] = "req-1", ["Retry-After"] = "0" }));
        var error = await Assert.ThrowsAnyAsync<NextApiException>(() => Client(fake).Cnpj.ConsultarAsync("11222333000181"));
        Assert.IsType(Classes[S(c, "error")!], error);
        Assert.Equal(I(c, "status"), error.Status);
        Assert.Equal(I(body, "code"), error.Code);
        Assert.Equal(S(body, "message"), error.Message);
        Assert.Equal("req-1", error.RequestId);
        Assert.Equal(B(c, "retryable") == true ? 3 : 1, fake.Seen.Count);
    }

    [Fact]
    public async Task RetryDe503DevolveO200()
    {
        var n = 0;
        var fake = new Fake(() => n++ == 0 ? Json("{}", 503, new() { ["Retry-After"] = "0" }) : Json("""{"cnpj":"1"}"""));
        var result = await Client(fake).Cnpj.ConsultarAsync("11222333000181");
        Assert.Equal("1", result.Cnpj);
        Assert.Equal(2, fake.Seen.Count);
    }

    [Fact]
    public async Task LoteNuncaERepetido()
    {
        var fake = new Fake(() => Json("""{"code":1014,"message":"x"}""", 503, new() { ["Retry-After"] = "0" }));
        await Assert.ThrowsAsync<NextApiServerException>(() => Client(fake).Cnpj.ConsultarLoteAsync(["11222333000181"]));
        Assert.Single(fake.Seen);
    }

    private sealed class Failing : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            throw new HttpRequestException("connection refused");
        }
    }

    [Fact]
    public async Task FalhaDeRedeERepetidaEViraConnectionSemAChave()
    {
        var handler = new Failing();
        var error = await Assert.ThrowsAsync<NextApiConnectionException>(() => Client(handler, maxRetries: 1).Creditos.ConsultarAsync());
        Assert.Equal(2, handler.Calls);
        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public async Task RedirecionamentoViraErro()
    {
        var fake = new Fake(() =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.Found);
            r.Headers.Location = new Uri("https://evil.example/");
            return r;
        });
        await Assert.ThrowsAsync<NextApiConnectionException>(() => Client(fake).Creditos.ConsultarAsync());
        Assert.Single(fake.Seen);
    }

    private sealed class Hanging : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException();
        }
    }

    [Fact]
    public async Task TempoEsgotadoViraConnection()
    {
        var error = await Assert.ThrowsAsync<NextApiConnectionException>(
            () => Client(new Hanging(), maxRetries: 0, timeout: TimeSpan.FromMilliseconds(20)).Creditos.ConsultarAsync());
        Assert.Contains("20 ms", error.Message);
    }

    [Fact]
    public async Task CancelamentoDoChamadorNaoRepete()
    {
        var handler = new Hanging();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).Creditos.ConsultarAsync(cts.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void AChaveNaoAparece()
    {
        var api = Client(new Fake(() => Json("{}")));
        Assert.DoesNotContain(Key, api.ToString());
        Assert.Equal(NextApiEnvironment.Sandbox, api.Environment);
        Assert.DoesNotContain(Key, new NextApiOptions { ApiKey = Key }.ToString());
    }

    [Fact]
    public void BaseUrlExigeHttps()
    {
        Assert.Throws<ArgumentException>(() => new NextApiClient(new NextApiOptions { ApiKey = Key, BaseUrl = new Uri("http://api.nextapi.com.br") }));
        using var _ = new NextApiClient(new NextApiOptions { ApiKey = Key, BaseUrl = new Uri("http://localhost:5112") });
    }

    [Fact]
    public void SemChaveErroClaro()
    {
        var saved = Environment.GetEnvironmentVariable("NEXTAPI_KEY");
        Environment.SetEnvironmentVariable("NEXTAPI_KEY", null);
        try
        {
            Assert.Throws<NextApiAuthenticationException>(() => new NextApiClient());
            Environment.SetEnvironmentVariable("NEXTAPI_KEY", "nxa_live_abc");
            using var api = new NextApiClient();
            Assert.Equal(NextApiEnvironment.Production, api.Environment);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEXTAPI_KEY", saved);
        }
    }

    [Fact]
    public async Task CorpoDeErroQueNaoEJson()
    {
        var fake = new Fake(() => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("<html>Bad Gateway</html>") });
        var error = await Assert.ThrowsAsync<NextApiServerException>(() => Client(fake, maxRetries: 0).Creditos.ConsultarAsync());
        Assert.Equal("HTTP 500", error.Message);
        Assert.Equal("<html>Bad Gateway</html>", error.Body);
    }

    [Fact]
    public void AddNextApiResolveOCliente()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Sharpfy.Nextapi.Extensions.ServiceCollectionExtensions.AddNextApi(services, o => o.ApiKey = Key);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var api = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<NextApiClient>(provider);
        Assert.Equal(NextApiEnvironment.Sandbox, api.Environment);
    }

    [Fact]
    public void CnpjAlfanumerico() => Assert.Equal("12ABC34501DE35", NextApiInput.NormalizeCnpj("12.abc.345/01de-35"));

    // ---- Fumaça contra a API publicada (sdks/smoke.sh): só roda com NEXTAPI_SMOKE_KEY. ----
    private static readonly string? SmokeKey = Environment.GetEnvironmentVariable("NEXTAPI_SMOKE_KEY");
    private static readonly string SmokeEnv = Environment.GetEnvironmentVariable("NEXTAPI_SMOKE_ENV") ?? "production";

    [Fact]
    public async Task Smoke()
    {
        if (string.IsNullOrEmpty(SmokeKey)) return; // sem chave, nada a fazer
        var smoke = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "smoke.json"))).RootElement;
        var failures = new List<string>();
        foreach (var step in smoke.GetProperty("steps").EnumerateArray())
        {
            if (S(step, "env") is { } env && env != SmokeEnv) continue;
            var key = B(step, "badKey") == true ? SmokeKey[..9] + "chave-invalida" : SmokeKey;
            using var api = new NextApiClient(new NextApiOptions { ApiKey = key, MaxRetries = 4 });
            string got, detail = "";
            try
            {
                await Invoke(api, S(step, "call")!, step.GetProperty("args"));
                got = "ok";
            }
            catch (NextApiConnectionException e) { got = "connection"; detail = $" ({e.Message})"; }
            catch (NextApiException e)
            {
                got = Classes.FirstOrDefault(c => c.Value == e.GetType()).Key ?? "other";
                detail = $" ({e.Status}/{e.Code} {e.Message} req={e.RequestId})";
            }
            Console.WriteLine($"SMOKE dotnet {SmokeEnv} {S(step, "id")}: {got}{detail}");
            if (!step.GetProperty("expect").EnumerateArray().Any(x => x.GetString() == got)) failures.Add($"{S(step, "id")}: {got}");
        }
        Assert.Empty(failures);
    }
}
