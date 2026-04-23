namespace Cirreum.Identity.Descope;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Validates the shared-secret authorization header sent by the Descope HTTP Connector.
/// </summary>
/// <remarks>
/// <para>
/// Descope HTTP Connectors authenticate outbound calls by attaching a static credential to
/// each request (typically an <c>Authorization: Bearer &lt;token&gt;</c> header or an
/// <c>X-API-Key</c> header). This validator performs a constant-time comparison between
/// the configured <see cref="DescopeOptions.SharedSecret"/> and the header value supplied
/// by Descope.
/// </para>
/// <para>
/// The validator does not inspect the request body. If stronger per-request authenticity
/// guarantees are required (for example HMAC over the body, or a Descope-signed JWT),
/// replace this validator with an application-specific implementation.
/// </para>
/// </remarks>
internal sealed class DescopeSharedSecretValidator(
	IOptions<DescopeOptions> options,
	ILogger<DescopeSharedSecretValidator> logger) {

	private readonly DescopeOptions _options = options.Value;
	private readonly byte[] _expectedBytes = Encoding.UTF8.GetBytes(options.Value.SharedSecret);

	public bool Validate(HttpRequest request) {

		if (string.IsNullOrEmpty(_options.SharedSecret)) {
			logger.LogError("DescopeOptions.SharedSecret is not configured.");
			return false;
		}

		if (!request.Headers.TryGetValue(_options.AuthorizationHeaderName, out var headerValues)
			|| headerValues.Count == 0) {
			logger.LogWarning("Missing '{Header}' header on Descope connector request.", _options.AuthorizationHeaderName);
			return false;
		}

		var headerValue = headerValues[0];
		if (string.IsNullOrWhiteSpace(headerValue)) {
			logger.LogWarning("Empty '{Header}' header on Descope connector request.", _options.AuthorizationHeaderName);
			return false;
		}

		var presented = ExtractPresentedSecret(headerValue, _options.AuthorizationScheme);
		if (presented is null) {
			logger.LogWarning("'{Header}' header did not include the expected '{Scheme}' scheme.",
				_options.AuthorizationHeaderName, _options.AuthorizationScheme);
			return false;
		}

		var presentedBytes = Encoding.UTF8.GetBytes(presented);
		var match = CryptographicOperations.FixedTimeEquals(presentedBytes, _expectedBytes);

		if (!match) {
			logger.LogWarning("Descope shared secret did not match the configured value.");
		}

		return match;
	}

	private static string? ExtractPresentedSecret(string headerValue, string scheme) {

		headerValue = headerValue.Trim();

		if (string.IsNullOrEmpty(scheme)) {
			return headerValue;
		}

		var prefix = scheme + " ";
		if (!headerValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
			return null;
		}

		return headerValue[prefix.Length..].Trim();
	}

}
