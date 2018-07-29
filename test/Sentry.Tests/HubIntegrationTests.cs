using System;
using System.Runtime.InteropServices;
using Sentry.Internal;
using Sentry.PlatformAbstractions;
using Sentry.Testing;
using Xunit;

namespace Sentry.Tests
{
    public class HubIntegrationTests
    {
        public FakeSentryServer FakeSentryServer { get; set; } = new FakeSentryServer();

        public IHub Sut { get; set; }

        public HubIntegrationTests()
        {
            FakeSentryServer.Start();

            var o = new SentryOptions();
            o.Dsn = DsnSamples.Valid;

            o.Http(h => h.SentryHttpClientFactory = FakeSentryServer.SentryHttpClientFactory);
            Sut = new Hub(o);
        }

        [Fact]
        public void CaptureEvent_EmptyEvent_IncludeId()
        {
            var evt = new SentryEvent();
            Sut.CaptureEvent(evt);

            var request = FakeSentryServer.LastHttpContext.RequestAsJson();
            Assert.True(Guid.TryParse(request.event_id.ToString(), out Guid id));
            Assert.NotEqual(Guid.Empty, id);
        }

        [Fact]
        public void CaptureEvent_EmptyEvent_IncludeTimestamp()
        {
            var evt = new SentryEvent();
            Sut.CaptureEvent(evt);

            var request = FakeSentryServer.LastHttpContext.RequestAsJson();
            Assert.True(DateTimeOffset.TryParse(request.timestamp.ToString(), out DateTimeOffset timestamp));
            Assert.NotEqual(default, timestamp);
        }

        [Fact]
        public void CaptureEvent_EmptyEvent_IncludeModules()
        {
            var evt = new SentryEvent();
            Sut.CaptureEvent(evt);

            var request = FakeSentryServer.LastHttpContext.RequestAsJson();
            Assert.NotEmpty(request.modules);
            // Sentry was reported
            Assert.Equal(
                typeof(Hub).Assembly.GetName().Version.ToString(),
                request.modules["Sentry"].ToString());
        }

        [Fact]
        public void CaptureEvent_EmptyEvent_IncludeRuntime()
        {
            var evt = CreateEvent();
            Sut.CaptureEvent(evt);

            var request = FakeSentryServer.LastHttpContext.RequestAsJson();
            Assert.NotEmpty(request.contexts.runtime);
            Assert.Equal(Runtime.Current.Name, request.contexts.runtime.name.ToString());
            Assert.Equal(Runtime.Current.Version, request.contexts.runtime.version.ToString());
        }


        [Fact]
        public void CaptureEvent_EmptyEvent_IncludeOs()
        {
            var evt = CreateEvent();
            Sut.CaptureEvent(evt);

            var request = FakeSentryServer.LastHttpContext.RequestAsJson();
            Assert.NotEmpty(request.contexts.os);

            // RuntimeInformation.OSDescription is throwing on Mono 5.12
            if (!Runtime.Current.IsMono())
            {
                Assert.Equal(
                    RuntimeInformation.OSDescription,
                    request.contexts.os.raw_description.ToString());
            }
        }

        private SentryEvent CreateEvent()
        {
            void CreateException()
            {
                try
                {
                    throw new InvalidOperationException("inner exception message");
                }
                catch (Exception e)
                {
                    e.Data.Add("exception-data", new
                    {
                        Prop = "value"
                    });
                    throw new AggregateException(e);
                }
            }

            Exception exception;
            try
            {
                CreateException();
            }
            catch (Exception e)
            {
                exception = e;
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            var evt = new SentryEvent(exception);
            evt.Message = "event message";

            return evt;
        }
    }
}
