namespace Warden.Configuration;

// Policy names as constants; a literal typo silently unprotects an endpoint, a typo here is a compile error
internal static class RateLimitPolicies
{
    public const string Api = "api-limit";
}
