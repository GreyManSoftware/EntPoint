using EntPoint.Persistence;

namespace EntPoint.Ingestion
{
	internal static class Program
	{
		public static async Task<int> Main(string[] args)
		{
			try
			{
				IngestionOptions options = IngestionOptions.Parse(args);
				if (options.ShowHelp)
				{
					Console.WriteLine(IngestionOptions.HelpText);
					return 0;
				}

				using CancellationTokenSource cancellation = new CancellationTokenSource();
				Console.CancelKeyPress += (_, eventArgs) =>
				{
					eventArgs.Cancel = true;
					cancellation.Cancel();
				};

				PostgresEventStore eventStore = new PostgresEventStore(
					options.PostgresConnectionString);
				MongoAlertStore alertStore = new MongoAlertStore(
					options.MongoConnectionString,
					options.MongoDatabaseName);

				Task initializeEvents = eventStore.InitializeAsync(
					options.Reset,
					cancellation.Token);
				Task initializeAlerts = alertStore.InitializeAsync(
					options.Reset,
					cancellation.Token);
				await Task.WhenAll(initializeEvents, initializeAlerts);

				NdjsonEventReader reader = new NdjsonEventReader();
				IngestionBatch batch = await reader.ReadAsync(
					options.InputPath,
					cancellation.Token);

				await eventStore.InsertAsync(batch.Events, cancellation.Token);
				await alertStore.InsertAsync(batch.Alerts, cancellation.Token);

				long storedEvents = await eventStore.CountAsync(cancellation.Token);
				long storedAlerts = await alertStore.CountAsync(cancellation.Token);

				Console.WriteLine(
					$"Ingested {batch.Events.Count} events into PostgreSQL and " +
					$"{batch.Alerts.Count} alerts into MongoDB.");
				Console.WriteLine(
					$"Stored totals: {storedEvents} events, {storedAlerts} alerts.");
				return 0;
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("Ingestion stopped.");
				return 0;
			}
			catch (ArgumentException exception)
			{
				Console.Error.WriteLine(exception.Message);
				Console.Error.WriteLine("Use --help to view available options.");
				return 2;
			}
			catch (FileNotFoundException exception)
			{
				Console.Error.WriteLine(exception.Message);
				return 3;
			}
			catch (InvalidDataException exception)
			{
				Console.Error.WriteLine(exception.Message);
				return 3;
			}
		}
	}
}
