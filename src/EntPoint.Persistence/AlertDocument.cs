using System.Globalization;
using EntPoint.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EntPoint.Persistence
{
	public sealed class AlertDocument
	{
		[BsonId]
		public ObjectId Id { get; init; } = ObjectId.GenerateNewId();

		[BsonElement("timestamp")]
		[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
		public DateTime Timestamp { get; init; }

		[BsonElement("endpoint_id")]
		[BsonGuidRepresentation(GuidRepresentation.Standard)]
		public Guid EndpointId { get; init; }

		[BsonElement("operating_system")]
		public required string OperatingSystem { get; init; }

		[BsonElement("event_type")]
		public required string EventType { get; init; }

		[BsonElement("user_id")]
		public required string UserId { get; init; }

		[BsonElement("process_name")]
		public required string ProcessName { get; init; }

		[BsonElement("pid")]
		public int Pid { get; init; }

		[BsonElement("ppid")]
		public int Ppid { get; init; }

		[BsonElement("is_alert")]
		public bool IsAlert { get; init; }

		[BsonElement("file_path")]
		[BsonIgnoreIfNull]
		public string? FilePath { get; init; }

		[BsonElement("alert_score")]
		public int AlertScore { get; init; }

		[BsonElement("alert_reason")]
		public required string AlertReason { get; init; }

		public static AlertDocument FromEvent(NormalizedSecurityEvent securityEvent)
		{
			if (!securityEvent.IsAlert ||
				!securityEvent.AlertScore.HasValue ||
				string.IsNullOrWhiteSpace(securityEvent.AlertReason))
			{
				throw new ArgumentException(
					"MongoDB accepts only complete alert events.",
					nameof(securityEvent));
			}

			DateTimeOffset timestamp = DateTimeOffset.Parse(
				securityEvent.Timestamp,
				CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind);

			return new AlertDocument
			{
				Timestamp = timestamp.UtcDateTime,
				EndpointId = securityEvent.EndpointId,
				OperatingSystem = securityEvent.OperatingSystem
					.ToString()
					.ToLowerInvariant(),
				EventType = securityEvent.EventType,
				UserId = securityEvent.UserId,
				ProcessName = securityEvent.ProcessName,
				Pid = securityEvent.Pid,
				Ppid = securityEvent.Ppid,
				IsAlert = true,
				FilePath = securityEvent.FilePath,
				AlertScore = securityEvent.AlertScore.Value,
				AlertReason = securityEvent.AlertReason
			};
		}
	}
}
