namespace Cirreum.Identity.Descope;

using Cirreum.Identity;
using Cirreum.Identity.Descope.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Handles the Descope HTTP Connector call invoked by a flow before token issuance.
/// Validates the shared secret, checks the calling app is allowed, provisions the user
/// via <see cref="IUserProvisioner"/>, and returns the provisioning decision to Descope.
/// </summary>
internal sealed partial class DescopeConnectorHandler(
	DescopeSharedSecretValidator secretValidator,
	IOptions<DescopeOptions> options,
	IServiceProvider services,
	ILogger<DescopeConnectorHandler> logger
) {

	public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default) {

		// -------------------------------------------------------------------------
		// 1. Validate shared secret
		// -------------------------------------------------------------------------

		if (!secretValidator.Validate(request)) {
			return Results.Unauthorized();
		}

		// -------------------------------------------------------------------------
		// 2. Deserialize payload
		// -------------------------------------------------------------------------

		DescopeProvisionRequest? payload;
		try {
			payload = await request.ReadFromJsonAsync(
				DescopeJsonContext.Default.DescopeProvisionRequest,
				cancellationToken);
		} catch (Exception ex) {
			Log.DeserializationFailed(logger, ex);
			return Results.BadRequest("Invalid request body");
		}

		if (payload is null) {
			Log.DeserializationFailed(logger, null);
			return Results.BadRequest("Invalid request body");
		}

		// -------------------------------------------------------------------------
		// 3. Validate required fields
		// -------------------------------------------------------------------------

		if (string.IsNullOrWhiteSpace(payload.ExternalUserId)) {
			Log.MissingExternalUserId(logger);
			return Results.BadRequest("Missing externalUserId");
		}

		if (string.IsNullOrWhiteSpace(payload.CorrelationId)) {
			Log.MissingCorrelationId(logger);
			return Results.BadRequest("Missing correlationId");
		}

		// -------------------------------------------------------------------------
		// 4. Validate calling app (when an allowlist is configured)
		// -------------------------------------------------------------------------

		var config = options.Value;
		if (config.HasAllowedAppIds()) {
			var allowedApps = config.GetAllowedAppIdSet();
			if (!allowedApps.Contains(payload.ClientAppId)) {
				Log.AppNotAllowed(logger, payload.ClientAppId);
				return Results.Forbid();
			}
		}

		// -------------------------------------------------------------------------
		// 5. Provision user
		// -------------------------------------------------------------------------

		var provisionContext = new ProvisionContext {
			Source = DescopeSource.Name,
			ExternalUserId = payload.ExternalUserId,
			CorrelationId = payload.CorrelationId,
			ClientAppId = payload.ClientAppId,
			Email = payload.Email
		};

		var provisioner = services.GetRequiredKeyedService<IUserProvisioner>(DescopeSource.Name);
		ProvisionResult provisionResult;
		try {
			provisionResult = await provisioner.ProvisionAsync(provisionContext, cancellationToken);
		} catch (Exception ex) {
			Log.ProvisionerFailed(logger, ex, provisionContext.ExternalUserId);
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		// -------------------------------------------------------------------------
		// 6. Map provision result to response
		// -------------------------------------------------------------------------

		if (provisionResult is ProvisionResult.Denied) {
			Log.UserDenied(logger, provisionContext.ExternalUserId);
			var denyBody = new DescopeProvisionResponse {
				Allowed = false,
				Roles = [],
				CorrelationId = payload.CorrelationId
			};
			return Results.Json(
				denyBody,
				DescopeJsonContext.Default.DescopeProvisionResponse,
				statusCode: StatusCodes.Status403Forbidden);
		}

		if (provisionResult is not ProvisionResult.Allowed { Roles: { Count: > 0 } roles }) {
			if (provisionResult is ProvisionResult.Allowed) {
				Log.ProvisionerAllowedWithNoRoles(logger, provisionContext.ExternalUserId);
			} else {
				Log.ProvisionerFailed(logger, null, provisionContext.ExternalUserId);
			}
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		var rolesStr = string.Join(",", roles);
		Log.IssuingRoles(logger, rolesStr, provisionContext.ExternalUserId, payload.CorrelationId);

		// -------------------------------------------------------------------------
		// 7. Build and return response
		// -------------------------------------------------------------------------

		var body = new DescopeProvisionResponse {
			Allowed = true,
			Roles = [.. roles],
			CorrelationId = payload.CorrelationId
		};

		return Results.Json(body, DescopeJsonContext.Default.DescopeProvisionResponse);
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize Descope connector request body.")]
		internal static partial void DeserializationFailed(ILogger logger, Exception? ex);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing externalUserId in Descope connector request.")]
		internal static partial void MissingExternalUserId(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing correlationId in Descope connector request.")]
		internal static partial void MissingCorrelationId(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "App '{AppId}' is not in the allowed list.")]
		internal static partial void AppNotAllowed(ILogger logger, string appId);

		[LoggerMessage(Level = LogLevel.Information, Message = "User '{UserId}' was denied by provisioner. Blocking token issuance.")]
		internal static partial void UserDenied(ILogger logger, string userId);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Provisioner returned Allowed with no roles for user '{UserId}'. Blocking token issuance.")]
		internal static partial void ProvisionerAllowedWithNoRoles(ILogger logger, string userId);

		[LoggerMessage(Level = LogLevel.Error, Message = "Provisioner failed for user '{UserId}'. Blocking token issuance.")]
		internal static partial void ProvisionerFailed(ILogger logger, Exception? ex, string userId);

		[LoggerMessage(Level = LogLevel.Information, Message = "Issuing roles '{Roles}' for user '{UserId}' (correlation: {CorrelationId}).")]
		internal static partial void IssuingRoles(ILogger logger, string roles, string userId, string correlationId);
	}

}
