using System;
using Microsoft.AspNetCore.Hosting;

namespace Sentry.Testing
{
    public abstract class SentrySdkTestFixture : IDisposable
    {
        public FakeSentryServer FakeSentryServer { get; set; } = new FakeSentryServer();

        protected virtual void Build() => FakeSentryServer.Start();

        protected virtual void ConfigureBuilder(WebHostBuilder builder)
        {
            SentrySdk.Init(o =>
            {
                o.Http(h => h.SentryHttpClientFactory = FakeSentryServer.SentryHttpClientFactory);
            });
        }

        public virtual void Dispose() => SentrySdk.Close();
    }
}
