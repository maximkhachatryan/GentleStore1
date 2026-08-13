namespace GentleStore.Api.Storefront;

public static class RateLimitPolicies
{
    /// <summary>Per-IP cap on invite redemption attempts.</summary>
    public const string InviteRedeem = "storefront-invite-redeem";
}
