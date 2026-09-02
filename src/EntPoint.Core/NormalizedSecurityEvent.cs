namespace EntPoint.Core
{
	public sealed record NormalizedSecurityEvent(
		string Timestamp,
		Guid EndpointId,
		EndpointOperatingSystem OperatingSystem,
		string EventType,
		string UserId,
		string ProcessName,
		int Pid,
		int Ppid,
		bool IsAlert,
		string? FilePath = null,
		int? AlertScore = null,
		string? AlertReason = null);
}
