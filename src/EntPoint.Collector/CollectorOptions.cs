using System.Globalization;

namespace EntPoint.Collector
{
	internal sealed class CollectorOptions
	{
		private CollectorOptions(
			string outputPath,
			int intervalMilliseconds,
			int? maxEvents,
			int machineCount,
			int initialProcesses,
			double alertPercentage,
			int? seed,
			bool showHelp)
		{
			OutputPath = outputPath;
			IntervalMilliseconds = intervalMilliseconds;
			MaxEvents = maxEvents;
			MachineCount = machineCount;
			InitialProcesses = initialProcesses;
			AlertPercentage = alertPercentage;
			Seed = seed;
			ShowHelp = showHelp;
		}

		public string OutputPath { get; }
		public int IntervalMilliseconds { get; }
		public int? MaxEvents { get; }
		public int MachineCount { get; }
		public int InitialProcesses { get; }
		public double AlertPercentage { get; }
		public int? Seed { get; }
		public bool ShowHelp { get; }

		public static CollectorOptions Parse(string[] args)
		{
			string outputPath = GetDefaultOutputPath();
			int intervalMilliseconds = 1000;
			int? maxEvents = null;
			int machineCount = 1;
			int initialProcesses = 8;
			double alertPercentage = 3d;
			int? seed = null;
			bool showHelp = false;

			for (int index = 0; index < args.Length; index++)
			{
				string argument = args[index];
				switch (argument)
				{
					case "--output":
						outputPath = ReadValue(args, ref index, argument);
						break;
					case "--interval-ms":
						intervalMilliseconds = ParseInt(ReadValue(args, ref index, argument), argument);
						break;
					case "--max-events":
						maxEvents = ParseInt(ReadValue(args, ref index, argument), argument);
						break;
					case "--machines":
						machineCount = ParseInt(ReadValue(args, ref index, argument), argument);
						break;
					case "--initial-processes":
						initialProcesses = ParseInt(ReadValue(args, ref index, argument), argument);
						break;
					case "--alert-percentage":
						alertPercentage = ParseDouble(ReadValue(args, ref index, argument), argument);
						break;
					case "--seed":
						seed = ParseInt(ReadValue(args, ref index, argument), argument);
						break;
					case "--help":
					case "-h":
						showHelp = true;
						break;
					default:
						throw new ArgumentException($"Unknown argument '{argument}'.");
				}
			}

			if (string.IsNullOrWhiteSpace(outputPath))
			{
				throw new ArgumentException("Output path cannot be empty.");
			}

			if (intervalMilliseconds < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(args),
					intervalMilliseconds,
					"Argument '--interval-ms' cannot be negative.");
			}

			if (maxEvents is <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(args),
					maxEvents,
					"Argument '--max-events' must be positive.");
			}

			if (initialProcesses < 1)
			{
				throw new ArgumentOutOfRangeException(
					nameof(args),
					initialProcesses,
					"Argument '--initial-processes' must be positive.");
			}

			if (machineCount < 1)
			{
				throw new ArgumentOutOfRangeException(
					nameof(args),
					machineCount,
					"Argument '--machines' must be positive.");
			}

			if (alertPercentage is < 0 or > 100)
			{
				throw new ArgumentOutOfRangeException(
					nameof(args),
					alertPercentage,
					"Argument '--alert-percentage' must be between 0 and 100.");
			}

			return new CollectorOptions(
				outputPath,
				intervalMilliseconds,
				maxEvents,
				machineCount,
				initialProcesses,
				alertPercentage,
				seed,
				showHelp);
		}

		public static string HelpText =>
			$"""
        EntPoint endpoint event simulator

        Options:
          --output <path>              NDJSON output path (default: {GetDefaultOutputPath()})
          --interval-ms <number>       Delay between continuous events (default: 1000)
          --max-events <number>        Stop after writing this many events
          --machines <number>          Number of virtual machines (default: 1)
          --initial-processes <number> Initial process inventory size (default: 8)
          --alert-percentage <number>  Alert frequency from 0 to 100 (default: 3)
          --seed <number>              Fixed random seed for repeatable output
          --help                       Show this help
        """;

		private static string GetDefaultOutputPath()
		{
			string? configuredOutputPath =
				Environment.GetEnvironmentVariable("ENTPOINT_OUTPUT_PATH");
			if (!string.IsNullOrWhiteSpace(configuredOutputPath))
			{
				return configuredOutputPath;
			}

			return string.Equals(
				Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
				"true",
				StringComparison.OrdinalIgnoreCase)
				? Path.Combine(Path.GetTempPath(), "entpoint", "events.ndjson")
				: Path.Combine("data", "events.ndjson");
		}

		private static string ReadValue(string[] args, ref int index, string argument)
		{
			if (++index >= args.Length)
			{
				throw new ArgumentException($"Argument '{argument}' requires a value.");
			}

			return args[index];
		}

		private static int ParseInt(string value, string argument) =>
			int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
				? result
				: throw new ArgumentException($"Argument '{argument}' requires an integer.");

		private static double ParseDouble(string value, string argument) =>
			double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
				? result
				: throw new ArgumentException($"Argument '{argument}' requires a number.");
	}
}
