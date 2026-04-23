namespace Cirreum.Identity.Descope;

using Cirreum.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for registering the Descope HTTP Connector provisioning endpoint.
/// </summary>
/// <remarks>
/// See SETUP.md for full Descope Console configuration, appsettings.json reference,
/// and troubleshooting guidance.
/// </remarks>
public static class DescopeExtensions {

	// Sentinel registered alongside the provisioner so MapDescopeProvisioning can validate
	// at startup that a provisioner has been registered.
	private sealed class ProvisionerMarker { }

	// -------------------------------------------------------------------------
	// IHostApplicationBuilder overloads (primary — WebApplicationBuilder etc.)
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers Descope provisioning services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation that controls user access.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// Register as scoped to allow access to database contexts and other request-scoped services.
	/// </typeparam>
	/// <param name="builder">The application builder (<c>WebApplicationBuilder</c> etc.).</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to <c>"Cirreum:Identity:Descope"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IHostApplicationBuilder AddDescopeProvisioning<TProvisioner>(
		this IHostApplicationBuilder builder,
		string sectionName = "Cirreum:Identity:Descope")
		where TProvisioner : class, IUserProvisioner {
		builder.Services.AddDescopeProvisioning<TProvisioner>(builder.Configuration, sectionName);
		return builder;
	}

	/// <summary>
	/// Registers Descope provisioning services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation using a factory function.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// </typeparam>
	/// <param name="builder">The application builder (<c>WebApplicationBuilder</c> etc.).</param>
	/// <param name="factory">Factory function to create the provisioner instance.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to <c>"Cirreum:Identity:Descope"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IHostApplicationBuilder AddDescopeProvisioning<TProvisioner>(
		this IHostApplicationBuilder builder,
		Func<IServiceProvider, TProvisioner> factory,
		string sectionName = "Cirreum:Identity:Descope")
		where TProvisioner : class, IUserProvisioner {
		builder.Services.AddDescopeProvisioning(builder.Configuration, factory, sectionName);
		return builder;
	}

	// -------------------------------------------------------------------------
	// IServiceCollection overloads (for testing and advanced scenarios)
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers Descope provisioning services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation that controls user access.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// Register as scoped to allow access to database contexts and other request-scoped services.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration root.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to <c>"Cirreum:Identity:Descope"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IServiceCollection AddDescopeProvisioning<TProvisioner>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName = "Cirreum:Identity:Descope")
		where TProvisioner : class, IUserProvisioner {
		services.Configure<DescopeOptions>(configuration.GetSection(sectionName));
		services.AddSingleton<DescopeSharedSecretValidator>();
		services.AddScoped<DescopeConnectorHandler>();
		services.AddKeyedScoped<IUserProvisioner, TProvisioner>(DescopeSource.Name);
		services.AddSingleton<ProvisionerMarker>();
		return services;
	}

	/// <summary>
	/// Registers Descope provisioning services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation using a factory function.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration root.</param>
	/// <param name="factory">Factory function to create the provisioner instance.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to <c>"Cirreum:Identity:Descope"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IServiceCollection AddDescopeProvisioning<TProvisioner>(
		this IServiceCollection services,
		IConfiguration configuration,
		Func<IServiceProvider, TProvisioner> factory,
		string sectionName = "Cirreum:Identity:Descope")
		where TProvisioner : class, IUserProvisioner {
		services.Configure<DescopeOptions>(configuration.GetSection(sectionName));
		services.AddSingleton<DescopeSharedSecretValidator>();
		services.AddScoped<DescopeConnectorHandler>();
		services.AddKeyedScoped<IUserProvisioner>(DescopeSource.Name, (sp, _) => factory(sp));
		services.AddSingleton<ProvisionerMarker>();
		return services;
	}

	// -------------------------------------------------------------------------
	// Endpoint mapping
	// -------------------------------------------------------------------------

	/// <summary>
	/// Maps the anonymous Descope HTTP Connector provisioning endpoint.
	/// Route is configurable via <see cref="DescopeOptions.Route"/>.
	/// </summary>
	/// <remarks>
	/// Register this after <c>UseAuthentication</c> / <c>UseAuthorization</c>.
	/// The endpoint is registered as <c>AllowAnonymous</c> — authentication is performed
	/// internally by validating the Descope-supplied shared secret. See SETUP.md.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <c>AddDescopeProvisioning&lt;TProvisioner&gt;</c> has not been called.
	/// </exception>
	public static IEndpointRouteBuilder MapDescopeProvisioning(this IEndpointRouteBuilder app) {
		if (app.ServiceProvider.GetService<ProvisionerMarker>() is null) {
			throw new InvalidOperationException(
				"No IUserProvisioner has been registered for Descope. " +
				"Call builder.AddDescopeProvisioning<TProvisioner>() before calling app.MapDescopeProvisioning().");
		}

		var options = app.ServiceProvider.GetRequiredService<IOptions<DescopeOptions>>().Value;
		app.MapPost(options.Route, async (HttpRequest request, DescopeConnectorHandler handler, CancellationToken cancellationToken) =>
			await handler.HandleAsync(request, cancellationToken))
			.AllowAnonymous()
			.ExcludeFromDescription(); // Hide from OpenAPI/Swagger
		return app;
	}

}
