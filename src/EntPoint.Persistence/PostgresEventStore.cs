using System.Globalization;
using EntPoint.Core;
using Npgsql;
using NpgsqlTypes;

namespace EntPoint.Persistence
{
	public sealed class PostgresEventStore : IEventQueryStore
	{
		private const string SchemaSql =
			"""
			CREATE TABLE IF NOT EXISTS events
			(
				id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
				timestamp TIMESTAMPTZ NOT NULL,
				endpoint_id UUID NOT NULL,
				operating_system VARCHAR(10) NOT NULL,
				event_type VARCHAR(32) NOT NULL,
				user_id VARCHAR(128) NOT NULL,
				process_name VARCHAR(255) NOT NULL,
				pid INTEGER NOT NULL,
				ppid INTEGER NOT NULL,
				file_path TEXT NULL,

				CONSTRAINT ck_events_operating_system
					CHECK (operating_system IN ('windows', 'linux')),
				CONSTRAINT ck_events_event_type
					CHECK (event_type IN ('process_snapshot', 'process_start', 'file_read')),
				CONSTRAINT ck_events_process_ids
					CHECK (pid > 0 AND ppid >= 0),
				CONSTRAINT ck_events_file_path
					CHECK (
						(event_type = 'file_read' AND file_path IS NOT NULL)
						OR
						(event_type <> 'file_read' AND file_path IS NULL)
					)
			);

			CREATE INDEX IF NOT EXISTS ix_events_endpoint_timestamp
				ON events (endpoint_id, timestamp DESC);
			""";

		private const string InsertSql =
			"""
			INSERT INTO events
			(
				timestamp,
				endpoint_id,
				operating_system,
				event_type,
				user_id,
				process_name,
				pid,
				ppid,
				file_path
			)
			VALUES
			(
				@timestamp,
				@endpoint_id,
				@operating_system,
				@event_type,
				@user_id,
				@process_name,
				@pid,
				@ppid,
				@file_path
			);
			""";

		private readonly string _connectionString;

		public PostgresEventStore(string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new ArgumentException(
					"PostgreSQL connection string is required.",
					nameof(connectionString));
			}

			_connectionString = connectionString;
		}

		public async Task InitializeAsync(bool reset, CancellationToken cancellationToken)
		{
			await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			await using NpgsqlCommand schemaCommand = new NpgsqlCommand(SchemaSql, connection);
			await schemaCommand.ExecuteNonQueryAsync(cancellationToken);

			if (reset)
			{
				await using NpgsqlCommand resetCommand = new NpgsqlCommand(
					"TRUNCATE TABLE events RESTART IDENTITY;",
					connection);
				await resetCommand.ExecuteNonQueryAsync(cancellationToken);
			}
		}

		public async Task InsertAsync(
			IReadOnlyCollection<NormalizedSecurityEvent> events,
			CancellationToken cancellationToken)
		{
			if (events.Any(securityEvent => securityEvent.IsAlert))
			{
				throw new ArgumentException(
					"PostgreSQL accepts only non-alert events.",
					nameof(events));
			}

			if (events.Count == 0)
			{
				return;
			}

			await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);
			await using NpgsqlTransaction transaction =
				await connection.BeginTransactionAsync(cancellationToken);
			await using NpgsqlCommand command = new NpgsqlCommand(InsertSql, connection, transaction);

			NpgsqlParameter timestamp = command.Parameters.Add(
				"timestamp",
				NpgsqlDbType.TimestampTz);
			NpgsqlParameter endpointId = command.Parameters.Add(
				"endpoint_id",
				NpgsqlDbType.Uuid);
			NpgsqlParameter operatingSystem = command.Parameters.Add(
				"operating_system",
				NpgsqlDbType.Varchar);
			NpgsqlParameter eventType = command.Parameters.Add(
				"event_type",
				NpgsqlDbType.Varchar);
			NpgsqlParameter userId = command.Parameters.Add("user_id", NpgsqlDbType.Varchar);
			NpgsqlParameter processName = command.Parameters.Add(
				"process_name",
				NpgsqlDbType.Varchar);
			NpgsqlParameter pid = command.Parameters.Add("pid", NpgsqlDbType.Integer);
			NpgsqlParameter ppid = command.Parameters.Add("ppid", NpgsqlDbType.Integer);
			NpgsqlParameter filePath = command.Parameters.Add("file_path", NpgsqlDbType.Text);

