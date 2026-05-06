using Microsoft.EntityFrameworkCore;
using TibiaMcp.Server.Crawlers;
using TibiaMcp.Server.Data;
using TibiaMcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── HttpClient for crawlers (shared, with polite defaults) ──────────────
builder.Services.AddHttpClient<ConditionCrawler>(client =>
{
    client.BaseAddress = new Uri("https://tibia.fandom.com");
    client.Timeout = TimeSpan.FromSeconds(30);

    // ── Browser-like headers to avoid Cloudflare 403 ────────────────
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

// ── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Crawler services ─────────────────────────────────────────────────────
builder.Services.AddScoped<CrawlerRunner>();

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
    .WithHttpTransport()                          // SSE + Streamable HTTP transport
    .AddAuthorizationFilters()                    // support [Authorize] on tools
    .WithTools<PlayerTools>()
    .WithTools<GuildTools>()
    .WithTools<ConditionTools>();

var app = builder.Build();

// ── Auto-migrate database on startup ─────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        app.Logger.LogInformation("Database ensured created / migrated successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not connect to PostgreSQL. Make sure the database is running and accessible.");
    }
}

// ── Run crawlers on startup ──────────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<CrawlerRunner>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting initial condition crawler...");

        var result = await runner.RunAsync(
            progress: new Progress<string>(msg => logger.LogInformation("Crawl: {Msg}", msg))
        );

        if (result.Success)
        {
            logger.LogInformation(
                "Initial crawl completed: {Conditions} conditions, {Sections} sections in {Duration:F1}s",
                result.TotalConditions, result.TotalSections, result.Duration.TotalSeconds);
        }
        else
        {
            logger.LogWarning(
                "Initial crawl finished with errors: {Conditions} conditions, {Sections} sections, {Errors} errors in {Duration:F1}s",
                result.TotalConditions, result.TotalSections, result.Errors, result.Duration.TotalSeconds);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Initial condition crawler failed. The server will continue running.");
    }
});

// ── MCP Endpoint ──────────────────────────────────────────────────────────
app.MapMcp("/mcp");

// Health-check endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "TibiaMcp MCP Server",
    status = "running",
    mcpEndpoint = "/mcp"
}));

app.Logger.LogInformation("TibiaMcp MCP Server starting on {Urls}", string.Join(", ", app.Urls));
app.Run();
