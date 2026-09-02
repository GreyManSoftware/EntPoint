using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace EntPoint.Api.Security
{
	internal sealed class ApiKeyAuthenticationHandler
		: AuthenticationHandler<AuthenticationSchemeOptions>
	{
		public ApiKeyAuthenticationHandler(
			IOptionsMonitor<AuthenticationSchemeOptions> options,
			ILoggerFactory logger,
			UrlEncoder encoder)
			: base(options, logger, encoder)
		{
		}

		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			if (!Request.Headers.TryGetValue(
					ApiKeyDefaults.HeaderName,
					out StringValues headerValues))
			{
				return Task.FromResult(AuthenticateResult.NoResult());
			}

			if (headerValues.Count != 1)
			{
				return Task.FromResult(
					AuthenticateResult.Fail("Exactly one API key must be supplied."));
			}

			AuthenticatedApiKey? apiKey = ApiKeyValidator.Validate(headerValues[0]);
			if (apiKey is null)
			{
				return Task.FromResult(AuthenticateResult.Fail("The API key is invalid."));
			}

			Claim[] claims =
			[
				new Claim(ClaimTypes.NameIdentifier, apiKey.Name),
				new Claim(ClaimTypes.Name, apiKey.Name),
				new Claim(ClaimTypes.Role, apiKey.Role)
			];
			ClaimsIdentity identity = new ClaimsIdentity(claims, ApiKeyDefaults.Scheme);
			ClaimsPrincipal principal = new ClaimsPrincipal(identity);
			AuthenticationTicket ticket = new AuthenticationTicket(
				principal,
				ApiKeyDefaults.Scheme);

			return Task.FromResult(AuthenticateResult.Success(ticket));
		}

		protected override async Task HandleChallengeAsync(
			AuthenticationProperties properties)
		{
			Response.StatusCode = StatusCodes.Status401Unauthorized;
			await Response.WriteAsJsonAsync(
				new ProblemDetails
				{
					Status = StatusCodes.Status401Unauthorized,
					Title = "Unauthorized",
					Detail = $"A valid {ApiKeyDefaults.HeaderName} header is required."
				},
				cancellationToken: Context.RequestAborted);
		}

		protected override async Task HandleForbiddenAsync(
			AuthenticationProperties properties)
		{
			Response.StatusCode = StatusCodes.Status403Forbidden;
			await Response.WriteAsJsonAsync(
				new ProblemDetails
				{
					Status = StatusCodes.Status403Forbidden,
					Title = "Forbidden",
					Detail = "The authenticated role cannot access this endpoint."
				},
				cancellationToken: Context.RequestAborted);
		}
	}
}
