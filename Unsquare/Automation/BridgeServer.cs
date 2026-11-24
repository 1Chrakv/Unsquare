#if UNSQUARE_AUTOMATION
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unsquare.Automation
{
    /// <summary>
    /// Loopback HTTP listener of the automation bridge. It only queues requests; the main thread runs them. Calls
    /// that outlast their timeout answer 202 with a job URL to poll, so long loads never hang a client.
    /// </summary>
    internal sealed class BridgeServer : IDisposable
    {
        private const int DefaultTimeoutMs = 30_000;
        private const int MaxTimeoutMs = 600_000;
        private const int MaxFinishedJobs = 64;

        private readonly string _token;
        private readonly HttpListener _listener = new();
        private readonly ConcurrentQueue<BridgeCall> _queue = new();
        private readonly ConcurrentDictionary<int, BridgeCall> _jobs = new();
        private readonly CancellationTokenSource _stop = new();
        private int _nextId;

        public BridgeServer(int port, string token)
        {
            _token = token;
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            Port = port;
        }

        public int Port { get; }

        public void Start()
        {
            _listener.Start();
            Task.Run(ListenLoop);
        }

        /// <summary>Main thread: the next queued request, if any.</summary>
        public bool TryDequeue(out BridgeCall call) => _queue.TryDequeue(out call);

        public void Dispose()
        {
            _stop.Cancel();
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // shutting down anyway
            }
            foreach (var call in _queue)
                call.Completion.TrySetResult((503, Error("The bridge stopped.")));
        }

        private async Task ListenLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    Mod.Log.Warn($"[Auto] Listener error: {e.Message}");
                    continue;
                }
                _ = Task.Run(() => Serve(context));
            }
        }

        private async Task Serve(HttpListenerContext context)
        {
            int status;
            JObject body;
            try
            {
                (status, body) = await Handle(context.Request).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                (status, body) = (500, Error(e.Message));
            }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                context.Response.Close();
            }
            catch (Exception)
            {
                // client gone
            }
        }

        private async Task<(int, JObject)> Handle(HttpListenerRequest request)
        {
            // Browsers send Origin on cross-site requests; a web page must never drive the game.
            if (request.Headers["Origin"] != null)
                return (403, Error("Requests from web pages are refused."));
            if (request.Headers["X-Unsquare-Token"] != _token)
                return (401, Error("Missing or wrong X-Unsquare-Token (see ModsData\\Unsquare\\automation\\token)."));
            if (request.HttpMethod == "OPTIONS")
                return (405, Error("No CORS."));

            // The raw path keeps escapes: Url decodes %23 to '#' and drops the rest of an entity ref as a fragment.
            var path = request.RawUrl.Split('?')[0];
            if (request.HttpMethod == "GET" && path.StartsWith("/v1/jobs/", StringComparison.Ordinal))
                return JobStatus(path.Substring("/v1/jobs/".Length));

            JObject json;
            try
            {
                json = await ReadBody(request).ConfigureAwait(false);
            }
            catch (JsonException e)
            {
                return (400, Error($"Body is not a JSON object: {e.Message}"));
            }
            var query = new Dictionary<string, string>();
            foreach (string key in request.QueryString)
                if (key != null)
                    query[key] = request.QueryString[key];

            var call = new BridgeCall(Interlocked.Increment(ref _nextId), request.HttpMethod, path, query, json);
            var timeout = Math.Min(MaxTimeoutMs, Math.Max(0, json.Value<int?>("timeoutMs") ?? DefaultTimeoutMs));
            _jobs[call.Id] = call;
            _queue.Enqueue(call);
            var finished = await Task.WhenAny(call.Completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished != call.Completion.Task)
                return (202, new JObject { ["ok"] = false, ["pending"] = true, ["job"] = $"/v1/jobs/{call.Id}" });
            Forget(call.Id);
            return call.Completion.Task.Result;
        }

        private (int, JObject) JobStatus(string idText)
        {
            if (!int.TryParse(idText, out var id) || !_jobs.TryGetValue(id, out var call))
                return (404, Error($"Unknown job {idText}."));
            if (!call.Completion.Task.IsCompleted)
                return (202, new JObject { ["ok"] = false, ["pending"] = true, ["job"] = $"/v1/jobs/{id}" });
            Forget(id);
            return call.Completion.Task.Result;
        }

        // Finished jobs stay pollable once; very old ones are dropped so the table can't grow without bound.
        private void Forget(int id)
        {
            _jobs.TryRemove(id, out _);
            if (_jobs.Count <= MaxFinishedJobs)
                return;
            foreach (var pair in _jobs)
                if (pair.Value.Completion.Task.IsCompleted && pair.Key < id - MaxFinishedJobs)
                    _jobs.TryRemove(pair.Key, out _);
        }

        private static async Task<JObject> ReadBody(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return new JObject();
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var text = await reader.ReadToEndAsync().ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
        }

        public static JObject Error(string message) => new() { ["ok"] = false, ["error"] = message };
    }
}
#endif
