namespace GentleStore.Domain.Enums;

/// <summary>How a store's storefront may be reached by customers.</summary>
public enum StorefrontAccessMode
{
    /// <summary>Anyone with the link can browse; the store is listed in the public directory.</summary>
    Public = 0,

    /// <summary>Only browsers that redeemed a personal invite link can browse; hidden from the directory.</summary>
    InviteOnly = 1
}
