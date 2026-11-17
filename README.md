# NextAPI para .NET (`Sharpfy.Nextapi`)

SDK oficial da [NextAPI](https://nextapi.com.br) para .NET 8 e .NET 10: CNPJ (inclusive alfanumérico), Simples Nacional, inscrições estaduais, conformidade, sócios, CEP com busca geográfica, pesquisa de empresas, localidades, mercado e créditos. Usa só a BCL (`HttpClient`, `System.Text.Json`); a única dependência é `Microsoft.Extensions.Http`, para o `AddNextApi()`.

## Requisitos

- .NET 8 ou 10 (alvos `net8.0` e `net10.0`; o .NET 11 também é testado)
- Uma chave de API (veja abaixo)

## Instalação

```bash
dotnet add package Sharpfy.Nextapi
```

## Chave de API

Crie a sua em [app.nextapi.com.br](https://app.nextapi.com.br). São dois tipos:

| Prefixo | Ambiente | Custo |
|---|---|---|
| `nxa_test_…` | Sandbox: dados de um subconjunto real, limites próprios e CNPJs de teste que provocam cada erro | Nenhum: nada é debitado |
| `nxa_live_…` | Produção | Créditos do seu plano |

O ambiente sai do prefixo da chave; o endereço da API é o mesmo. Guarde a chave na variável de ambiente
`NEXTAPI_KEY`, que o cliente lê quando você não passa a chave. **Nunca** a coloque no código, no repositório ou num
front-end: quem tem a chave consome os seus créditos.

## Início rápido

```csharp
using Sharpfy.Nextapi;

using var nextapi = new NextApiClient(); // lê NEXTAPI_KEY

var empresa = await nextapi.Cnpj.ConsultarAsync("11.222.333/0001-81");
Console.WriteLine($"{empresa.RazaoSocial} — {empresa.SituacaoCadastral}");
```

## Configuração

```csharp
var nextapi = new NextApiClient(new NextApiOptions
{
    ApiKey = Environment.GetEnvironmentVariable("NEXTAPI_KEY"), // padrão: NEXTAPI_KEY
    BaseUrl = new Uri("https://api.nextapi.com.br"),             // só https (http apenas para localhost)
    Timeout = TimeSpan.FromSeconds(15),                          // por tentativa; padrão 30 s
    MaxRetries = 3,                                              // padrão 2; 0 desliga
    AppInfo = "minha-app/1.4.0",                                 // identifica a sua aplicação
});
```

| Opção | Padrão | Efeito |
|---|---|---|
| `ApiKey` | `NEXTAPI_KEY` | Chave de API; o prefixo define o ambiente (`nextapi.Environment`) |
| `BaseUrl` | `https://api.nextapi.com.br` | Endereço da API |
| `Timeout` | 30 s | Tempo limite de cada tentativa |
| `MaxRetries` | 2 | Novas tentativas em falha transitória, só em GET |
| `AppInfo` | — | Nome e versão da sua aplicação, nos cabeçalhos |

### ASP.NET Core e injeção de dependência

<!-- readme-check: ignorar (depende do framework) -->
```csharp
using Sharpfy.Nextapi.Extensions;

builder.Services.AddNextApi(o =>
{
    o.ApiKey = builder.Configuration["NextApi:Key"]; // user-secrets, variável NextApi__Key ou cofre
    o.AppInfo = "minha-api/2.0";
});

app.MapGet("/fornecedores/{cnpj}", async (string cnpj, NextApiClient nextapi, CancellationToken ct) =>
    await nextapi.Cnpj.ConsultarAsync(cnpj, cancellationToken: ct));
```

`AddNextApi` registra o cliente com `IHttpClientFactory` (sem seguir redirecionamento) e devolve o
`IHttpClientBuilder`, para você acrescentar handlers, proxy ou políticas.

```json
// appsettings.json: nunca a chave de produção; use user-secrets ou variável de ambiente
{ "NextApi": { "Key": "" } }
```

## Exemplos

### CNPJ

```csharp
// Cadastro (1 crédito). toleranceDays: idade máxima aceita do dado em cache (0 a 45); 0 força tempo real.
var empresa = await nextapi.Cnpj.ConsultarAsync("11222333000181", toleranceDays: 30);
var aoVivo  = await nextapi.Cnpj.ConsultarAoVivoAsync("11222333000181"); // tempo real; exige plano que permita

var simples   = await nextapi.Cnpj.ObterSimplesAsync("11222333000181");
var ies       = await nextapi.Cnpj.ObterInscricoesEstaduaisAsync("11222333000181");
var conform   = await nextapi.Cnpj.ObterConformidadeAsync("11222333000181"); // sanções e dívida ativa
var alfanum   = await nextapi.Cnpj.ConsultarAsync("12.ABC.345/01DE-35");
```

### Lotes (até 100 CNPJs)

```csharp
var cnpjs = new[] { "11222333000181", "45997418000153" };
var lote = await nextapi.Cnpj.ConsultarLoteAsync(cnpjs);         // 1 crédito por CNPJ encontrado
var simplesLote = await nextapi.Cnpj.ObterSimplesLoteAsync(cnpjs);

// Mais de 100: divida em blocos. Lote nunca é repetido sozinho; decida você numa falha.
foreach (var bloco in todos.Chunk(100))
    resultados.AddRange(await nextapi.Cnpj.ConsultarLoteAsync(bloco));
```

### Sócios e grupo econômico

```csharp
var rel = await nextapi.Cnpj.ObterRelacionamentosAsync("11222333000181", page: 1, pageSize: 50);
foreach (var socio in rel.Socios ?? [])
{
    // HashSocio só é estável quando VinculoVerificavel é verdadeiro: não guarde os provisórios.
    var empresasDoSocio = await nextapi.Socios.ListarEmpresasAsync(socio.HashSocio!);
}
```

### CEP

```csharp
var cep = await nextapi.Ceps.ConsultarAsync("01001-000");
var porRua = await nextapi.Ceps.BuscarAsync(new CepFiltro { Uf = "SP", NomeMunicipio = "São Paulo", Logradouro = "Paulista" });
var raio = await nextapi.Ceps.BuscarProximosAsync(-23.5505, -46.6333, raioMetro: 500);
var vizinhos = await nextapi.Ceps.BuscarProximosDoCepAsync("01001000", raioMetro: 1000, pageSize: 100);
```

### Pesquisa de empresas, com paginação

```csharp
var filtro = new EmpresaFiltro
{
    Uf = "SP",
    Cnaes = "1091101",                       // vários: separados por vírgula
    DataAberturaInicio = new DateOnly(2024, 1, 1),
    PageSize = 100,
};

for (var page = 1; ; page++)
{
    var pagina = await nextapi.Empresas.PesquisarAsync(filtro with { Page = page });
    foreach (var e in pagina.Items) Console.WriteLine($"{e.Cnpj} {e.RazaoSocial}");
    if (page * pagina.PageSize >= pagina.Total) break;
}
```

### Localidades, mercado e créditos

```csharp
var estados = await nextapi.Estados.ListarAsync();
var municipios = await nextapi.Municipios.ListarAsync(uf: "MG", pageSize: 100);
var bh = await nextapi.Municipios.ObterPorIdAsync(3106200);
var cnaes = await nextapi.Cnaes.ListarAsync(page: 1, pageSize: 50);

var mercado = await nextapi.Mercado.ObterCnaeAsync("1091101", uf: "SP", meses: 12);
var creditos = await nextapi.Creditos.ConsultarAsync();
Console.WriteLine($"restam {creditos.TotalRemainingCredits} créditos");
```

Todo método aceita `CancellationToken` como último parâmetro.

## Erros

Toda falha vira uma exceção do SDK; a classe diz o que fazer, e o código detalha.

| Classe | Quando | Status / código | O que fazer |
|---|---|---|---|
| `NextApiAuthenticationException` | Chave ausente, inválida ou revogada | 401 / 1002, 400 / 1001 | Confira a chave e o ambiente |
| `NextApiInsufficientCreditsException` | Créditos do período acabaram | 405 / 1004, 1006 | Aguarde a renovação ou compre créditos avulsos |
| `NextApiPaidPlanRequiredException` | Recurso exige plano pago | 405 / 1012, 1005 | Mude de plano |
| `NextApiNotFoundException` | CNPJ, CEP ou município não existe | 404 / 1000 | Trate como resultado, não como falha |
| `NextApiValidationException` | Entrada recusada pela API ou pelo SDK | 400 / 1007, ou status 0 | Corrija a entrada; não repita |
| `NextApiRateLimitException` | Limite de requisições por minuto | 429 / 1003 | Já repetido sozinho; diminua o ritmo |
| `NextApiServerException` | Falha da API ou do provedor externo | 5xx / 1014, 9999 | 503 é repetido sozinho; persistindo, fale com o suporte |
| `NextApiConnectionException` | Rede, tempo esgotado ou redirecionamento | status 0 | Verifique a conexão; GETs já foram repetidos |

Todas trazem o status HTTP, o código da API, a mensagem, o corpo da resposta e o **request id**. Ao falar com o
suporte, mande o request id: ele localiza a requisição nos nossos logs.

```csharp
try
{
    var empresa = await nextapi.Cnpj.ConsultarAsync(cnpj);
}
catch (NextApiNotFoundException)
{
    return null; // não existe
}
catch (NextApiInsufficientCreditsException e)
{
    logger.LogWarning("Sem créditos ({RequestId})", e.RequestId);
}
catch (NextApiRateLimitException e)
{
    await Task.Delay(e.RetryAfter ?? TimeSpan.FromSeconds(5));
}
catch (NextApiException e) // qualquer outra falha do SDK
{
    logger.LogError(e, "NextAPI {Status}/{Code} req={RequestId}", e.Status, e.Code, e.RequestId);
}
```

Os códigos estão em `NextApiErrorCodes` (`NextApiErrorCodes.MissingCredits` etc.).

## Comportamento

**Novas tentativas.** Só requisições `GET` são repetidas, e só em falha transitória: `429`, `502`, `503`, `504` e
erro de rede. O cliente respeita o `Retry-After` da resposta (até 60 s); sem ele, espera de 0,5 s a 8 s, com
crescimento exponencial e variação aleatória. Os lotes (`POST`) **nunca** são repetidos sozinhos, porque consomem
créditos: numa falha, decida você se repete. O padrão é 2 novas tentativas; `MaxRetries = 0` desliga.

**Tempo limite.** `Timeout` por tentativa (padrão 30 s). Com novas tentativas, o tempo total pode passar disso.

**Validação local.** Entrada que a API recusaria com certeza nem sai do processo: vira
`NextApiValidationException` (status 0) antes da requisição. É o caso de CNPJ com tamanho ou dígitos verificadores errados,
lote vazio ou acima de 100, CEP sem 8 dígitos, raio menor ou igual a zero e latitude ou longitude fora da faixa.
CNPJ e CEP aceitam pontuação (`11.222.333/0001-81`, `01001-000`).

**CNPJ alfanumérico.** O formato novo da Receita (IN RFB 2.229/2024, a partir de julho de 2026) é aceito em todos os
métodos de CNPJ: `12.ABC.345/01DE-35` vira `12ABC34501DE35`, com letras em maiúsculas.

**Segurança.** A chave não aparece em logs, mensagens de erro nem na representação do cliente. O cliente não segue
redirecionamento, para a chave nunca ir para outro host, e só aceita `https` no `baseUrl` (`http` apenas para
`localhost`).

**Cabeçalhos enviados.** Além da autorização, cada requisição leva `User-Agent`, `X-NextApi-Client`
(`dotnet/<versão>`) e `X-NextApi-Client-Info` (runtime, sistema e o seu `appInfo`). Servem para o suporte e para
medirmos o uso de cada versão; não carregam dado do seu negócio. O `appInfo` passa por um filtro: só
`A-Za-z0-9._/@+-`, até 64 caracteres.

**Reuso.** `NextApiClient` é thread-safe: crie um só e reutilize (ou registre com `AddNextApi`). Criar um por requisição desperdiça conexões. Com um `HttpClient` próprio, passe-o no construtor: `new NextApiClient(options, httpClient)`; nesse caso desligue o redirecionamento automático no handler.

## Sandbox

Com a chave `nxa_test_`, estes CNPJs provocam cada resposta da API, para você testar o tratamento de erro sem
gastar nada:

| CNPJ | Resposta |
|---|---|
| `99999991000125` | 404, não encontrado |
| `99999992000170` | 405 / 1004, sem créditos |
| `99999993000114` | 429 / 1003, limite por minuto |
| `99999994000169` | 503 / 1014, falha do provedor |
| `99999995000103` | sucesso depois de 8 s (consulta lenta) |
| `99999996000158` | 405 / 1012, exige plano pago |
| `99999997000100` | 401 / 1002, chave inválida |

O sandbox tem limite próprio de requisições por minuto e por dia, e os dados vêm de um subconjunto real: um CNPJ ou
CEP pode existir em produção e não no sandbox.

## Testes

Os testes não usam rede: conferem cada método contra o contrato da API, em `contract/cases.json` (rota, parâmetros, corpo, respostas e erros).

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test
```

Com `NEXTAPI_SMOKE_KEY` definida (e `NEXTAPI_SMOKE_ENV=sandbox` ou `production`), roda também a fumaça contra a API
publicada, com os passos de `contract/smoke.json`.

## Suporte e licença

Dúvidas e problemas: abra uma issue neste repositório ou escreva pelo [app.nextapi.com.br](https://app.nextapi.com.br),
com o request id do erro. Licença MIT.
