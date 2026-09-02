using System.Globalization;

namespace EntPoint.Core
{
	public sealed class SimulatedEndpoint
	{
		private static readonly string[] ProcessNames =
		[
			"explorer.exe",
			"chrome.exe",
			"dotnet",
			"powershell.exe",
			"cmd.exe",
			"bash",
			"python",
			"code",
			"firefox.exe",
			"msedge.exe",
			"notepad.exe",
			"outlook.exe",
			"teams.exe",
			"node",
			"java",
			"nginx",
			"sshd",
			"curl",
			"git",
			"docker",
			"w3wp.exe",
			"sqlservr.exe",
			"rundll32.exe",
			"system_idle_process",
			"svchost.exe"
		];

		private static readonly string[] UserIds =
		[
			"user-1001",
			"user-1002",
			"service-backup",
			"service-web"
		];

		private static readonly HashSet<string> DenylistedProcessNames =
			new(["system_idle_process", "svchost.exe"], StringComparer.OrdinalIgnoreCase);

		private static readonly string[] FilePaths =
		[
			@"C:\Users\alex\Documents\report.docx",
			@"C:\Users\alex\Documents\budget.xlsx",
			@"C:\Users\alex\Downloads\invoice.pdf",
			@"C:\Users\alex\Downloads\payload.zip",
			@"C:\Users\alex\AppData\Local\Temp\update.exe",
			@"C:\Users\alex\AppData\Local\Google\Chrome\User Data\Default\Cookies",
			@"C:\ProgramData\EntPoint\config.json",
			@"C:\Windows\System32\config\SAM",
			@"C:\Windows\Temp\service.log",
			"/home/alex/.ssh/id_rsa",
			"/home/alex/.bash_history",
			"/home/alex/projects/app/appsettings.json",
			"/etc/shadow",
			"/etc/passwd",
			"/etc/hosts",
			"/var/log/auth.log",
			"/var/log/syslog",
			"/opt/service/config.yaml",
			"/tmp/session-token.txt",
			"/tmp/archive.zip"
		];

		private readonly SimulationOptions _options;
		private readonly Random _random;
		private readonly List<SimulatedProcess> _processes = [];
		private int _nextPid;
		private bool _initialized;

		public SimulatedEndpoint(SimulationOptions options, Guid? endpointId = null)
		{
			options.Validate();
			_options = options;
			_random = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();
			_nextPid = _random.Next(1000, 5000);
			EndpointId = endpointId ?? Guid.NewGuid();
		}

		public Guid EndpointId { get; }

		public IReadOnlyList<RawSecurityEvent> Initialize()
		{
			if (_initialized)
			{
				throw new InvalidOperationException("The endpoint has already been initialized.");
			}

			_initialized = true;
			List<RawSecurityEvent> events = new List<RawSecurityEvent>(_options.InitialProcessCount);

			for (int index = 0; index < _options.InitialProcessCount; index++)
			{
				int parentPid = SelectCollectableProcess()?.Pid ?? 0;
				SimulatedProcess process = CreateProcess(parentPid, requireCollectable: index == 0);
				_processes.Add(process);
				events.Add(CreateProcessEvent(SecurityEventTypes.ProcessSnapshot, process));
			}

			return events;
		}

		public RawSecurityEvent GenerateNext()
		{
			if (!_initialized)
			{
				throw new InvalidOperationException("Initialize the endpoint before generating events.");
			}

			return _random.NextDouble() < 0.35
				? GenerateProcessStart()
				: GenerateFileRead();
		}

		private RawSecurityEvent GenerateProcessStart()
		{
			SimulatedProcess parent = SelectCollectableProcess()
				?? throw new InvalidOperationException("No collectable parent process is available.");
			SimulatedProcess process = CreateProcess(parent.Pid);
			_processes.Add(process);
			return CreateProcessEvent(SecurityEventTypes.ProcessStart, process);
		}

		private RawSecurityEvent GenerateFileRead()
		{
			SimulatedProcess process = SelectCollectableProcess()
				?? throw new InvalidOperationException("No collectable process is available.");
			string filePath = FilePaths[_random.Next(FilePaths.Length)];
			(int? Score, string? Reason) alert =
				CreateAlert($"Suspicious file read by {process.Name}: {filePath}");

			return new RawSecurityEvent(
				UtcTimestamp(),
				EndpointId,
				SecurityEventTypes.FileRead,
				process.UserId,
				process.Name,
				process.Pid,
				process.Ppid,
				filePath,
				alert.Score,
				alert.Reason);
		}

		private RawSecurityEvent CreateProcessEvent(string eventType, SimulatedProcess process)
		{
			(int? Score, string? Reason) alert =
				CreateAlert($"Suspicious process activity: {process.Name}");

			return new RawSecurityEvent(
				UtcTimestamp(),
				EndpointId,
				eventType,
				process.UserId,
				process.Name,
				process.Pid,
				process.Ppid,
				AlertScore: alert.Score,
				AlertReason: alert.Reason);
		}

		private SimulatedProcess CreateProcess(int parentPid, bool requireCollectable = false) =>
			new(
				_nextPid++,
				parentPid,
				SelectProcessName(requireCollectable),
				UserIds[_random.Next(UserIds.Length)]);

		private SimulatedProcess? SelectCollectableProcess()
		{
			SimulatedProcess[] candidates = _processes
				.Where(process => !DenylistedProcessNames.Contains(process.Name))
				.ToArray();

			return candidates.Length == 0
				? null
				: candidates[_random.Next(candidates.Length)];
		}

		private string SelectProcessName(bool requireCollectable)
		{
			string processName;
			do
			{
				processName = ProcessNames[_random.Next(ProcessNames.Length)];
			}
			while (requireCollectable && DenylistedProcessNames.Contains(processName));

			return processName;
		}

		private (int? Score, string? Reason) CreateAlert(string reason)
		{
			if (_random.NextDouble() >= _options.AlertPercentage / 100)
			{
				return (null, null);
			}

			return (_random.Next(50, 101), reason);
		}

		private static string UtcTimestamp() =>
			DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

		private sealed record SimulatedProcess(int Pid, int Ppid, string Name, string UserId);
	}
}
