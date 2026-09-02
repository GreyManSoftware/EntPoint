namespace EntPoint.Core
{
	public sealed record RawSecurityEvent(
		string Timestamp,
		Guid EndpointId,
		EndpointOperatingSystem OperatingSystem,
		string EventType,
		string UserId,
		string ProcessName,
		int Pid,
		int Ppid,
		string? FilePath = null,
		int? AlertScore = null,
		string? AlertReason = null);
}
