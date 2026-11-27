#if UNSQUARE_AUTOMATION
using Unsquare.Core.Automation;

namespace Unsquare.Automation
{
    /// <summary>A bridge command; runs on the main thread.</summary>
    internal delegate CommandRun BridgeHandler(BridgeArgs args);
}
#endif
