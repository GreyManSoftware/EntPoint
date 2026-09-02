using System.Globalization;

namespace EntPoint.Ingestion
{
	internal sealed class IngestionOptions
	{
		private IngestionOptions(
			string inputPath,
			string postgresConnectionString,
			string mongoConnectionString,
			string mongoDatabaseName,
			bool reset,
			bool showHelp)
		{
			InputPath = inputPath;
			PostgresConnectionString = postgresConnectionString;
			MongoConnectionString = mongoConnectionString;
			MongoDatabaseName = mongoDatabaseName;
			Reset = reset;
			ShowHelp = showHelp;
		}

		public string InputPath { get; }
		public string PostgresConnectionString { get; }
		public string MongoConnectionString { get; }
		public string MongoDatabaseName { get; }
		public bool Reset { get; }
		public bool ShowHelp { get; }

		public static IngestionOptions Parse(string[] args)
		{
			string inputPath = ReadEnvironment(
				"ENTPOINT_INPUT_PATH",
				Path.Combine("data", "events.ndjson"));
			string postgresConnectionString =
				Environment.GetEnvironmentVariable("ENTPOINT_POSTGRES") ?? string.Empty;
			string mongoConnectionString =
				Environment.GetEnvironmentVariable("ENTPOINT_MONGO") ?? string.Empty;
			string mongoDatabaseName =
				Environment.GetEnvironmentVariable("ENTPOINT_MONGO_DATABASE") ?? string.Empty;
			bool reset = false;
			bool showHelp = false;

			for (int index = 0; index < args.Length; index++)
			{
				string argument = args[index];
				switch (argument)
				{
					case "--input":
						inputPath = ReadValue(args, ref index, argument);
						break;
					case "--postgres":
						postgresConnectionString = ReadValue(args, ref index, argument);
						break;
					case "--mongo":
						mongoConnectionString = ReadValue(args, ref index, argument);
						break;
					case "--mongo-database":
						mongoDatabaseName = ReadValue(args, ref index, argument);
						break;
					case "--reset":
						reset = true;
						break;
					case "--help":
					case "-h":
						showHelp = true;
						break;
					default:
						throw new ArgumentException($"Unknown argument '{argument}'.");
				}
			}

			ValidateRequired(inputPath, "--input");
			ValidateRequired(postgresConnectionString, "--postgres");
			ValidateRequired(mongoConnectionString, "--mongo");
			ValidateRequired(mongoDatabaseName, "--mongo-database");

			return new IngestionOptions(
				inputPath,
				postgresConnectionString,
				mongoConnectionString,
				mongoDatabaseName,
				reset,
				showHelp);
		}

		public static string HelpText =>
			"""
			EntPoint event ingestion

			Options:
			  --input <path>             NDJSON input path
			  --postgres <connection>    PostgreSQL connection string
			  --mongo <connection>       MongoDB connection string
			  --mongo-database <name>    MongoDB database name
			  --reset                    Clear existing events and alerts before ingestion
			  --help                     Show this help

			Environment variables:
			  ENTPOINT_INPUT_PATH
			  ENTPOINT_POSTGRES          Required unless --postgres is provided
			  ENTPOINT_MONGO             Required unless --mongo is provided
			  ENTPOINT_MONGO_DATABASE    Required unless --mongo-database is provided
			""";

		private static string ReadEnvironment(string name, string fallback)
		{
			string? value = Environment.GetEnvironmentVariable(name);
			return string.IsNullOrWhiteSpace(value) ? fallback : value;
		}

		private static string ReadValue(string[] args, ref int index, string argument)
		{
			if (++index >= args.Length)
			{
				throw new ArgumentException($"Argument '{argument}' requires a value.");
			}

			return args[index];
		}

		private static void ValidateRequired(string value, string argument)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new ArgumentException(
					string.Create(
						CultureInfo.InvariantCulture,
						$"Argument '{argument}' cannot be empty."));
			}
		}
	}
}
