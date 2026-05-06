using TibiaMcp.Server.Services;
using TibiaMcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── HttpClient for wiki scraping (polite defaults) ──────────────────────
builder.Services.AddHttpClient<ConditionWikiService>(client =>
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
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua",
        "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");

}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    MaxConnectionsPerServer = 2,
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AutomaticDecompression = System.Net.DecompressionMethods.GZip
                              | System.Net.DecompressionMethods.Deflate
                              | System.Net.DecompressionMethods.Brotli,
});

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
    .WithTools<ConditionTools>();

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
