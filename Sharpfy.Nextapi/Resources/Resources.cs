using Sharpfy.Nextapi.Models;
using static Sharpfy.Nextapi.NextApiInput;

namespace Sharpfy.Nextapi.Resources;

public abstract class ResourceBase(NextApiClient client)
{
    protected Task<T> GetAsync<T>(string path, CancellationToken ct) => client.SendAsync<T>(HttpMethod.Get, path, null, ct);
    protected Task<T> PostAsync<T>(string path, object body, CancellationToken ct) => client.SendAsync<T>(HttpMethod.Post, path, body, ct);
}

/// <summary>Consultas por CNPJ, com ou sem pontuação, numérico ou alfanumérico.</summary>
public sealed class CnpjResource(NextApiClient client) : ResourceBase(client)
{
    /// <summary>Dados cadastrais do estabelecimento (1 crédito).</summary>
    /// <param name="toleranceDays">Idade máxima aceita do dado em cache, em dias (0 a 45). 0 força tempo real.</param>
    public Task<CnpjItemResponse> ConsultarAsync(string cnpj, int? toleranceDays = null, CancellationToken cancellationToken = default) =>
        GetAsync<CnpjItemResponse>($"/v1/cnpj/{NormalizeCnpj(cnpj)}" + Query(("toleranceDays", toleranceDays)), cancellationToken);

    /// <summary>Consulta em tempo real ao provedor (<c>toleranceDays = 0</c>); exige plano que permita.</summary>
    public Task<CnpjItemResponse> ConsultarAoVivoAsync(string cnpj, CancellationToken cancellationToken = default) =>
        ConsultarAsync(cnpj, 0, cancellationToken);

    /// <summary>Situação no Simples Nacional e no MEI.</summary>
    public Task<CnpjSimplesResponse> ObterSimplesAsync(string cnpj, int? toleranceDays = null, CancellationToken cancellationToken = default) =>
        GetAsync<CnpjSimplesResponse>($"/v1/cnpj/{NormalizeCnpj(cnpj)}/simples" + Query(("toleranceDays", toleranceDays)), cancellationToken);

