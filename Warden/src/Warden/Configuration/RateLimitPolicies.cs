namespace Warden.Configuration;

// Policy names as constants; a literal typo silently unprotects an endpoint, a typo here is a compile error
internal static class RateLimitPolicies
{
    public const string Api = "api-limit";

    // three requests per save (POST, redirect, GET); the API budget is too tight
    public const string Admin = "admin-limit";

    // Its own bucket, deliberately not Admin's. Sharing one meant a flood of /auth/login spent the
    // same budget a signed-in operator needs for /admin/settings, so anyone could lock the panel out.
    public const string Auth = "auth-limit";
}
