using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Sentry.Protocol;
using System.Diagnostics;
using System.Threading.Tasks;
using Sentry.Extensibility;
using Sentry.Infrastructure;
using Sentry.Internal;

namespace Sentry
{
    /// <summary>
    /// Sentry SDK entrypoint
    /// </summary>
    /// <remarks>
    /// This is a façade to the SDK instance.
    /// It allows safe static access to a client and scope management.
    /// When the SDK is uninitialized, calls to this class result in no-op so no callbacks are invoked.
    /// </remarks>
    public static class SentrySdk
    {
        private static IHub _hub = DisabledHub.Instance;

        /// <summary>
        /// Initializes the SDK while attempting to locate the DSN
        /// </summary>
        /// <remarks>
        /// If the DSN is not found, the SDK will not change state.
        /// </remarks>
        public static IDisposable Init() => Init(DsnLocator.FindDsnStringOrDisable());

        /// <summary>
        /// Initializes the SDK with the specified DSN
        /// </summary>
        /// <remarks>
        /// An empty string is interpreted as a disabled SDK
        /// </remarks>
        /// <seealso href="https://docs.sentry.io/clientdev/overview/#usage-for-end-users"/>
        /// <param name="dsn">The dsn</param>
        public static IDisposable Init(string dsn)
            => string.IsNullOrWhiteSpace(dsn)
                ? DisabledHub.Instance
                : Init(c => c.Dsn = new Dsn(dsn));

        /// <summary>
        /// Initializes the SDK with the specified DSN
        /// </summary>
        /// <param name="dsn">The dsn</param>
        public static IDisposable Init(Dsn dsn) => Init(c => c.Dsn = dsn);

        /// <summary>
        /// Initializes the SDK with an optional configuration options callback.
        /// </summary>
        /// <param name="configureOptions">The configure options.</param>
        public static IDisposable Init(Action<SentryOptions> configureOptions)
        {
            var options = new SentryOptions();
            configureOptions?.Invoke(options);

            return Init(options);
        }

        /// <summary>
        /// Initializes the SDK with the specified options instance
        /// </summary>
        /// <param name="options">The options instance</param>
        /// <remarks>
        /// Used by integrations which have their own delegates
        /// </remarks>
        /// <returns>A disposable to close the SDK.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IDisposable Init(SentryOptions options)
        {
            options.SetupLogging();

            if (options.Dsn == null)
            {
                if (!Dsn.TryParse(DsnLocator.FindDsnStringOrDisable(), out var dsn))
                {
                    options.DiagnosticLogger?.LogWarning("Init was called but no DSN was provided nor located. Sentry SDK will be disabled.");
                    return DisabledHub.Instance;
                }
                options.Dsn = dsn;
            }

            var hub = new Hub(options);
            // Push the first scope so the async local starts from here
            hub.PushScope();

            var oldHub = Interlocked.Exchange(ref _hub, hub);
            (oldHub as IDisposable)?.Dispose();

            return new DisposeHandle(hub);
        }

        // For testing
        internal static void UseHub(IHub hub) => _hub = hub;

        /// <summary>
        /// Close the SDK
        /// </summary>
        /// <remarks>
        /// Flushes the events and disables the SDK.
        /// This method is mostly used for testing the library since
        /// Init returns a IDisposable that can be used to shutdown the SDK.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Close()
        {
            var oldHub = Interlocked.Exchange(ref _hub, DisabledHub.Instance);
            (oldHub as IDisposable)?.Dispose();
        }

        private class DisposeHandle : IDisposable
        {
            private IHub _localHub;
            public DisposeHandle(IHub hub) => _localHub = hub;

            public void Dispose()
            {
                Interlocked.CompareExchange(ref _hub, DisabledHub.Instance, _localHub);
                (_localHub as IDisposable)?.Dispose();
                _localHub = null;
            }
        }

        /// <summary>
        /// Whether the SDK is enabled or not
        /// </summary>
        public static bool IsEnabled { [DebuggerStepThrough] get => _hub.IsEnabled; }

        /// <summary>
        /// Creates a new scope that will terminate when disposed
        /// </summary>
        /// <remarks>
        /// Pushes a new scope while inheriting the current scope's data.
        /// </remarks>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state">A state object to be added to the scope</param>
        /// <returns>A disposable that when disposed, ends the created scope.</returns>
        [DebuggerStepThrough]
        public static IDisposable PushScope<TState>(TState state) => _hub.PushScope(state);

