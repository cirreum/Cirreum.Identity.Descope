namespace Cirreum.Identity.Descope;

using Cirreum.Identity.Descope.Models;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(DescopeProvisionRequest))]
[JsonSerializable(typeof(DescopeProvisionResponse))]
internal sealed partial class DescopeJsonContext : JsonSerializerContext {
}
