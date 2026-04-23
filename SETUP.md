# Setup Guide — Cirreum.Identity.Descope

This guide walks through the Descope Console configuration and application setup required
to use an **HTTP Connector** action to drive user provisioning from a Descope flow.

> **Descope terminology note.** Descope's product evolves quickly and labels for the same
> concept sometimes differ between their docs, console, and SDKs. This guide uses the terms
> you'll see in the flow editor: **Flow**, **Action**, **HTTP Connector**. If your console
> shows slightly different wording (for example "Outbound Connector" instead of
> "HTTP Connector"), the concepts still map one-to-one.

## Prerequisites

- A **Descope project** (create one at [descope.com](https://descope.com))
- At least one **authentication method** enabled for the project (email OTP, social, etc.)
- An **ASP.NET host** to run the callback endpoint (your API, an Azure Function, or a dedicated service)

## 1. Pick the Route and Generate a Shared Secret

The provisioning endpoint authenticates Descope by comparing a shared secret against a header
on the inbound request. Pick both values before configuring Descope or your app.

1. **Route** — pick an endpoint path. Default: `/auth/descope/provision`
2. **Shared secret** — generate a long random value. For example:

   ```powershell
   [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 } | ForEach-Object { [byte]$_ }))
   ```

   Store the secret in your app's secret store (user-secrets, Key Vault, etc.). You will
   paste the same value into the Descope HTTP Connector in a later step.

## 2. Create the HTTP Connector in Descope

The **HTTP Connector** is the Descope object that knows how to call your endpoint. It holds
the base URL, the auth header, and (optionally) a fixed body template.

1. In the Descope Console, open **Connectors** (under **Flows** or **Integrations**,
   depending on console version)
2. Click **Add Connector** and choose **HTTP**
3. Configure:
   - **Name:** e.g. `Cirreum User Provisioning`
   - **Base URL:** the public URL of your host, for example `https://api.yourapp.com`
   - **Authentication:** choose the scheme that matches your app's configuration.
     The defaults shipped by Cirreum.Identity.Descope expect:

     | Descope field | Value |
     |---|---|
     | Auth type | Bearer Token (or Authorization header) |
     | Header name | `Authorization` |
     | Header value | `Bearer <shared-secret-from-step-1>` |

     If you prefer an API-key style header, set `AuthorizationHeaderName` / `AuthorizationScheme`
     in `appsettings.json` to match (see *Configuration reference* below).

   - **Request method:** `POST`
4. Save the connector

## 3. Add an HTTP Connector Action to Your Flow

Descope flows are composed of **Actions**. Add one that calls the connector before the
session token is issued.

1. Open the **Flow** that handles sign-up / sign-in for your users
2. Insert an **HTTP Connector** action at the point where you want provisioning to run —
   typically **after authentication succeeds** and **before the final "success" step**
3. Configure the action:
   - **Connector:** the connector created in Step 2
   - **Endpoint / Path:** the `Route` value from Step 1 (e.g. `/auth/descope/provision`)
   - **Body:** use the canonical mapping below

   ```json
   {
     "externalUserId": "{{user.userId}}",
     "email": "{{user.email}}",
     "correlationId": "{{flow.executionId}}",
     "clientAppId": "{{project.id}}"
   }
   ```

   > The exact template-variable syntax depends on your Descope console version. Use
   > whatever Descope expression resolves to the user ID, email, flow run id, and
   > project/app id. The field **names** (left side) must match the canonical names above —
   > they are what the endpoint deserialises.

4. **Save** the action

## 4. Branch on the Connector Response

The endpoint replies with this shape on success:

```json
{ "allowed": true, "roles": ["app:user"], "correlationId": "..." }
```

and with HTTP 403 + the following body on denial:

```json
{ "allowed": false, "roles": [], "correlationId": "..." }
```

Configure the flow so that:

1. When **`allowed` is false** (or the HTTP status is not 2xx) — **fail the flow**.
   The user is not granted a session.
2. When **`allowed` is true** — continue. Use the returned `roles` either by:
   - Storing them on the user's `customAttributes.roles`, or
   - Consuming them directly in a later action that assigns Descope roles/permissions.

Then include `customAttributes.roles` (or whatever field you picked) in the **JWT template**
for the flow's session token. This is what the rest of your app will read via
`[Authorize(Roles = "...")]`.

## 5. Application Configuration

Add the following to your `appsettings.json`:

```json
{
  "Cirreum": {
    "Identity": {
      "Descope": {
        "Route": "/auth/descope/provision",
        "SharedSecret": "<paste-the-value-from-step-1>",
        "AuthorizationHeaderName": "Authorization",
        "AuthorizationScheme": "Bearer",
        "AllowedAppIds": "<descope-project-id>"
      }
    }
  }
}
```

Do **not** commit the real shared secret. Use user-secrets locally and Key Vault / App
Configuration / an equivalent secret store in production.

### Configuration reference

| Property | Required | Description |
|---|---|---|
| `Route` | No | Endpoint path. Defaults to `/auth/descope/provision`. Must match the path configured on the Descope Connector action. |
| `SharedSecret` | Yes | The secret value Descope attaches to every connector call. Must exactly match the value entered in the Descope Console. |
| `AuthorizationHeaderName` | No | Header Descope uses to transmit the secret. Defaults to `Authorization`. Override if you configured a custom header (e.g. `X-API-Key`). |
| `AuthorizationScheme` | No | Scheme prefix before the secret. Defaults to `Bearer`. Set to an empty string to compare the header value directly (API-key style). |
| `AllowedAppIds` | No | Comma- or semicolon-separated list of client application IDs allowed to call this endpoint. Leave empty to disable app-ID enforcement. |

### Multiple allowed client apps

If multiple Descope projects share the same provisioning endpoint, list all their project
IDs:

```json
"AllowedAppIds": "project-id-1,project-id-2,project-id-3"
```

## 6. Application Code

### Register services and map the endpoint

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddDescopeProvisioning<AppUserProvisioner>();

var app = builder.Build();

app.MapDescopeProvisioning();

app.Run();
```

### Implement the provisioner

Inherit from `UserProvisionerBase<TUser>` for the standard invitation-redemption flow:

```csharp
using Cirreum.Identity;

public sealed class AppUser : IProvisionedUser {
    public string ExternalUserId { get; init; } = "";
    public string Email { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = [];
}
```

> **Cirreum framework users:** If your application uses the full Cirreum runtime
> (`Cirreum.Runtime.Wasm`, etc.), also implement `IApplicationUser` on your user class
> so it participates in the Cirreum user-state pipeline.
>
> ```csharp
> public sealed class AppUser : IProvisionedUser, IApplicationUser { ... }
> ```

```csharp
public sealed class AppUserProvisioner(AppDbContext db)
    : UserProvisionerBase<AppUser> {

    protected override Task<AppUser?> FindUserAsync(
        string externalUserId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalUserId, ct);

    protected override async Task<AppUser?> RedeemInvitationAsync(
        string email, string externalUserId, CancellationToken ct) {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(
                i => i.Email == email.ToLowerInvariant()
                  && i.ClaimedAt == null
                  && i.ExpiresAt > DateTimeOffset.UtcNow,
                ct);
        if (invitation is null) return null;

        invitation.ClaimedAt = DateTimeOffset.UtcNow;
        invitation.ClaimedByExternalUserId = externalUserId;
        var user = new AppUser {
            ExternalUserId = externalUserId,
            Email = email,
            Roles = [invitation.Role]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }
}
```

### Multiple identity providers in one host

If your host also serves provisioning for another Cirreum identity provider package
(for example `Cirreum.Identity.EntraExternalId`), both packages register
`IUserProvisioner` as a **keyed** service — Descope uses `DescopeSource.Name`, Entra uses
`EntraExternalIdSource.Name`. You can register a different provisioner per source:

```csharp
builder.AddEntraExternalId<EntraUserProvisioner>();
builder.AddDescopeProvisioning<DescopeUserProvisioner>();
```

…or register the same type against both and branch on `context.Source` inside:

```csharp
public sealed class AppUserProvisioner : IUserProvisioner {
    public Task<ProvisionResult> ProvisionAsync(ProvisionContext context, CancellationToken ct) =>
        context.Source switch {
            DescopeSource.Name         => HandleDescope(context, ct),
            EntraExternalIdSource.Name => HandleEntra(context, ct),
            _                          => Task.FromResult(ProvisionResult.Deny())
        };
    // ...
}
```

Or implement `IUserProvisioner` directly for custom provisioning logic:

```csharp
using Cirreum.Identity;

public sealed class CustomProvisioner(AppDbContext db) : IUserProvisioner {

    public async Task<ProvisionResult> ProvisionAsync(
        ProvisionContext context, CancellationToken ct) {

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.ExternalUserId == context.ExternalUserId, ct);

        if (user is not null) {
            return ProvisionResult.Allow(user.Roles.ToArray());
        }

        // Auto-provision on first sign-in (no invitation required)
        var newUser = new AppUser {
            ExternalUserId = context.ExternalUserId,
            Email = context.Email,
            Roles = ["app:user"]
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);
        return ProvisionResult.Allow("app:user");
    }
}
```

## 7. Local Development

### Expose your local endpoint

Descope calls your endpoint over HTTPS from the Descope cloud. For local development, use
a tunnel:

**Using Visual Studio Dev Tunnels:**
1. In Visual Studio, go to **Tools** > **Options** > **Dev Tunnels** > **Enable Dev Tunnels**
2. Create a persistent tunnel via **View** > **Other Windows** > **Dev Tunnels**
3. Set the tunnel URL as the **Base URL** on the Descope HTTP Connector

**Using ngrok:**
```bash
ngrok http https://localhost:5001
```
Use the HTTPS forwarding URL as the Base URL in the Descope Console.

### Update the Base URL

After creating your tunnel, update the HTTP Connector's **Base URL** in the Descope Console
to point to your tunnel URL:

```
https://{tunnel-id}.devtunnels.ms
```

> **Tip:** Create a separate HTTP Connector for development so production configuration is
> unaffected.

### Testing the endpoint directly

You can test the endpoint with any HTTP client:

```bash
curl -X POST https://localhost:5001/auth/descope/provision \
  -H "Authorization: Bearer <your-shared-secret>" \
  -H "Content-Type: application/json" \
  -d '{
    "externalUserId": "test-user-1",
    "email": "test@example.com",
    "correlationId": "local-test-1",
    "clientAppId": "<descope-project-id>"
  }'
```

You should see:

- **`200 OK`** with `{ "allowed": true, "roles": [...], "correlationId": "..." }` on success
- **`403 Forbidden`** with `{ "allowed": false, ... }` when the provisioner denies the user
- **`401 Unauthorized`** if the shared secret doesn't match
- **`400 Bad Request`** if the payload is malformed or required fields are missing

For integration testing, use the `IServiceCollection` overload to wire up the services with
a test configuration and mock provisioner:

```csharp
services.AddDescopeProvisioning<TestProvisioner>(configuration);
```

## Troubleshooting

### 401 Unauthorized

- **Shared secret mismatch.** Confirm the value configured on the Descope HTTP Connector
  matches `DescopeOptions.SharedSecret` exactly (no leading/trailing whitespace).
- **Header name mismatch.** If you changed `AuthorizationHeaderName` in config, the Descope
  Connector must send the secret on that same header.
- **Scheme mismatch.** With `AuthorizationScheme = "Bearer"` (the default), Descope must
  send `Bearer <secret>`. Configure the Descope Connector as *Bearer Token* rather than
  *Custom Header* unless you also change `AuthorizationScheme`.

### 403 Forbidden — App not allowed

The `AllowedAppIds` list does not include the value Descope is sending in the `clientAppId`
request field. Options:

1. Add the Descope Project ID (or whatever value your flow puts in `clientAppId`) to
   `AllowedAppIds`, **or**
2. Leave `AllowedAppIds` empty to disable app-ID enforcement.

### 400 Bad Request

- **Missing required fields.** The endpoint requires non-empty `externalUserId` and
  `correlationId`. Check the body template on the HTTP Connector action.
- **Malformed JSON.** Descope's body template must produce valid JSON. If you reference a
  flow variable that is null, verify the mapping.

### Provisioner returns Denied but user should be allowed

- Check your database — does the user record exist with the correct `ExternalUserId`?
- If using invitations, verify the invitation is not expired and has not already been claimed.
- Check the `Email` field — if the identity provider doesn't supply an email, invitation
  redemption is skipped and the user is denied (when using `UserProvisionerBase`).

### Roles don't appear in the issued session token

1. Verify the flow's **HTTP Connector action** is invoked before the session is minted.
2. Verify the flow step that consumes the connector response actually writes the roles onto
   the user's `customAttributes` (or wherever your JWT template reads them from).
3. Verify the **JWT template** for the flow's session token includes the custom attribute
   holding the roles.
4. Clear the browser cache / use an incognito window — cached tokens won't have the new
   claims.
