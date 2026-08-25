namespace Warden.Configuration;

public sealed record AltchaOptions(bool Enabled = false, double SessionHours = 24);
