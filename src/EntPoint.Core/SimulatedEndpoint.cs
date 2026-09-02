using System.Globalization;

namespace EntPoint.Core
{
	public sealed class SimulatedEndpoint
	{
		private static readonly string[] WindowsProcessNames =
		[
			"explorer.exe",
			"chrome.exe",
			"powershell.exe",
			"cmd.exe",
			"firefox.exe",
			"msedge.exe",
			"notepad.exe",
			"outlook.exe",
			"teams.exe",
			"w3wp.exe",
			"sqlservr.exe",
			"rundll32.exe",
			"dotnet.exe",
			"code.exe",
			"system_idle_process",
			"svchost.exe"
		];

		private static readonly string[] LinuxProcessNames =
		[
			"systemd",
			"bash",
			"python3",
			"dotnet",
			"code",
			"node",
			"java",
			"nginx",
			"sshd",
			"curl",
			"git",
			"dockerd",
			"cron",
			"rsyslogd",
			"kthreadd",
			"kworker"
		];

		private static readonly string[] WindowsUserIds =
		[
			@"CORP\alex",
			@"CORP\jamie",
			@"NT AUTHORITY\SYSTEM",
			@"NT SERVICE\W3SVC"
		];

		private static readonly string[] LinuxUserIds =
		[
			"alex",
			"jamie",
			"root",
			"www-data",
			"backup"
		];

		private static readonly string[] WindowsFilePaths =
		[
			@"C:\Users\alex\Documents\report.docx",
			@"C:\Users\alex\Documents\budget.xlsx",
			@"C:\Users\alex\Downloads\invoice.pdf",
			@"C:\Users\alex\Downloads\payload.zip",
			@"C:\Users\alex\AppData\Local\Temp\update.exe",
			@"C:\Users\alex\AppData\Local\Google\Chrome\User Data\Default\Cookies",
			@"C:\ProgramData\EntPoint\config.json",
			@"C:\Windows\System32\config\SAM",
			@"C:\Windows\Temp\service.log"
		];

		private static readonly string[] LinuxFilePaths =
		[
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

		private static readonly HashSet<string> DenylistedProcessNames =
			new(
				["system_idle_process", "svchost.exe", "kthreadd", "kworker"],
				StringComparer.OrdinalIgnoreCase);

		private readonly SimulationOptions _options;
		private readonly Random _random;
		private readonly string[] _processNames;
		private readonly string[] _userIds;
		private readonly string[] _filePaths;
		private readonly List<SimulatedProcess> _processes = [];
		private int _nextPid;
		private bool _initialized;

		public SimulatedEndpoint(
			SimulationOptions options,
			EndpointOperatingSystem operatingSystem,
			Guid? endpointId = null)
		{
			options.Validate();
			_options = options;
			_random = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();
			_nextPid = _random.Next(1000, 5000);
			OperatingSystem = operatingSystem;
			EndpointId = endpointId ?? Guid.NewGuid();

			(_processNames, _userIds, _filePaths) = operatingSystem switch
			{
				EndpointOperatingSystem.Windows =>
					(WindowsProcessNames, WindowsUserIds, WindowsFilePaths),
				EndpointOperatingSystem.Linux =>
					(LinuxProcessNames, LinuxUserIds, LinuxFilePaths),
				_ => throw new ArgumentOutOfRangeException(
					nameof(operatingSystem),
					operatingSystem,
					"Unsupported operating system.")
			};
		}

		public Guid EndpointId { get; }

		public EndpointOperatingSystem OperatingSystem { get; }

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
			string filePath = _filePaths[_random.Next(_filePaths.Length)];
			(int? Score, string? Reason) alert =
				CreateAlert($"Suspicious file read by {process.Name}: {filePath}");

			return new RawSecurityEvent(
				UtcTimestamp(),
				EndpointId,
				OperatingSystem,
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
				OperatingSystem,
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
				_userIds[_random.Next(_userIds.Length)]);

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
				processName = _processNames[_random.Next(_processNames.Length)];
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
