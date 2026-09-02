using EntPoint.Api.Models;
using EntPoint.Api.Security;
using EntPoint.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EntPoint.Api.Controllers
{
	[ApiController]
	[Authorize(Policy = ApiAuthorizationPolicies.AnalystOrAdmin)]
	[Route("api/v1/summary")]
	public sealed class SummaryController : ControllerBase
	{
		private readonly IEventQueryStore _eventStore;

		public SummaryController(IEventQueryStore eventStore)
		{
			_eventStore = eventStore;
		}

		[HttpGet("{endpointId}")]
		public async Task<ActionResult<EndpointSummaryResponse>> Get(
			string endpointId,
			CancellationToken cancellationToken)
		{
			if (!Guid.TryParse(endpointId, out Guid parsedEndpointId))
			{
				return Problem(
					statusCode: StatusCodes.Status400BadRequest,
					title: "Invalid endpoint ID",
					detail: "The endpoint ID must be a valid UUID.");
			}

			EndpointSummary? summary = await _eventStore.GetSummaryAsync(
				parsedEndpointId,
				cancellationToken);
			if (summary is null)
			{
				return Problem(
					statusCode: StatusCodes.Status404NotFound,
					title: "Endpoint not found",
					detail: $"No events were found for endpoint '{parsedEndpointId}'.");
			}

			return Ok(EndpointSummaryResponse.FromSummary(summary));
		}
	}
}
