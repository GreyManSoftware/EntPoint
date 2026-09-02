using System.Globalization;
using EntPoint.Core;
using Npgsql;
using NpgsqlTypes;

namespace EntPoint.Persistence
{
	public sealed class PostgresEventStore
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
	}
}
