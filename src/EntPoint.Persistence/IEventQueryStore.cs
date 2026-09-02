namespace EntPoint.Persistence
{
	public interface IEventQueryStore
	{
		Task<IReadOnlyList<EndpointDescriptor>> GetEndpointsAsync(
			CancellationToken cancellationToken);

		Task<EndpointSummary?> GetSummaryAsync(
			Guid endpointId,
			CancellationToken cancellationToken);
	}
}
