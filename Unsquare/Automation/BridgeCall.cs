#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Unsquare.Automation
{
    /// <summary>One HTTP request waiting for the main thread; the listener thread awaits <see cref="Completion"/>.</summary>
    internal sealed class BridgeCall
    {
        public BridgeCall(int id, string method, string path, IReadOnlyDictionary<string, string> query, JObject body)
        {
            Id = id;
            Method = method;
            Path = path;
            Query = query;
            Body = body;
        }

        public int Id { get; }
        public string Method { get; }
        public string Path { get; }
        public IReadOnlyDictionary<string, string> Query { get; }
        public JObject Body { get; }

        /// <summary>Status and JSON body of the reply; set once on the main thread.</summary>
        public TaskCompletionSource<(int Status, JObject Body)> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
#endif
