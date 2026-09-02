using System.Globalization;

namespace EntPoint.Core
{
	public sealed class EventNormalizer
	{
		private static readonly HashSet<string> SupportedEventTypes =
		[
			SecurityEventTypes.ProcessSnapshot,
			SecurityEventTypes.ProcessStart,
			SecurityEventTypes.FileRead
		];

		private readonly HashSet<string> _processDenylist;

		public EventNormalizer(IEnumerable<string>? processDenylist = null)
		{
			_processDenylist = new HashSet<string>(
				processDenylist ?? ["system_idle_process", "svchost.exe", "kthreadd", "kworker"],
				StringComparer.OrdinalIgnoreCase);
		}

		public NormalizationResult Normalize(RawSecurityEvent rawEvent)
		{
			if (!DateTimeOffset.TryParse(
					rawEvent.Timestamp,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
					out DateTimeOffset timestamp))
			{
				return NormalizationResult.Rejected("Timestamp is not a valid ISO 8601 value.");
			}

			if (!SupportedEventTypes.Contains(rawEvent.EventType))
			{
				return NormalizationResult.Rejected($"Unsupported event type '{rawEvent.EventType}'.");
			}

			if (!Enum.IsDefined(rawEvent.OperatingSystem))
			{
				return NormalizationResult.Rejected("Operating system is not supported.");
			}

			if (string.IsNullOrWhiteSpace(rawEvent.UserId) ||
				string.IsNullOrWhiteSpace(rawEvent.ProcessName))
			{
				return NormalizationResult.Rejected("User ID and process name are required.");
			}

			if (rawEvent.Pid <= 0 || rawEvent.Ppid < 0)
			{
				return NormalizationResult.Rejected("PID must be positive and PPID cannot be negative.");
			}

			if (_processDenylist.Contains(rawEvent.ProcessName))
			{
				return NormalizationResult.Rejected("Process is on the denylist.");
			}

			if (rawEvent.EventType == SecurityEventTypes.FileRead &&
				string.IsNullOrWhiteSpace(rawEvent.FilePath))
			{
				return NormalizationResult.Rejected("File-read events require a file path.");
			}

			if (rawEvent.EventType != SecurityEventTypes.FileRead && rawEvent.FilePath is not null)
			{
				return NormalizationResult.Rejected("Only file-read events can include a file path.");
			}

			if (rawEvent.AlertScore is < 1 or > 100)
			{
				return NormalizationResult.Rejected("Alert score must be between 1 and 100.");
			}

			bool isAlert = rawEvent.AlertScore.HasValue;
			if (isAlert != !string.IsNullOrWhiteSpace(rawEvent.AlertReason))
			{
				return NormalizationResult.Rejected(
					"Alert score and alert reason must either both be present or both be absent.");
			}

			return NormalizationResult.Accepted(new NormalizedSecurityEvent(
				timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
				rawEvent.EndpointId,
				rawEvent.OperatingSystem,
				rawEvent.EventType,
				rawEvent.UserId.Trim(),
				rawEvent.ProcessName.Trim(),
				rawEvent.Pid,
				rawEvent.Ppid,
				isAlert,
				rawEvent.FilePath,
				rawEvent.AlertScore,
				rawEvent.AlertReason));
		}
	}
}
