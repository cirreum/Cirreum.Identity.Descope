namespace Cirreum.Identity.Descope.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The response body returned to Descope after a provisioning decision.
/// A Descope Flow action can read these fields via <c>connector.response.*</c> to drive
/// conditional branches, set custom attributes, or fail the flow.
/// </summary>
internal sealed record DescopeProvisionResponse {

	/// <summary>
	/// <see langword="true"/> when the provisioner allowed the user; <see langword="false"/>
	/// when the user was denied.
	/// </summary>
	[JsonPropertyName("allowed")]
	public bool Allowed { get; init; }

	/// <summary>
	/// The roles to embed in the issued token (empty when <see cref="Allowed"/> is
	/// <see langword="false"/>).
	/// </summary>
	[JsonPropertyName("roles")]
	public IReadOnlyList<string> Roles { get; init; } = [];

	/// <summary>
	/// Echoes the correlation ID supplied in the request, so the Descope flow log can
	/// match request and response.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

}
