namespace GentleStore.Domain.Enums;

public enum CustomerOrigin
{
    /// <summary>Added by the store, which then sent them an invite link.</summary>
    StoreInvited = 0,

    /// <summary>Created by the customer themselves while checking out on a public storefront.</summary>
    SelfRegistered = 1
}
