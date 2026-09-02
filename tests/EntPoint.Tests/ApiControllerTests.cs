using EntPoint.Api.Controllers;
using EntPoint.Api.Models;
using EntPoint.Core;
using EntPoint.Persistence;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class ApiControllerTests
	{
		private static readonly Guid EndpointId =
			Guid.Parse("3ee9070e-bdb0-41c7-b8e1-86b1aab86aa3");

		[Fact]
		public async Task Summary_ReturnsBadRequestForInvalidEndpoint()
		{
			FakeEventQueryStore store = new FakeEventQueryStore();
			SummaryController controller = new SummaryController(store);

			ActionResult<EndpointSummaryResponse> result = await controller.Get(
				"invalid",
				CancellationToken.None);

			ObjectResult problem = Assert.IsType<ObjectResult>(result.Result);
			Assert.Equal(400, problem.StatusCode);
		}

		[Fact]
		public async Task Summary_ReturnsNotFoundForMissingEndpoint()
		{
			FakeEventQueryStore store = new FakeEventQueryStore();
			SummaryController controller = new SummaryController(store);

			ActionResult<EndpointSummaryResponse> result = await controller.Get(
				EndpointId.ToString(),
				CancellationToken.None);

			ObjectResult problem = Assert.IsType<ObjectResult>(result.Result);
			Assert.Equal(404, problem.StatusCode);
		}

		[Fact]
		public async Task Summary_ReturnsStoredSummary()
		{
			EndpointSummary summary = new EndpointSummary(
				EndpointId,
				25,
				"bash",
				new Dictionary<string, long> { [SecurityEventTypes.FileRead] = 25 });
			FakeEventQueryStore store = new FakeEventQueryStore(summary: summary);
			SummaryController controller = new SummaryController(store);

			ActionResult<EndpointSummaryResponse> result = await controller.Get(
				EndpointId.ToString(),
				CancellationToken.None);

			OkObjectResult response = Assert.IsType<OkObjectResult>(result.Result);
			EndpointSummaryResponse body =
				Assert.IsType<EndpointSummaryResponse>(response.Value);
			Assert.Equal(25, body.TotalEvents);
			Assert.Equal("bash", body.MostFrequentProcess);
		}

		[Fact]
		public async Task Alerts_ReturnsBadRequestForInvalidFilters()
		{
			FakeAlertQueryStore store = new FakeAlertQueryStore([]);
			AlertsController controller = new AlertsController(store);

			ActionResult<IReadOnlyList<AlertResponse>> endpointResult = await controller.Get(
				"invalid",
				null,
				CancellationToken.None);
			ActionResult<IReadOnlyList<AlertResponse>> scoreResult = await controller.Get(
				null,
				101,
				CancellationToken.None);

			Assert.Equal(
				400,
				Assert.IsType<ObjectResult>(endpointResult.Result).StatusCode);
			Assert.Equal(
				400,
				Assert.IsType<ObjectResult>(scoreResult.Result).StatusCode);
		}

		[Fact]
		public async Task Alerts_PassesFiltersToStore()
		{
			AlertDocument alert = CreateAlert();
			FakeAlertQueryStore store = new FakeAlertQueryStore([alert]);
			AlertsController controller = new AlertsController(store);

			ActionResult<IReadOnlyList<AlertResponse>> result = await controller.Get(
				EndpointId.ToString(),
				70,
				CancellationToken.None);

			OkObjectResult response = Assert.IsType<OkObjectResult>(result.Result);
			IReadOnlyList<AlertResponse> body =
				Assert.IsAssignableFrom<IReadOnlyList<AlertResponse>>(response.Value);
			Assert.Single(body);
			Assert.Equal(EndpointId, store.EndpointId);
			Assert.Equal(70, store.MinimumScore);
		}

		[Fact]
		public async Task Endpoints_ReturnsKnownEndpoints()
		{
			EndpointDescriptor endpoint = new EndpointDescriptor(
				EndpointId,
				EndpointOperatingSystem.Linux);
			FakeEventQueryStore store = new FakeEventQueryStore(endpoints: [endpoint]);
			EndpointsController controller = new EndpointsController(store);

			ActionResult<IReadOnlyList<EndpointResponse>> result =
				await controller.Get(CancellationToken.None);

			OkObjectResult response = Assert.IsType<OkObjectResult>(result.Result);
			IReadOnlyList<EndpointResponse> body =
				Assert.IsAssignableFrom<IReadOnlyList<EndpointResponse>>(response.Value);
			Assert.Single(body);
			Assert.Equal(EndpointId, body[0].EndpointId);
		}

		private static AlertDocument CreateAlert() =>
			new AlertDocument
			{
				Timestamp = DateTime.UtcNow,
				EndpointId = EndpointId,
				OperatingSystem = "linux",
				EventType = SecurityEventTypes.FileRead,
				UserId = "alex",
				ProcessName = "bash",
				Pid = 1200,
				Ppid = 1000,
				IsAlert = true,
				FilePath = "/etc/shadow",
				AlertScore = 90,
				AlertReason = "Suspicious file read"
			};

		private sealed class FakeEventQueryStore : IEventQueryStore
		{
			private readonly EndpointSummary? _summary;
			private readonly IReadOnlyList<EndpointDescriptor> _endpoints;

			public FakeEventQueryStore(
				EndpointSummary? summary = null,
				IReadOnlyList<EndpointDescriptor>? endpoints = null)
			{
				_summary = summary;
				_endpoints = endpoints ?? [];
			}

			public Task<IReadOnlyList<EndpointDescriptor>> GetEndpointsAsync(
				CancellationToken cancellationToken) =>
				Task.FromResult(_endpoints);

			public Task<EndpointSummary?> GetSummaryAsync(
				Guid endpointId,
				CancellationToken cancellationToken) =>
				Task.FromResult(_summary);
		}

		private sealed class FakeAlertQueryStore : IAlertQueryStore
		{
			private readonly IReadOnlyList<AlertDocument> _alerts;

			public FakeAlertQueryStore(IReadOnlyList<AlertDocument> alerts)
			{
				_alerts = alerts;
			}

			public Guid? EndpointId { get; private set; }
			public int? MinimumScore { get; private set; }
			public Task<IReadOnlyList<AlertDocument>> GetAlertsAsync(
				Guid? endpointId,
				int? minimumScore,
				CancellationToken cancellationToken)
			{
				EndpointId = endpointId;
				MinimumScore = minimumScore;
				return Task.FromResult(_alerts);
			}
		}
	}
}