        /// <summary>
        /// Creates a new scope that will terminate when disposed
        /// </summary>
        /// <returns>A disposable that when disposed, ends the created scope.</returns>
        [DebuggerStepThrough]
        public static IDisposable PushScope() => _hub.PushScope();

        /// <summary>
        /// Binds the client to the current scope.
        /// </summary>
        /// <param name="client">The client.</param>
        [DebuggerStepThrough]
        public static void BindClient(ISentryClient client) => _hub.BindClient(client);

        /// <summary>
        /// Adds a breadcrumb to the current Scope
        /// </summary>
        /// <param name="message">
        /// If a message is provided it’s rendered as text and the whitespace is preserved.
        /// Very long text might be abbreviated in the UI.</param>
        /// <param name="type">
        /// The type of breadcrumb.
        /// The default type is default which indicates no specific handling.
        /// Other types are currently http for HTTP requests and navigation for navigation events.
        /// <seealso href="https://docs.sentry.io/clientdev/interfaces/breadcrumbs/#breadcrumb-types"/>
        /// </param>
        /// <param name="category">
        /// Categories are dotted strings that indicate what the crumb is or where it comes from.
        /// Typically it’s a module name or a descriptive string.
        /// For instance ui.click could be used to indicate that a click happened in the UI or flask could be used to indicate that the event originated in the Flask framework.
        /// </param>
        /// <param name="data">
        /// Data associated with this breadcrumb.
        /// Contains a sub-object whose contents depend on the breadcrumb type.
        /// Additional parameters that are unsupported by the type are rendered as a key/value table.
        /// </param>
        /// <param name="level">Breadcrumb level.</param>
        /// <seealso href="https://docs.sentry.io/clientdev/interfaces/breadcrumbs/"/>
        [DebuggerStepThrough]
        public static void AddBreadcrumb(
            string message,
            string category = null,
            string type = null,
            IDictionary<string, string> data = null,
            BreadcrumbLevel level = default)
            => _hub.AddBreadcrumb(message, category, type, data, level);

        /// <summary>
        /// Adds a breadcrumb to the current scope
        /// </summary>
        /// <remarks>
        /// This overload is intended to be used by integrations only.
        /// The objective is to allow better testability by allowing control of the timestamp set to the breadcrumb.
        /// </remarks>
        /// <param name="clock">An optional <see cref="ISystemClock"/></param>
        /// <param name="message">The message.</param>
        /// <param name="type">The type.</param>
        /// <param name="category">The category.</param>
        /// <param name="data">The data.</param>
        /// <param name="level">The level.</param>
        [DebuggerStepThrough]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void AddBreadcrumb(
            ISystemClock clock,
            string message,
            string category = null,
            string type = null,
            IDictionary<string, string> data = null,
            BreadcrumbLevel level = default)
            => _hub.AddBreadcrumb(clock, message, category, type, data, level);

        /// <summary>
        /// Configures the scope through the callback.
        /// </summary>
        /// <param name="configureScope">The configure scope callback.</param>
        [DebuggerStepThrough]
        public static void ConfigureScope(Action<Scope> configureScope)
            => _hub.ConfigureScope(configureScope);

        /// <summary>
        /// Configures the scope asynchronously
        /// </summary>
        /// <param name="configureScope">The configure scope callback.</param>
        /// <returns>The Id of the event</returns>
        [DebuggerStepThrough]
        public static Task ConfigureScopeAsync(Func<Scope, Task> configureScope)
            => _hub.ConfigureScopeAsync(configureScope);

        /// <summary>
        /// Captures the event.
        /// </summary>
        /// <param name="evt">The event.</param>
        /// <returns>The Id of the event</returns>
        [DebuggerStepThrough]
        public static Guid CaptureEvent(SentryEvent evt)
            => _hub.CaptureEvent(evt);

        /// <summary>
        /// Captures the event using the specified scope.
        /// </summary>
        /// <param name="evt">The event.</param>
        /// <param name="scope">The scope.</param>
        /// <returns>The Id of the event</returns>
        [DebuggerStepThrough]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Guid CaptureEvent(SentryEvent evt, Scope scope)
            => _hub.CaptureEvent(evt, scope);

        /// <summary>
        /// Captures the exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns>The Id of the event</returns>
        [DebuggerStepThrough]
        public static Guid CaptureException(Exception exception)
            => _hub.CaptureException(exception);

        /// <summary>
        /// Captures the message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="level">The message level.</param>
        /// <returns>The Id of the event</returns>
        [DebuggerStepThrough]
        public static Guid CaptureMessage(string message, SentryLevel level = SentryLevel.Info)
            => _hub.CaptureMessage(message, level);
    }
}
