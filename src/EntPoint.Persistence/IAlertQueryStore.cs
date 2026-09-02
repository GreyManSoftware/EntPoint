namespace EntPoint.Persistence
{
	public interface IAlertQueryStore
	{
		Task<IReadOnlyList<AlertDocument>> GetAlertsAsync(
			Guid? endpointId,
			int? minimumScore,
			CancellationToken cancellationToken);
	}
}
