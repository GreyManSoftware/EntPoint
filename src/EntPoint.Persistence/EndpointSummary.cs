namespace EntPoint.Persistence
{
	public sealed record EndpointSummary(
		Guid EndpointId,
		long TotalEvents,
		string MostFrequentProcess,
		IReadOnlyDictionary<string, long> EventTypeCounts);
}
