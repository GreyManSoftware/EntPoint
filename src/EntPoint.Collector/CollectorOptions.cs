using System.Globalization;

namespace EntPoint.Collector
{
	internal sealed record CollectorOptions(
		string OutputPath,
		int IntervalMilliseconds,
		int? MaxEvents,
		int InitialProcesses,
		double AlertPercentage,
		int? Seed,
		bool ShowHelp)
	{
		public static CollectorOptions Parse(string[] args)
		{
			string outputPath = Path.Combine("data", "events.ndjson");
			int intervalMilliseconds = 1000;
			int? maxEvents = null;
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
					nameof(intervalMilliseconds),
					"Interval cannot be negative.");
			}

			if (maxEvents is <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(maxEvents),
					"Maximum events must be positive.");
			}

			if (initialProcesses < 1)
			{
				throw new ArgumentOutOfRangeException(
					nameof(initialProcesses),
					"At least one initial process is required.");
			}

			if (alertPercentage is < 0 or > 100)
			{
				throw new ArgumentOutOfRangeException(
					nameof(alertPercentage),
					"Alert percentage must be between 0 and 100.");
			}

			return new CollectorOptions(
				outputPath,
				intervalMilliseconds,
				maxEvents,
				initialProcesses,
				alertPercentage,
				seed,
				showHelp);
		}

		public static string HelpText =>
			"""
        EntPoint endpoint event simulator

        Options:
          --output <path>              NDJSON output path (default: data/events.ndjson)
          --interval-ms <number>       Delay between continuous events (default: 1000)
          --max-events <number>        Stop after writing this many events
          --initial-processes <number> Initial process inventory size (default: 8)
          --alert-percentage <number>  Alert frequency from 0 to 100 (default: 3)
          --seed <number>              Fixed random seed for repeatable output
          --help                       Show this help
        """;

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
