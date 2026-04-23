# Cirreum.Identity.Descope

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Identity.Descope.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Descope/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Identity.Descope.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Descope/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Identity.Descope?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Identity.Descope/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Identity.Descope?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Identity.Descope/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**User provisioning for Descope HTTP Connector callbacks.**

## Overview

**Cirreum.Identity.Descope** hosts the HTTP endpoint that a Descope Flow calls through an
**HTTP Connector** action before issuing a session token. When a user signs in, the flow
posts user context to your endpoint — this library validates the call, provisions the user,
and returns roles that the flow embeds on the user record and/or in the issued JWT.

The library is framework-independent and can run in any ASP.NET host: your main API, an Azure
Function, or a dedicated service.

### How it works

1. **Authenticates** the inbound call by comparing a shared secret against the header
   Descope attaches to every connector request
2. **Provisions** the user via your `IUserProvisioner` implementation
3. **Returns** a decision (`allowed` + `roles`) the Descope flow consumes via
   `connector.response.*`

### Installation

```
dotnet add package Cirreum.Identity.Descope
```

This package depends transitively on
[Cirreum.Identity.Abstractions](https://github.com/cirreum/Cirreum.Identity.Abstractions),
which defines the provider-agnostic contracts (`IUserProvisioner`, `ProvisionContext`,
`ProvisionResult`, `IProvisionedUser`, `IPendingInvitation`, `UserProvisionerBase<TUser>`).

### Quick start

```csharp
// Program.cs
builder.AddDescopeProvisioning<AppUserProvisioner>();

var app = builder.Build();
app.MapDescopeProvisioning();
```

### Implement the provisioner

```csharp
using Cirreum.Identity;

public sealed class AppUserProvisioner(AppDbContext db)
    : UserProvisionerBase<AppUser> {

    protected override Task<AppUser?> FindUserAsync(
        string externalUserId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalUserId, ct);

    protected override async Task<AppUser?> RedeemInvitationAsync(
        string email, string externalUserId, CancellationToken ct) {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Email == email && !i.IsRedeemed, ct);
        if (invitation is null || invitation.IsExpired) return null;

        var user = new AppUser {
            ExternalUserId = externalUserId,
            Email = email,
            Roles = [invitation.Role]
        };
        db.Users.Add(user);
        invitation.IsRedeemed = true;
        await db.SaveChangesAsync(ct);
        return user;
    }
}
```

> **Cirreum framework users:** If your app uses the full Cirreum runtime, also implement `IApplicationUser` on your user class so it integrates with `IUserState` and the authentication post-processor pipeline.

### Configuration

```json
{
  "Cirreum": {
    "Identity": {
      "Descope": {
        "Route": "/auth/descope/provision",
        "SharedSecret": "<long-random-secret-matching-the-descope-connector>",
        "AuthorizationHeaderName": "Authorization",
        "AuthorizationScheme": "Bearer",
        "AllowedAppIds": "<descope-project-id-or-app-id>"
      }
    }
  }
}
```

## Documentation

See [SETUP.md](SETUP.md) for Descope Console configuration, flow/connector wiring, and local
development instructions.

## Relationship to other Cirreum.Identity packages

This package, together with
[Cirreum.Identity.EntraExternalId](https://github.com/cirreum/Cirreum.Identity.EntraExternalId),
implements the provider-specific half of Cirreum's identity integration. Both packages
depend on [Cirreum.Identity.Abstractions](https://github.com/cirreum/Cirreum.Identity.Abstractions)
so an application can swap providers without rewriting its provisioner.

## Contribution Guidelines

1. **Be conservative with new abstractions**
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Identity.Descope follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
