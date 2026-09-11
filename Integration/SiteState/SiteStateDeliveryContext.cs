namespace YAGOT_2._0.Integration.SiteState;

public sealed record SiteStateDeliveryContext(Guid DeliveryId, byte[] PayloadSha256)
{
    public void Validate()
    {
        if (DeliveryId == Guid.Empty)
        {
            throw new SiteStateContractValidationException("DeliveryId cannot be empty.");
        }

        if (PayloadSha256 is null ||
            PayloadSha256.Length != SiteStateContractV1.PayloadSha256Length)
        {
            throw new SiteStateContractValidationException(
                $"PayloadSha256 must contain exactly {SiteStateContractV1.PayloadSha256Length} bytes.");
        }
    }
}
