using System.Reflection;
using EntPoint.Api.Controllers;
using EntPoint.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class ApiSecurityTests
	{
		[Theory]
		[InlineData(ApiKeyDefaults.AnalystKey, "analyst", ApiRoles.Analyst)]
		[InlineData(ApiKeyDefaults.AdminKey, "admin", ApiRoles.Admin)]
		public void Validate_AcceptsKnownKeys(
			string suppliedKey,
			string expectedName,
			string expectedRole)
		{
			AuthenticatedApiKey? apiKey = ApiKeyValidator.Validate(suppliedKey);

			Assert.NotNull(apiKey);
			Assert.Equal(expectedName, apiKey.Name);
			Assert.Equal(expectedRole, apiKey.Role);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("invalid")]
		public void Validate_RejectsUnknownKeys(string? suppliedKey)
		{
			Assert.Null(ApiKeyValidator.Validate(suppliedKey));
		}

		[Theory]
		[InlineData(typeof(EndpointsController), ApiAuthorizationPolicies.AnalystOrAdmin)]
		[InlineData(typeof(SummaryController), ApiAuthorizationPolicies.AnalystOrAdmin)]
		[InlineData(typeof(AlertsController), ApiAuthorizationPolicies.AdminOnly)]
		public void Controllers_RequireExpectedPolicy(Type controllerType, string expectedPolicy)
		{
			AuthorizeAttribute authorization = Assert.Single(
				controllerType.GetCustomAttributes<AuthorizeAttribute>());

			Assert.Equal(expectedPolicy, authorization.Policy);
		}

		[Fact]
		public void AllApiControllersRequireAuthorization()
		{
			Type controllerBaseType = typeof(Microsoft.AspNetCore.Mvc.ControllerBase);
			Type[] controllerTypes = typeof(EndpointsController).Assembly
				.GetTypes()
				.Where(type =>
					!type.IsAbstract &&
					controllerBaseType.IsAssignableFrom(type))
				.ToArray();

			Assert.NotEmpty(controllerTypes);
			Assert.All(
				controllerTypes,
				controllerType => Assert.NotEmpty(
					controllerType.GetCustomAttributes<AuthorizeAttribute>()));
		}
	}
}