			foreach (NormalizedSecurityEvent securityEvent in events)
			{
				DateTimeOffset parsedTimestamp = DateTimeOffset.Parse(
					securityEvent.Timestamp,
					CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind);

				timestamp.Value = parsedTimestamp.UtcDateTime;
				endpointId.Value = securityEvent.EndpointId;
				operatingSystem.Value = securityEvent.OperatingSystem
					.ToString()
					.ToLowerInvariant();
				eventType.Value = securityEvent.EventType;
				userId.Value = securityEvent.UserId;
				processName.Value = securityEvent.ProcessName;
				pid.Value = securityEvent.Pid;
				ppid.Value = securityEvent.Ppid;
				filePath.Value = (object?)securityEvent.FilePath ?? DBNull.Value;

				await command.ExecuteNonQueryAsync(cancellationToken);
			}

			await transaction.CommitAsync(cancellationToken);
		}

		public async Task<long> CountAsync(CancellationToken cancellationToken)
		{
			await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);
			await using NpgsqlCommand command = new NpgsqlCommand(
				"SELECT COUNT(*) FROM events;",
				connection);
			object? result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt64(result, CultureInfo.InvariantCulture);
		}

		public async Task<IReadOnlyList<EndpointDescriptor>> GetEndpointsAsync(
			CancellationToken cancellationToken)
		{
			const string sql =
				"""
				SELECT endpoint_id, operating_system
				FROM events
				GROUP BY endpoint_id, operating_system
				ORDER BY endpoint_id;
				""";

			await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);
			await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
			await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
			List<EndpointDescriptor> endpoints = [];

			while (await reader.ReadAsync(cancellationToken))
			{
				Guid endpointId = reader.GetGuid(0);
				string operatingSystemValue = reader.GetString(1);
				if (!Enum.TryParse(
						operatingSystemValue,
						ignoreCase: true,
						out EndpointOperatingSystem operatingSystem))
				{
					throw new InvalidDataException(
						$"Unsupported stored operating system '{operatingSystemValue}'.");
				}

				endpoints.Add(new EndpointDescriptor(endpointId, operatingSystem));
			}

			return endpoints;
		}

		public async Task<EndpointSummary?> GetSummaryAsync(
			Guid endpointId,
			CancellationToken cancellationToken)
		{
			await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			long totalEvents = await GetTotalEventsAsync(
				connection,
				endpointId,
				cancellationToken);
			if (totalEvents == 0)
			{
				return null;
			}

			string mostFrequentProcess = await GetMostFrequentProcessAsync(
				connection,
				endpointId,
				cancellationToken);
			IReadOnlyDictionary<string, long> eventTypeCounts = await GetEventTypeCountsAsync(
				connection,
				endpointId,
				cancellationToken);

			return new EndpointSummary(
				endpointId,
				totalEvents,
				mostFrequentProcess,
				eventTypeCounts);
		}

		private static async Task<long> GetTotalEventsAsync(
			NpgsqlConnection connection,
			Guid endpointId,
			CancellationToken cancellationToken)
		{
			const string sql = "SELECT COUNT(*) FROM events WHERE endpoint_id = @endpoint_id;";
			await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("endpoint_id", NpgsqlDbType.Uuid, endpointId);
			object? result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt64(result, CultureInfo.InvariantCulture);
		}

		private static async Task<string> GetMostFrequentProcessAsync(
			NpgsqlConnection connection,
			Guid endpointId,
			CancellationToken cancellationToken)
		{
			const string sql =
				"""
				SELECT process_name
				FROM events
				WHERE endpoint_id = @endpoint_id
				GROUP BY process_name
				ORDER BY COUNT(*) DESC, process_name
				LIMIT 1;
				""";
			await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("endpoint_id", NpgsqlDbType.Uuid, endpointId);
			object? result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToString(result, CultureInfo.InvariantCulture)
				?? throw new InvalidDataException("Endpoint has no process data.");
		}

		private static async Task<IReadOnlyDictionary<string, long>> GetEventTypeCountsAsync(
			NpgsqlConnection connection,
			Guid endpointId,
			CancellationToken cancellationToken)
		{
			const string sql =
				"""
				SELECT event_type, COUNT(*)
				FROM events
				WHERE endpoint_id = @endpoint_id
				GROUP BY event_type
				ORDER BY event_type;
				""";
			await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("endpoint_id", NpgsqlDbType.Uuid, endpointId);
			await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
			Dictionary<string, long> counts = new Dictionary<string, long>(
				StringComparer.Ordinal);

			while (await reader.ReadAsync(cancellationToken))
			{
				counts.Add(reader.GetString(0), reader.GetInt64(1));
			}

			return counts;
		}
	}
}
