namespace Cirreum.Identity.Descope;

/// <summary>
/// Well-known identifier for the Descope identity provider.
/// </summary>
/// <remarks>
/// Used in two places:
/// <list type="bullet">
///   <item><description>
///     As the value of <see cref="ProvisionContext.Source"/> when this package invokes
///     <see cref="IUserProvisioner"/>.
///   </description></item>
///   <item><description>
///     As the DI service key under which this package registers the application's
///     <see cref="IUserProvisioner"/> implementation, so an application consuming multiple
///     identity providers can register a distinct provisioner per source.
///   </description></item>
/// </list>
/// </remarks>
public static class DescopeSource {

	/// <summary>
	/// The canonical source identifier for Descope: <c>"Descope"</c>.
	/// </summary>
	public const string Name = "Descope";

}
