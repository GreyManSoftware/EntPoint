using System.Text.Json;
using System.Text.Json.Serialization;
using EntPoint.Api.Security;
using EntPoint.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace EntPoint.Api
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			string postgresConnectionString = GetRequiredConfiguration(
				builder.Configuration,
				"ENTPOINT_POSTGRES");
			string mongoConnectionString = GetRequiredConfiguration(
				builder.Configuration,
				"ENTPOINT_MONGO");
			string mongoDatabaseName = GetRequiredConfiguration(
				builder.Configuration,
				"ENTPOINT_MONGO_DATABASE");

			builder.Services
				.AddControllers()
				.AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.PropertyNamingPolicy =
						JsonNamingPolicy.SnakeCaseLower;
					options.JsonSerializerOptions.Converters.Add(
						new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
				});
			builder.Services.AddProblemDetails();
			builder.Services
				.AddAuthentication(ApiKeyDefaults.Scheme)
				.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
					ApiKeyDefaults.Scheme,
					options => { });
			AuthorizationBuilder authorization = builder.Services.AddAuthorizationBuilder();
			authorization.SetFallbackPolicy(
				new AuthorizationPolicyBuilder(
					ApiKeyDefaults.Scheme)
					.RequireAuthenticatedUser()
					.Build());
			authorization.AddPolicy(
				ApiAuthorizationPolicies.AnalystOrAdmin,
				policy => policy.RequireRole(ApiRoles.Analyst, ApiRoles.Admin));
			authorization.AddPolicy(
				ApiAuthorizationPolicies.AdminOnly,
				policy => policy.RequireRole(ApiRoles.Admin));
			builder.Services.AddSingleton<IEventQueryStore>(
				new PostgresEventStore(postgresConnectionString));
			builder.Services.AddSingleton<IAlertQueryStore>(
				new MongoAlertStore(mongoConnectionString, mongoDatabaseName));

			WebApplication app = builder.Build();
			app.UseExceptionHandler();
			app.UseDefaultFiles();
			app.UseStaticFiles();
			app.UseAuthentication();
			app.UseAuthorization();
			app.MapControllers();
			app.Run();
		}

		private static string GetRequiredConfiguration(
			IConfiguration configuration,
			string name)
		{
			string? value = configuration[name];
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidOperationException(
					$"Required configuration value '{name}' is missing.");
			}

			return value;
		}
	}
}
