using EntPoint.Persistence;

namespace EntPoint.Api.Models
{
	public sealed record EndpointSummaryResponse(
		Guid EndpointId,
		long TotalEvents,
		string MostFrequentProcess,
		IReadOnlyDictionary<string, long> EventTypeCounts)
	{
		public static EndpointSummaryResponse FromSummary(EndpointSummary summary) =>
			new EndpointSummaryResponse(
				summary.EndpointId,
				summary.TotalEvents,
				summary.MostFrequentProcess,
				summary.EventTypeCounts);
	}
}
