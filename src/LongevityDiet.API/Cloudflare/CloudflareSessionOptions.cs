namespace LongevityDiet.API.Cloudflare;

public sealed class CloudflareSessionOptions
{
    public const string SectionName = "CloudflareSessions";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ServiceBearer { get; set; } = string.Empty;
}