    /// <summary>Inscrições estaduais do estabelecimento, uma por UF.</summary>
    public Task<IReadOnlyList<CnpjInscricaoEstadualResponse>> ObterInscricoesEstaduaisAsync(string cnpj, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<CnpjInscricaoEstadualResponse>>($"/v1/cnpj/{NormalizeCnpj(cnpj)}/ie", cancellationToken);

    /// <summary>Sanções (CEIS, CNEP, CEPIM) e dívida ativa da União. Planos pagos ou créditos avulsos.</summary>
    public Task<CnpjConformidadeResponse> ObterConformidadeAsync(string cnpj, CancellationToken cancellationToken = default) =>
        GetAsync<CnpjConformidadeResponse>($"/v1/cnpj/{NormalizeCnpj(cnpj)}/conformidade", cancellationToken);

    /// <summary>Sócios e empresas ligadas pelos mesmos sócios (grupo econômico), paginado.</summary>
    public Task<CnpjRelacionamentoResponse> ObterRelacionamentosAsync(string cnpj, int? page = null, int? pageSize = null,
        bool? incluirInativas = null, CancellationToken cancellationToken = default) =>
        GetAsync<CnpjRelacionamentoResponse>($"/v1/cnpj/{NormalizeCnpj(cnpj)}/relacionamentos"
            + Query(("page", page), ("pageSize", pageSize), ("incluirInativas", incluirInativas)), cancellationToken);

    /// <summary>Até 100 CNPJs numa chamada; 1 crédito por CNPJ encontrado. Nunca repetida automaticamente.</summary>
    public Task<IReadOnlyList<CnpjItemResponse>> ConsultarLoteAsync(IEnumerable<string> cnpjs, CancellationToken cancellationToken = default) =>
        PostAsync<IReadOnlyList<CnpjItemResponse>>("/v1/cnpj/batch", new { cnpjs = Batch(cnpjs) }, cancellationToken);

    /// <summary>Simples Nacional de até 100 CNPJs numa chamada. Nunca repetida automaticamente.</summary>
    public Task<IReadOnlyList<CnpjSimplesResponse>> ObterSimplesLoteAsync(IEnumerable<string> cnpjs, CancellationToken cancellationToken = default) =>
        PostAsync<IReadOnlyList<CnpjSimplesResponse>>("/v1/cnpj/batch/simples", new { cnpjs = Batch(cnpjs) }, cancellationToken);
}

public sealed class SociosResource(NextApiClient client) : ResourceBase(client)
{
    /// <summary>
    /// Empresas do sócio. O hash vem de <c>ObterRelacionamentosAsync</c> (<c>Socios[].HashSocio</c>); com
    /// <c>VinculoVerificavel</c> falso ele é provisório: não guarde.
    /// </summary>
    public Task<SocioEmpresasResponse> ListarEmpresasAsync(string hashSocio, int? page = null, int? pageSize = null,
        bool? incluirInativas = null, CancellationToken cancellationToken = default) =>
        GetAsync<SocioEmpresasResponse>($"/v1/socios/{HashSocio(hashSocio)}/empresas"
            + Query(("page", page), ("pageSize", pageSize), ("incluirInativas", incluirInativas)), cancellationToken);
}

/// <summary>Filtros de <see cref="CepsResource.BuscarAsync"/>. <c>Q</c> (texto livre) não combina com <c>Logradouro</c>.</summary>
public sealed record CepFiltro
{
    public string? Uf { get; init; }
    public int? IdMunicipio { get; init; }
    public string? NomeMunicipio { get; init; }
    public string? Logradouro { get; init; }
    public string? Q { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class CepsResource(NextApiClient client) : ResourceBase(client)
{
    /// <summary>Endereço e coordenadas de um CEP.</summary>
    public Task<CepItemResponse> ConsultarAsync(string cep, CancellationToken cancellationToken = default) =>
        GetAsync<CepItemResponse>($"/v1/ceps/{NormalizeCep(cep)}", cancellationToken);

    /// <summary>Busca por UF, município, logradouro ou texto livre.</summary>
    public Task<Page<CepItemResponse>> BuscarAsync(CepFiltro filtro, CancellationToken cancellationToken = default) =>
        GetAsync<Page<CepItemResponse>>("/v1/ceps" + Query(("uf", filtro.Uf), ("idMunicipio", filtro.IdMunicipio),
            ("nomeMunicipio", filtro.NomeMunicipio), ("logradouro", filtro.Logradouro), ("q", filtro.Q),
            ("page", filtro.Page), ("pageSize", filtro.PageSize)), cancellationToken);

    /// <summary>CEPs num raio (em metros) ao redor de uma coordenada.</summary>
    public Task<Page<CepItemResponse>> BuscarProximosAsync(double latitude, double longitude, double raioMetro, int? page = null,
        int? pageSize = null, CancellationToken cancellationToken = default)
    {
        RequireRange(latitude, -90, 90, "latitude");
        RequireRange(longitude, -180, 180, "longitude");
        RequirePositive(raioMetro, "raioMetro");
        return GetAsync<Page<CepItemResponse>>("/v1/ceps/geo" + Query(("latitude", latitude), ("longitude", longitude),
            ("raioMetro", raioMetro), ("page", page), ("pageSize", pageSize)), cancellationToken);
    }

    /// <summary>CEPs num raio (em metros) ao redor de outro CEP.</summary>
    public Task<Page<CepItemResponse>> BuscarProximosDoCepAsync(string cep, double raioMetro, int? page = null, int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var value = NormalizeCep(cep);
        RequirePositive(raioMetro, "raioMetro");
        return GetAsync<Page<CepItemResponse>>($"/v1/ceps/geo/{value}" + Query(("raioMetro", raioMetro), ("page", page),
            ("pageSize", pageSize)), cancellationToken);
    }
}

/// <summary>Filtros de <see cref="EmpresasResource.PesquisarAsync"/>. <c>Ceps</c> e <c>Cnaes</c> aceitam vários valores separados por vírgula.</summary>
public sealed record EmpresaFiltro
{
    public string? Q { get; init; }
    public string? Uf { get; init; }
    public string? CodigoMunicipio { get; init; }
    public string? CnpjRaiz { get; init; }
    public string? Cnpj { get; init; }
    public DateOnly? DataAberturaInicio { get; init; }
    public DateOnly? DataAberturaFim { get; init; }
    public string? CodigoSituacaoCadastral { get; init; }
    public string? Ceps { get; init; }
    public string? Cnaes { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class EmpresasResource(NextApiClient client) : ResourceBase(client)
{
    public Task<Page<CnpjSimplifiedResponse>> PesquisarAsync(EmpresaFiltro filtro, CancellationToken cancellationToken = default) =>
        GetAsync<Page<CnpjSimplifiedResponse>>("/v1/empresas" + Query(("q", filtro.Q), ("uf", filtro.Uf),
            ("codigoMunicipio", filtro.CodigoMunicipio), ("cnpjRaiz", filtro.CnpjRaiz), ("cnpj", filtro.Cnpj),
            ("dataAberturaInicio", filtro.DataAberturaInicio), ("dataAberturaFim", filtro.DataAberturaFim),
            ("codigoSituacaoCadastral", filtro.CodigoSituacaoCadastral), ("ceps", filtro.Ceps), ("cnaes", filtro.Cnaes),
            ("page", filtro.Page), ("pageSize", filtro.PageSize)), cancellationToken);
}

public sealed class CnaesResource(NextApiClient client) : ResourceBase(client)
{
    public Task<Page<CnaeItemResponse>> ListarAsync(int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
        GetAsync<Page<CnaeItemResponse>>("/v1/cnaes" + Query(("page", page), ("pageSize", pageSize)), cancellationToken);
}

public sealed class EstadosResource(NextApiClient client) : ResourceBase(client)
{
    public Task<Page<EstadoItemResponse>> ListarAsync(int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
        GetAsync<Page<EstadoItemResponse>>("/v1/estados" + Query(("page", page), ("pageSize", pageSize)), cancellationToken);
}

public sealed class MunicipiosResource(NextApiClient client) : ResourceBase(client)
{
    public Task<Page<MunicipioItemResponse>> ListarAsync(string? uf = null, int? page = null, int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        GetAsync<Page<MunicipioItemResponse>>("/v1/municipios" + Query(("uf", uf), ("page", page), ("pageSize", pageSize)), cancellationToken);

    /// <summary>Município pelo código da Receita (o <c>Id</c> de <c>ListarAsync</c>), com a hierarquia do IBGE.</summary>
    public Task<MunicipioDetalhadoResponse> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<MunicipioDetalhadoResponse>($"/v1/municipios/{PositiveInt(id, "id")}", cancellationToken);
}

/// <summary>Dados de mercado agregados. Planos pagos ou créditos avulsos.</summary>
public sealed class MercadoResource(NextApiClient client) : ResourceBase(client)
{
    /// <summary>Empresas ativas, aberturas e baixas de um CNAE (com ou sem pontuação), no Brasil ou numa UF.</summary>
    public Task<MercadoCnaeResponse> ObterCnaeAsync(string cnae, string? uf = null, int? meses = null, CancellationToken cancellationToken = default)
    {
        var codigo = PathSegment(System.Text.RegularExpressions.Regex.Replace(cnae ?? "", @"[\s./-]", ""), "cnae");
        return GetAsync<MercadoCnaeResponse>($"/v1/mercado/cnaes/{codigo}" + Query(("uf", uf), ("meses", meses)), cancellationToken);
    }

    /// <summary>Empresas e economia (PIB, população) de um município, pelo código da Receita.</summary>
    public Task<MercadoMunicipioResponse> ObterMunicipioAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<MercadoMunicipioResponse>($"/v1/mercado/municipios/{PositiveInt(id, "id")}", cancellationToken);

    /// <summary>Série de um indicador econômico (ex.: <c>ipca</c>), de <paramref name="inicio"/> a <paramref name="fim"/> (<c>AAAAMM</c>).</summary>
    public Task<MercadoIndicadorResponse> ObterIndicadorAsync(string tipo, string? inicio = null, string? fim = null,
        CancellationToken cancellationToken = default) =>
        GetAsync<MercadoIndicadorResponse>($"/v1/mercado/indicadores/{PathSegment(tipo, "tipo")}" + Query(("inicio", inicio), ("fim", fim)), cancellationToken);
}

public sealed class CreditosResource(NextApiClient client) : ResourceBase(client)
{
    /// <summary>Saldo de créditos do plano e avulsos, e os limites da chave. Não consome crédito.</summary>
    public Task<WorkspaceCreditsResponse> ConsultarAsync(CancellationToken cancellationToken = default) =>
        GetAsync<WorkspaceCreditsResponse>("/v1/credits", cancellationToken);
}
