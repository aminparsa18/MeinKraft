using MeinKraft;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

public class ServerSystemHttpServer : ServerSystem
{
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private readonly ILanguageService _languageService;
    private readonly ServerConfig _config;
    private readonly ServerGameService server;
    public ServerSystemHttpServer(ServerGameService server, IModEvents modEvents, ILanguageService languageService,
        IOptions<ServerConfig> options) : base(modEvents)
    {
        this.server = server;
        _languageService = languageService;
        _config = options.Value;
    }

    protected override void Initialize()
    {
        if (!_config.EnableHTTPServer)
        {
            return;
        }

        int httpPort = server.Port + 1;
        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{httpPort}/");
            _listener.Start();

            _ = ListenAsync(server, _cts.Token);

            Console.WriteLine(_languageService.ServerHTTPServerStarted(), httpPort);
        }
        catch
        {
            Console.WriteLine(_languageService.ServerHTTPServerError(), httpPort);
        }
    }

    private async Task ListenAsync(ServerGameService server, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                _ = Task.Run(() => RouteRequest(server, context), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task RouteRequest(ServerGameService server, HttpListenerContext context)
    {
        try
        {
            // check main module first, then installed modules
            IEnumerable<IHttpModule> allModules = Enumerable
                .Repeat<IHttpModule>(new MainHttpModule { server = server }, 1)
                .Concat(server.HttpModules.Select(m => m.module));

            IHttpModule? handler = allModules.FirstOrDefault(m => m.ResponsibleForRequest(context.Request));

            if (handler != null)
            {
                await handler.ProcessAsync(context);
            }
            else
            {
                await WriteResponse(context, 404, "text/plain", "404 - Not Found");
            }
        }
        catch (Exception ex)
        {
            await WriteResponse(context, 500, "text/plain", ex.Message);
        }
    }

    public override void OnRestart(ServerGameService server)
    {
        _cts?.Cancel();
        _listener?.Stop();
        server.HttpModules.Clear();
    }

    // helper the modules can also use
    public static async Task WriteResponse(HttpListenerContext ctx, int statusCode, string contentType, string body)
    {
        var buffer = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = buffer.Length;
        await ctx.Response.OutputStream.WriteAsync(buffer);
        ctx.Response.OutputStream.Close();
    }
}

public class ActiveHttpModule
{
    public string name;
    public Func<string> description;
    public IHttpModule module;
    public bool installed;
}

// -----------------------------------------------------------------------------
internal class MainHttpModule : IHttpModule
{
    public ServerGameService server;

    public bool ResponsibleForRequest(HttpListenerRequest request)
        => request.Url.AbsolutePath.Equals("/", StringComparison.OrdinalIgnoreCase);

    public async Task ProcessAsync(HttpListenerContext context)
    {
        StringBuilder sb = new("<html><body>");

        foreach (ActiveHttpModule? m in server.HttpModules.OrderBy(m => m.name))
        {
            sb.Append($"<a href='/{m.name}'>{m.name}</a> - {m.description()}<br/>");
        }

        sb.Append("</body></html>");

        await ServerSystemHttpServer.WriteResponse(context, 200, "text/html", sb.ToString());
    }
}