namespace GentleStore.Api.Storefront;

public static class RateLimitPolicies
{
    /// <summary>Per-IP cap on invite redemption attempts.</summary>
    public const string InviteRedeem = "storefront-invite-redeem";

    /// <summary>
    /// Per-IP cap on order placement. Public checkouts are open to anyone, so this is the floor
    /// under how much junk one source can push into a store's order list.
    /// </summary>
    public const string PlaceOrder = "storefront-place-order";
}
