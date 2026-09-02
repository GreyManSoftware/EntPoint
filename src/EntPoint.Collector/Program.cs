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

				IReadOnlyList<SimulatedEndpoint> simulators = CreateSimulators(options);
				EventNormalizer normalizer = new EventNormalizer();
				await using NdjsonEventWriter writer = NdjsonEventWriter.Open(options.OutputPath);

				foreach (SimulatedEndpoint simulator in simulators)
				{
					string operatingSystem = simulator.OperatingSystem
						.ToString()
						.ToLowerInvariant();
					Console.WriteLine($"Endpoint: {simulator.EndpointId} ({operatingSystem})");
				}

				Console.WriteLine($"Writing events to: {Path.GetFullPath(options.OutputPath)}");

				int written = 0;
				foreach (SimulatedEndpoint simulator in simulators)
				{
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
				}

				int nextSimulatorIndex = 0;
				while (!cancellation.IsCancellationRequested &&
					   (!options.MaxEvents.HasValue || written < options.MaxEvents.Value))
				{
					SimulatedEndpoint simulator = simulators[nextSimulatorIndex];
					nextSimulatorIndex = (nextSimulatorIndex + 1) % simulators.Count;
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

		private static IReadOnlyList<SimulatedEndpoint> CreateSimulators(CollectorOptions options)
		{
			Random operatingSystemRandom = options.Seed.HasValue
				? new Random(options.Seed.Value)
				: new Random();
			EndpointOperatingSystem firstOperatingSystem = operatingSystemRandom.Next(2) == 0
				? EndpointOperatingSystem.Windows
				: EndpointOperatingSystem.Linux;
			List<SimulatedEndpoint> simulators = new List<SimulatedEndpoint>(options.MachineCount);

			for (int index = 0; index < options.MachineCount; index++)
			{
				EndpointOperatingSystem operatingSystem = index % 2 == 0
					? firstOperatingSystem
					: Opposite(firstOperatingSystem);
				int? seed = options.Seed.HasValue
					? unchecked(options.Seed.Value + index + 1)
					: null;

				simulators.Add(new SimulatedEndpoint(
					new SimulationOptions(
						options.InitialProcesses,
						options.AlertPercentage,
						seed),
					operatingSystem));
			}

			return simulators;
		}

		private static EndpointOperatingSystem Opposite(
			EndpointOperatingSystem operatingSystem) =>
			operatingSystem == EndpointOperatingSystem.Windows
				? EndpointOperatingSystem.Linux
				: EndpointOperatingSystem.Windows;
	}
}
