namespace Sharpfy.Nextapi;

/// <summary>Códigos de erro devolvidos pela NextAPI no corpo <c>{ status, code, message }</c>.</summary>
public static class NextApiErrorCodes
{
    public const int NotFound = 1000;
    public const int TokenNotProvided = 1001;
    public const int TokenInvalid = 1002;
    public const int TooManyRequests = 1003;
    public const int MissingCredits = 1004;
    public const int LiveSearchNotAllowed = 1005;
    public const int LiveSearchMissingCredits = 1006;
    public const int InvalidCep = 1007;
    public const int RequiresPaidPlan = 1012;
    public const int ExternalProviderFailure = 1014;
    public const int SandboxDailyLimit = 1015;
    public const int SandboxDisabled = 1016;
}

/// <summary>
/// Falha numa chamada à NextAPI. A subclasse diz o que fazer; <see cref="Code"/> distingue casos com o mesmo
/// status HTTP (405 tanto para "sem créditos" quanto para "plano pago obrigatório").
/// </summary>
public class NextApiException : Exception
{
    public NextApiException(string message, int status = 0, int? code = null, string? body = null,
        string? requestId = null, TimeSpan? retryAfter = null, Exception? inner = null) : base(message, inner)
    {
        Status = status;
        Code = code;
        Body = body;
        RequestId = requestId;
        RetryAfter = retryAfter;
    }

    /// <summary>Status HTTP (0 quando a requisição nem chegou à API).</summary>
    public int Status { get; }

    /// <summary>Código de erro da NextAPI (<see cref="NextApiErrorCodes"/>), quando informado.</summary>
    public int? Code { get; }

    /// <summary>Corpo bruto da resposta de erro.</summary>
    public string? Body { get; }

    /// <summary>Cabeçalho <c>x-request-id</c>, para o suporte.</summary>
    public string? RequestId { get; }

    /// <summary>Espera indicada pelo cabeçalho <c>Retry-After</c>.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>Chave ausente, inválida ou revogada.</summary>
public sealed class NextApiAuthenticationException(string message, int status = 0, int? code = null, string? body = null, string? requestId = null)
    : NextApiException(message, status, code, body, requestId);

/// <summary>Sem créditos para a consulta (1004, 1006).</summary>
public sealed class NextApiInsufficientCreditsException(string message, int status, int? code, string? body, string? requestId)
    : NextApiException(message, status, code, body, requestId);

/// <summary>O recurso exige plano pago ou créditos avulsos (1012, 1005).</summary>
public sealed class NextApiPaidPlanRequiredException(string message, int status, int? code, string? body, string? requestId)
    : NextApiException(message, status, code, body, requestId);

/// <summary>O CNPJ, CEP ou sócio pesquisado não existe (404).</summary>
public sealed class NextApiNotFoundException(string message, int status, int? code, string? body, string? requestId)
    : NextApiException(message, status, code, body, requestId);

/// <summary>Parâmetro rejeitado pela API (400) ou pelo próprio SDK antes de enviar (status 0).</summary>
public sealed class NextApiValidationException(string message, int status = 0, int? code = null, string? body = null, string? requestId = null)
    : NextApiException(message, status, code, body, requestId);

/// <summary>Limite de requisições do plano ou do sandbox (429).</summary>
public sealed class NextApiRateLimitException(string message, int status, int? code, string? body, string? requestId, TimeSpan? retryAfter)
    : NextApiException(message, status, code, body, requestId, retryAfter);

/// <summary>Falha do lado da API (5xx), inclusive a do provedor em tempo real (1014).</summary>
public sealed class NextApiServerException(string message, int status, int? code, string? body, string? requestId)
    : NextApiException(message, status, code, body, requestId);

/// <summary>Não houve resposta: rede, DNS, TLS, tempo esgotado ou redirecionamento recusado.</summary>
public sealed class NextApiConnectionException(string message, int status = 0, Exception? inner = null)
    : NextApiException(message, status, inner: inner);
