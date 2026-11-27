#if UNSQUARE_AUTOMATION
using System;
using Newtonsoft.Json.Linq;

namespace Unsquare.Automation
{
    /// <summary>
    /// The outcome of a bridge command: finished (result or error with an HTTP status) or still running, in which
    /// case the bridge polls it once per frame on the main thread.
    /// </summary>
    internal sealed class CommandRun
    {
        private readonly Func<CommandRun> _poll;

        private CommandRun(int status, JToken result, string error, Func<CommandRun> poll)
        {
            Status = status;
            Result = result;
            Error = error;
            _poll = poll;
        }

        public int Status { get; }
        public JToken Result { get; }
        public string Error { get; }
        public bool IsPending => _poll != null;

        public static CommandRun Done(JToken result = null) => new(200, result, null, null);

        /// <summary>400 bad arguments, 404 unknown building, 409 wrong game state, 422 rejected by the mod.</summary>
        public static CommandRun Fail(int status, string error, JToken details = null) =>
            new(status, details, error, null);

        /// <summary>Runs <paramref name="poll"/> every frame until it returns a finished run.</summary>
        public static CommandRun Pending(Func<CommandRun> poll) => new(202, null, null, poll);

        public CommandRun Poll() => IsPending ? _poll() ?? this : this;
    }
}
#endif
