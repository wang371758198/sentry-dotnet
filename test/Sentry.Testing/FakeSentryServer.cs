using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sentry.Http;

namespace Sentry.Testing
{
    public class FakeSentryServer : FakeServer
    {
        public ISentryHttpClientFactory SentryHttpClientFactory { get; set; }

        public FakeSentryServer()
        {
            RequestHandlers = new List<RequestHandler>
            {
                new RequestHandler
                {
                    Path = "/store",
                    Handler = c => c.Response.WriteAsync(SentryResponses.SentryOkResponseBody)
                }
            };

            SentryHttpClientFactory = new DelegateHttpClientFactory((d, o) => HttpClient);
        }
    }

    public class FakeServer
    {
        // Can be modified after the server is built. Allows handling the requests into the fake server
        public List<RequestHandler> RequestHandlers { get; set; } = new List<RequestHandler>();

        public Action<IServiceCollection> ConfigureServices { get; set; }

        public TestServer TestServer { get; set; }

        public HttpClient HttpClient { get; set; }
        public IServiceProvider ServiceProvider { get; set; }

        public LastExceptionFilter LastExceptionFilter { get; } = new LastExceptionFilter();
        public LastHttpContext LastHttpContext { get; } = new LastHttpContext();

        public virtual void Start()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(s =>
                {
                    // Stores an unhandled exception in the pipeline
                    var lastException =
                    s.AddSingleton<IStartupFilter>(LastExceptionFilter);
                    s.AddSingleton(LastHttpContext);
                    s.AddSingleton(lastException);

                    ConfigureServices?.Invoke(s);
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Request.EnableRewind();

                        var lastContext = context.RequestServices.GetRequiredService<LastHttpContext>();
                        lastContext.HttpContext = context;

                        var handler = RequestHandlers.FirstOrDefault(p => p.Path == context.Request.Path);

                        await (handler?.Handler(context) ?? next());
                    });
                });

            TestServer = new TestServer(builder);
            HttpClient = TestServer.CreateClient();
            ServiceProvider = TestServer.Host.Services;
        }
    }
}
