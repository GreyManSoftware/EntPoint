using EntPoint.Core;
using MongoDB.Driver;

namespace EntPoint.Persistence
{
	public sealed class MongoAlertStore
	{
		private readonly IMongoCollection<AlertDocument> _collection;

		public MongoAlertStore(
			string connectionString,
			string databaseName,
			string collectionName = "alerts")
		{
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new ArgumentException(
					"MongoDB connection string is required.",
					nameof(connectionString));
			}

			if (string.IsNullOrWhiteSpace(databaseName))
			{
				throw new ArgumentException(
					"MongoDB database name is required.",
					nameof(databaseName));
			}

			MongoClient client = new MongoClient(connectionString);
			IMongoDatabase database = client.GetDatabase(databaseName);
			_collection = database.GetCollection<AlertDocument>(collectionName);
		}

		public async Task InitializeAsync(bool reset, CancellationToken cancellationToken)
		{
			if (reset)
			{
				await _collection.DeleteManyAsync(
					Builders<AlertDocument>.Filter.Empty,
					cancellationToken);
			}

			IndexKeysDefinitionBuilder<AlertDocument> keys =
				Builders<AlertDocument>.IndexKeys;
			IEnumerable<CreateIndexModel<AlertDocument>> indexes =
			[
				new CreateIndexModel<AlertDocument>(
					keys
						.Ascending(document => document.EndpointId)
						.Descending(document => document.AlertScore)
						.Descending(document => document.Timestamp),
					new CreateIndexOptions { Name = "ix_alerts_endpoint_score_timestamp" }),
				new CreateIndexModel<AlertDocument>(
					keys
						.Descending(document => document.AlertScore)
						.Descending(document => document.Timestamp),
					new CreateIndexOptions { Name = "ix_alerts_score_timestamp" }),
				new CreateIndexModel<AlertDocument>(
					keys.Descending(document => document.Timestamp),
					new CreateIndexOptions { Name = "ix_alerts_timestamp" })
			];

			await _collection.Indexes.CreateManyAsync(
				indexes,
				cancellationToken: cancellationToken);
		}

		public async Task InsertAsync(
			IReadOnlyCollection<NormalizedSecurityEvent> events,
			CancellationToken cancellationToken)
		{
			if (events.Any(securityEvent => !securityEvent.IsAlert))
			{
				throw new ArgumentException(
					"MongoDB accepts only alert events.",
					nameof(events));
			}

			if (events.Count == 0)
			{
				return;
			}

			IEnumerable<AlertDocument> documents = events.Select(AlertDocument.FromEvent);
			await _collection.InsertManyAsync(
				documents,
				cancellationToken: cancellationToken);
		}

		public Task<long> CountAsync(CancellationToken cancellationToken) =>
			_collection.CountDocumentsAsync(
				Builders<AlertDocument>.Filter.Empty,
				cancellationToken: cancellationToken);
	}
}
