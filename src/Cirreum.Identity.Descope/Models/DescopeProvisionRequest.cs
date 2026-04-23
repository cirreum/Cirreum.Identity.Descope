namespace Cirreum.Identity.Descope.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The request body posted by a Descope HTTP Connector to the provisioning endpoint.
/// The Descope Connector's body-template must populate these fields from the flow context.
/// See SETUP.md for the recommended body-template mapping.
/// </summary>
internal sealed record DescopeProvisionRequest {

	/// <summary>
	/// The Descope user ID (loginId or userId). Maps to
	/// <see cref="Cirreum.Identity.ProvisionContext.ExternalUserId"/>.
	/// </summary>
	[JsonPropertyName("externalUserId")]
	public string ExternalUserId { get; init; } = "";

	/// <summary>
	/// The user's email address, if available from the flow context.
	/// </summary>
	[JsonPropertyName("email")]
	public string Email { get; init; } = "";

	/// <summary>
	/// A flow-scoped correlation identifier (typically the flow execution ID).
	/// Used for end-to-end request tracing.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

	/// <summary>
	/// The Descope project or application identifier that initiated the flow. Validated against
	/// <see cref="DescopeOptions.AllowedAppIds"/> when that allowlist is configured.
	/// </summary>
	[JsonPropertyName("clientAppId")]
	public string ClientAppId { get; init; } = "";

}
