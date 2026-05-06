using System.Net;
using TibiaMcp.Server.Services;
using TibiaMcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── In-memory cache & HttpClient for wiki scraping ────────────────────
// Fandom uses Cloudflare which blocks .NET's HttpClient TLS fingerprint.
// We use a CookieContainer to capture the __cf_bm cookie from the 403
// challenge response, then reuse it on the MediaWiki API (which is
// allowed through with the cookie).
builder.Services.AddMemoryCache();

var cookieContainer = new CookieContainer();

void ConfigureWikiClient(HttpClient client)
{
    client.BaseAddress = new Uri("https://tibia.fandom.com");
    client.Timeout = TimeSpan.FromSeconds(30);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/120.0.0.0 Safari/537.36");

    client.DefaultRequestHeaders.Accept.ParseAdd(
        "text/html,application/xhtml+xml,application/xml;q=0.9," +
        "image/avif,image/webp,image/apng,*/*;q=0.8");

    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
}

// Each typed HttpClient gets its own SocketsHttpHandler, but they all
// share the same CookieContainer so the Cloudflare __cf_bm cookie is
// available to every service.
SocketsHttpHandler CreateHandler() => new()
{
    MaxConnectionsPerServer = 2,
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AutomaticDecompression = System.Net.DecompressionMethods.GZip
                              | System.Net.DecompressionMethods.Deflate
                              | System.Net.DecompressionMethods.Brotli,
    UseCookies = true,
    CookieContainer = cookieContainer,
};

builder.Services.AddHttpClient<ConditionWikiService>(ConfigureWikiClient)
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

builder.Services.AddHttpClient<SpellWikiService>(ConfigureWikiClient)
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

builder.Services.AddHttpClient<CreatureWikiService>(ConfigureWikiClient)
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

// ── MCP Server ────────────────────────────────────────────────────────────
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "TibiaMcp",
            Version = "1.0.0",
        };
    })
    .WithHttpTransport()
    .WithTools<ConditionTools>()
    .WithTools<SpellTools>()
    .WithTools<CreatureTools>();

var app = builder.Build();

// ── Endpoints ─────────────────────────────────────────────────────────────
app.MapMcp("/mcp");

app.MapGet("/", () => Results.Ok(new
{
    service = "TibiaMcp MCP Server",
    status = "running",
    mcpEndpoint = "/mcp"
}));

app.Logger.LogInformation("TibiaMcp MCP Server starting...");
app.Run();
