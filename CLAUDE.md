# Cirreum.Identity.Descope

## What This Library Does

Hosts the HTTP endpoint that a Descope Flow calls through an HTTP Connector action during
user sign-in. When a user authenticates via Descope, this library:

1. Validates the shared-secret header Descope attaches to every connector call
2. Provisions the user (find existing or redeem invitation) via `IUserProvisioner`
3. Returns `{ allowed, roles, correlationId }` that the Descope flow consumes via
   `connector.response.*`

This is a **server-side endpoint** that supports the **client's sign-in flow**. It can run
in any ASP.NET host: the main API, an Azure Function, or a dedicated service.

## Architecture

- **Framework-independent** — does NOT depend on Cirreum.Core or any Cirreum framework libraries
- Depends on `Cirreum.Identity.Abstractions` for the IdP-agnostic contracts
  (`IUserProvisioner`, `ProvisionContext`, `ProvisionResult`, `IProvisionedUser`,
  `IPendingInvitation`, `UserProvisionerBase<T>`)
- Lives in `C:\Cirreum\Common` (not Infrastructure or Runtime)
- Single NuGet package: `Cirreum.Identity.Descope`

## Key Types

### Public (Descope-specific)
- `DescopeOptions` — configuration options
- `DescopeExtensions` — DI registration + endpoint mapping (`AddDescopeProvisioning<T>()`,
  `MapDescopeProvisioning()`)

### Public (inherited via Cirreum.Identity.Abstractions)
- `IUserProvisioner`, `UserProvisionerBase<TUser>`
- `IProvisionedUser`, `IPendingInvitation`
- `ProvisionContext`, `ProvisionResult`

### Internal
- `DescopeSharedSecretValidator` — constant-time shared-secret comparison
- `DescopeConnectorHandler` — request orchestration
- `DescopeJsonContext` — AOT-compatible source-gen JSON
- `Models/DescopeProvisionRequest` — connector callback request DTO
- `Models/DescopeProvisionResponse` — connector callback response DTO

## Commands

```powershell
# Build
dotnet build Cirreum.Identity.Descope.slnx

# Pack
dotnet pack Cirreum.Identity.Descope.slnx -c Release -o ./artifacts
```

## Configuration Section

Default: `Cirreum:Identity:Descope`

## Testing

There are no test projects in this repository. The library is tested via integration with
consuming applications.

## Security Design

- The callback endpoint is `AllowAnonymous` — authentication is performed internally by
  comparing a shared secret against the header Descope attaches to every connector request
- Shared-secret comparison uses `CryptographicOperations.FixedTimeEquals` to avoid timing
  side channels
- Calling applications can be restricted via an `AllowedAppIds` allowlist (matched against
  the `clientAppId` field in the request body)
- The secret must be stored outside source control (user-secrets, Key Vault, etc.) and
  must match the value configured on the Descope HTTP Connector exactly

## Design Notes

- **Shared secret, not a Descope-signed JWT.** Descope HTTP Connectors authenticate outbound
  calls with a static credential (Bearer token / API key / Basic auth), not a JWT signed by
  Descope's keys. If a JWT-authenticated outbound mode becomes available on Descope, a
  future package version can add a JWT validator alongside the shared-secret validator.
- **Canonical request body.** The request DTO names (`externalUserId`, `email`,
  `correlationId`, `clientAppId`) are what the endpoint deserialises. The user populates
  these fields in the Descope Connector's body template using whatever flow expressions
  resolve to those values.
- **HTTP status + body for denial.** The handler returns `403 Forbidden` with
  `{ "allowed": false, ... }` on denial so Descope flows can branch on either the status
  code or the response body, depending on how the flow action is configured.
