using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The native realization of <see cref="IClock"/>: a <see cref="System.Threading.Timer"/>, which is
/// the platform's own scheduler and needs no help from this framework. Nothing is threaded through
/// the frame loop, because nothing has to be — the shells' loops already tick continuously and
/// present when the host says something changed, so a tick that flips that flag is picked up on the
/// next vsync.
/// <para>
/// The callback therefore arrives on a THREAD-POOL thread, which is the same contract every live
/// capability here already states (see <see cref="INetworkStatus"/>): a realization hands the
/// platform's callback to the app as it arrives, and <c>SetState</c> is what marshals the
/// consequence into the next frame.
/// </para>
/// </summary>
public sealed class PhotonClock : IClock
{
    public IDisposable Every(TimeSpan interval, Action onTick)
    {
        // dueTime == period: the first tick is one interval from now, as the contract says, and .NET
        // schedules the next one after the callback rather than replaying the ones a sleeping device
        // missed — the "one tick, not six hundred" half of the same contract, for free.
        var timer = new System.Threading.Timer(_ => onTick(), null, interval, interval);
        return timer;
    }
}
