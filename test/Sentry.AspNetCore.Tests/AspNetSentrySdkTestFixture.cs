using System;
using Microsoft.AspNetCore.Hosting;
using Sentry.Testing;

namespace Sentry.AspNetCore.Tests
{
    public class AspNetSentrySdkTestFixture : SentrySdkTestFixture
    {
        protected Action<SentryAspNetCoreOptions> Configure;

        protected Action<WebHostBuilder> AfterConfigureBuilder;

        public FakeServer FakeServer { get; set; } = new FakeSentryServer();

        public AspNetSentrySdkTestFixture()
        {
            FakeServer.RequestHandlers.Add(new RequestHandler
            {
                Path = "/",
                Response = "home"
            });

            FakeServer.RequestHandlers.Add(new RequestHandler
            {
                Path = "/throw",
                Handler = _ => throw new Exception("test error")
            });
        }

        protected override void ConfigureBuilder(WebHostBuilder builder)
        {
            builder.UseSentry(options =>
            {
                options.Dsn = DsnSamples.ValidDsnWithSecret;
                options.Init(i =>
                {
                    i.Http(h =>
                    {
                        h.SentryHttpClientFactory = );
                    });
                });

                Configure?.Invoke(options);
            });

            AfterConfigureBuilder?.Invoke(builder);
        }
    }
}
