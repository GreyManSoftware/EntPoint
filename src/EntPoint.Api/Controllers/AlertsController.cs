using EntPoint.Api.Models;
using EntPoint.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace EntPoint.Api.Controllers
{
	[ApiController]
	[Route("api/v1/alerts")]
	public sealed class AlertsController : ControllerBase
	{
		private readonly IAlertQueryStore _alertStore;

		public AlertsController(IAlertQueryStore alertStore)
		{
			_alertStore = alertStore;
		}

		[HttpGet]
		public async Task<ActionResult<IReadOnlyList<AlertResponse>>> Get(
			[FromQuery(Name = "endpoint_id")] string? endpointId,
			[FromQuery(Name = "min_score")] int? minimumScore,
			CancellationToken cancellationToken)
		{
			Guid? parsedEndpointId = null;
			if (!string.IsNullOrWhiteSpace(endpointId))
			{
				if (!Guid.TryParse(endpointId, out Guid value))
				{
					return Problem(
						statusCode: StatusCodes.Status400BadRequest,
						title: "Invalid endpoint ID",
						detail: "The endpoint_id query parameter must be a valid UUID.");
				}

				parsedEndpointId = value;
			}

			if (minimumScore is < 1 or > 100)
			{
				return Problem(
					statusCode: StatusCodes.Status400BadRequest,
					title: "Invalid minimum score",
					detail: "The min_score query parameter must be between 1 and 100.");
			}

			IReadOnlyList<AlertDocument> alerts = await _alertStore.GetAlertsAsync(
				parsedEndpointId,
				minimumScore,
				cancellationToken);
			IReadOnlyList<AlertResponse> response = alerts
				.Select(AlertResponse.FromDocument)
				.ToArray();

			return Ok(response);
		}
	}
}
