namespace GentleStore.Api.Auth;

public static class AppClaimTypes
{
    public const string StoreId = "store_id";
}

public static class Policies
{
    public const string SuperAdmin = "SuperAdmin";
    public const string StoreMember = "StoreMember";
    public const string StoreOwner = "StoreOwner";
}
