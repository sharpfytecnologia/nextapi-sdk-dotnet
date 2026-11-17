using Microsoft.Extensions.DependencyInjection;

namespace Sharpfy.Nextapi.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="NextApiClient"/> com <c>IHttpClientFactory</c>. O handler primário não segue
    /// redirecionamento, para a chave não ir para outro host.
    /// </summary>
    public static IHttpClientBuilder AddNextApi(this IServiceCollection services, Action<NextApiOptions>? configure = null)
    {
        var options = new NextApiOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services.AddHttpClient(nameof(NextApiClient))
            .AddTypedClient((http, sp) => new NextApiClient(sp.GetRequiredService<NextApiOptions>(), http))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .ConfigureHttpClient(http => http.Timeout = System.Threading.Timeout.InfiniteTimeSpan);
    }
}
