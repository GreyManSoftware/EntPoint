using EntPoint.Persistence;

namespace EntPoint.Api.Models
{
	public sealed record AlertResponse(
		DateTime Timestamp,
		Guid EndpointId,
		string OperatingSystem,
		string EventType,
		string UserId,
		string ProcessName,
		int Pid,
		int Ppid,
		bool IsAlert,
		string? FilePath,
		int AlertScore,
		string AlertReason)
	{
		public static AlertResponse FromDocument(AlertDocument document) =>
			new AlertResponse(
				document.Timestamp,
				document.EndpointId,
				document.OperatingSystem,
				document.EventType,
				document.UserId,
				document.ProcessName,
				document.Pid,
				document.Ppid,
				document.IsAlert,
				document.FilePath,
				document.AlertScore,
				document.AlertReason);
	}
}
