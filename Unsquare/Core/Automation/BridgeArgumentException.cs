using System;

namespace Unsquare.Core.Automation
{
    /// <summary>A bridge request with missing or malformed arguments; answered with 400.</summary>
    public sealed class BridgeArgumentException : ArgumentException
    {
        public BridgeArgumentException(string message) : base(message)
        {
        }
    }
}
