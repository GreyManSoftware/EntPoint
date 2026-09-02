using EntPoint.Core;

namespace EntPoint.Collector
{
	internal static class Program
	{
		public static async Task<int> Main(string[] args)
		{
			try
			{
				CollectorOptions options = CollectorOptions.Parse(args);
				if (options.ShowHelp)
				{
					Console.WriteLine(CollectorOptions.HelpText);
					return 0;
				}

				using CancellationTokenSource cancellation = new CancellationTokenSource();
				Console.CancelKeyPress += (_, eventArgs) =>
				{
					eventArgs.Cancel = true;
					cancellation.Cancel();
				};

				SimulatedEndpoint simulator = new SimulatedEndpoint(new SimulationOptions(
					options.InitialProcesses,
					options.AlertPercentage,
					options.Seed));
				EventNormalizer normalizer = new EventNormalizer();
				await using NdjsonEventWriter writer = NdjsonEventWriter.Open(options.OutputPath);

				Console.WriteLine($"Endpoint: {simulator.EndpointId}");
				Console.WriteLine($"Writing events to: {Path.GetFullPath(options.OutputPath)}");

				int written = 0;
				foreach (RawSecurityEvent rawEvent in simulator.Initialize())
				{
					if (await WriteIfAcceptedAsync(rawEvent))
					{
						written++;
					}

					if (options.MaxEvents.HasValue && written >= options.MaxEvents.Value)
					{
						Console.WriteLine($"Completed after writing {written} events.");
						return 0;
					}
				}

				while (!cancellation.IsCancellationRequested &&
					   (!options.MaxEvents.HasValue || written < options.MaxEvents.Value))
				{
					RawSecurityEvent rawEvent = simulator.GenerateNext();
					if (await WriteIfAcceptedAsync(rawEvent))
					{
						written++;
					}

					await Task.Delay(options.IntervalMilliseconds, cancellation.Token);
				}

				Console.WriteLine($"Stopped after writing {written} events.");
				return 0;

				async Task<bool> WriteIfAcceptedAsync(RawSecurityEvent rawEvent)
				{
					NormalizationResult result = normalizer.Normalize(rawEvent);
					if (!result.IsAccepted)
					{
						return false;
					}

					NormalizedSecurityEvent normalizedEvent = result.Event
						?? throw new InvalidOperationException(
							"Accepted normalization result has no event.");
					await writer.WriteAsync(normalizedEvent, cancellation.Token);
					return true;
				}
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("Collection stopped.");
				return 0;
			}
			catch (ArgumentException exception)
			{
				Console.Error.WriteLine(exception.Message);
				Console.Error.WriteLine("Use --help to view available options.");
				return 2;
			}
		}
	}
}
