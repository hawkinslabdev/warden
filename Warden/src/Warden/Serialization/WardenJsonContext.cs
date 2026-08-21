using System.Text.Json.Serialization;
using Warden.Models;

namespace Warden.Serialization;

// Source-generated JSON metadata for API response types; no runtime reflection, AOT-ready; unknown types fall back to reflection via the resolver chain
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<PageSummary>))]
[JsonSerializable(typeof(BuildVersionResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(HeartbeatPayload))]
[JsonSerializable(typeof(StatusApiResponse))]
[JsonSerializable(typeof(WebhookPayload))]
internal sealed partial class WardenJsonContext : JsonSerializerContext;
