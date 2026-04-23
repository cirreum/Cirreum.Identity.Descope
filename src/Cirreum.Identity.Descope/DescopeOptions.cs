namespace Cirreum.Identity.Descope;

/// <summary>
/// Configuration options for the Descope HTTP Connector provisioning endpoint.
/// Bind from appsettings.json under the section name specified during registration.
/// </summary>
/// <remarks>
/// See SETUP.md for full configuration instructions, Descope Console setup steps,
/// and troubleshooting guidance.
/// </remarks>
public sealed class DescopeOptions {

	/// <summary>
	/// The endpoint route path. Defaults to <c>/auth/descope/provision</c>.
	/// Must match the <c>Base URL</c> + <c>Endpoint</c> configured on the HTTP Connector
	/// in the Descope Console.
	/// </summary>
	public string Route { get; set; } = "/auth/descope/provision";

	/// <summary>
	/// The shared secret used to authenticate Descope-originated calls.
	/// Descope sends this in the <see cref="AuthorizationHeaderName"/> header on every connector call
	/// (prefixed with <see cref="AuthorizationScheme"/> if one is configured).
	/// </summary>
	/// <remarks>
	/// Required. Generate a long random value (32+ bytes, base64/hex encoded) and configure
	/// it as a static header/bearer token on the HTTP Connector in the Descope Console.
	/// Never commit the secret to source control — keep it in user-secrets, Key Vault, or
	/// an equivalent secret store.
	/// </remarks>
	public required string SharedSecret { get; set; }

	/// <summary>
	/// The HTTP header Descope uses to transmit the shared secret.
	/// Defaults to <c>Authorization</c>.
	/// </summary>
	public string AuthorizationHeaderName { get; set; } = "Authorization";

	/// <summary>
	/// The scheme prefix Descope places before the shared secret in the authorization header.
	/// Defaults to <c>Bearer</c>. Set to an empty string to compare the header value directly
	/// (for an API-key-style header).
	/// </summary>
	public string AuthorizationScheme { get; set; } = "Bearer";

	/// <summary>
	/// Optional comma- or semicolon-separated list of client application IDs allowed to trigger
	/// this endpoint. If empty, client app ID enforcement is disabled.
	/// </summary>
	/// <remarks>
	/// Descope can populate the <c>clientAppId</c> field in the request body from the flow context
	/// (for example the Descope Project ID, a Tenant ID, or a custom application identifier).
	/// Set <c>AllowedAppIds</c> to lock down which values are permitted so a leaked shared secret
	/// cannot be used to provision users in a different application.
	/// </remarks>
	public string AllowedAppIds { get; set; } = "";

	/// <summary>
	/// Parses <see cref="AllowedAppIds"/> into a set for fast lookup.
	/// </summary>
	internal HashSet<string> GetAllowedAppIdSet() =>
		[.. this.AllowedAppIds.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

	/// <summary>
	/// Returns <see langword="true"/> if <see cref="AllowedAppIds"/> has been configured with
	/// at least one entry.
	/// </summary>
	internal bool HasAllowedAppIds() =>
		!string.IsNullOrWhiteSpace(this.AllowedAppIds);

}
