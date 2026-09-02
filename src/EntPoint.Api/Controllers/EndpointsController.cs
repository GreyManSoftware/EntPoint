using EntPoint.Api.Models;
using EntPoint.Api.Security;
using EntPoint.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EntPoint.Api.Controllers
{
	[ApiController]
	[Authorize(Policy = ApiAuthorizationPolicies.AnalystOrAdmin)]
	[Route("api/v1/endpoints")]
	public sealed class EndpointsController : ControllerBase
	{
		private readonly IEventQueryStore _eventStore;

		public EndpointsController(IEventQueryStore eventStore)
		{
			_eventStore = eventStore;
		}

		[HttpGet]
		public async Task<ActionResult<IReadOnlyList<EndpointResponse>>> Get(
			CancellationToken cancellationToken)
		{
			IReadOnlyList<EndpointDescriptor> endpoints =
				await _eventStore.GetEndpointsAsync(cancellationToken);
			IReadOnlyList<EndpointResponse> response = endpoints
				.Select(EndpointResponse.FromDescriptor)
				.ToArray();

			return Ok(response);
		}
	}
}
