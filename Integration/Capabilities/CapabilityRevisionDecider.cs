using System.Security.Cryptography;

namespace YAGOT_2._0.Integration.Capabilities;

public static class CapabilityRevisionDecider
{
    public static CapabilityReceiptDecision Decide(long incomingRevision, ReadOnlySpan<byte> incomingHash, long? currentRevision, ReadOnlySpan<byte> currentHash)
    {
        if (currentRevision is null || incomingRevision > currentRevision) return CapabilityReceiptDecision.Applied;
        if (incomingRevision < currentRevision) return CapabilityReceiptDecision.Stale;
        return currentHash.Length == incomingHash.Length && CryptographicOperations.FixedTimeEquals(currentHash, incomingHash)
            ? CapabilityReceiptDecision.Equal
            : CapabilityReceiptDecision.EqualConflict;
    }
}
