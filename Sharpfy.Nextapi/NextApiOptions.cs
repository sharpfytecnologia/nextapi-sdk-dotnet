namespace Sharpfy.Nextapi;

/// <summary>Configuração do cliente. Todas as opções têm padrão; a chave pode vir de <c>NEXTAPI_KEY</c>.</summary>
public sealed class NextApiOptions
{
    /// <summary><c>nxa_live_…</c> (produção) ou <c>nxa_test_…</c> (sandbox). Padrão: variável <c>NEXTAPI_KEY</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Padrão <c>https://api.nextapi.com.br</c>; só https (http apenas para localhost).</summary>
    public Uri BaseUrl { get; set; } = new("https://api.nextapi.com.br");

    /// <summary>Tempo máximo de cada tentativa. Padrão 30 s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Novas tentativas em falha transitória (429, 502, 503, 504, rede), só em leituras. Padrão 2.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Complemento do User-Agent para identificar a sua aplicação (ex.: <c>minha-app/1.2</c>).</summary>
    public string? AppInfo { get; set; }

    // A chave nunca aparece ao imprimir as opções.
    public override string ToString() => $"NextApiOptions {{ BaseUrl = {BaseUrl}, Timeout = {Timeout}, MaxRetries = {MaxRetries} }}";
}
