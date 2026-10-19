using Unity.Netcode.Components;

namespace HeatRise
{
    // The same client owns movement and animation; observers only receive state.
    public sealed class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
