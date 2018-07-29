using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sentry.Testing
{
    public class LastExceptionFilter : IStartupFilter
    {
        public Exception LastException { get; set; }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            =>
                e =>
                {
                    e.Use(async (_, n) =>
                    {
                        try
                        {
                            await n();
                        }
                        catch (Exception ex)
                        {
                            LastException = ex;
                        }
                    });

                    next(e);
                };
    }

    public class LastHttpContext
    {
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim();
        private HttpContext _httpContext;

        public HttpContext HttpContext
        {
            get
            {
                if (!_signal.Wait(TimeSpan.FromSeconds(2)))
                {
                    throw new Exception("HttpContext wasn't set within the expected time. Is the background worker running?");
                }

                return _httpContext;
            }
            set
            {
                _httpContext = value;
                _signal.Set();
            }
        }

        public dynamic RequestAsJson()
        {
            using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, true, 1024,
                leaveOpen: true))
            {
                var body = reader.ReadToEnd();
                return body.Length == 0
                    ? null
                    : JsonConvert.DeserializeObject(body);
            }
        }
    }
}
