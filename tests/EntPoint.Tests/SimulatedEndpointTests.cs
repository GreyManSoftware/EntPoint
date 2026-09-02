using EntPoint.Core;

namespace EntPoint.Tests
{
	public sealed class SimulatedEndpointTests
	{
		[Fact]
		public void Initialize_CreatesCoherentProcessInventory()
		{
			SimulatedEndpoint simulator = new SimulatedEndpoint(
				new SimulationOptions(InitialProcessCount: 12, Seed: 42));

			IReadOnlyList<RawSecurityEvent> events = simulator.Initialize();
			HashSet<int> processIds = events.Select(securityEvent => securityEvent.Pid).ToHashSet();

			Assert.Equal(12, events.Count);
			Assert.Equal(12, processIds.Count);
			Assert.All(events, securityEvent =>
			{
				Assert.Equal(SecurityEventTypes.ProcessSnapshot, securityEvent.EventType);
				Assert.True(securityEvent.Ppid == 0 || processIds.Contains(securityEvent.Ppid));
			});
		}

		[Fact]
		public void GenerateNext_UsesKnownProcessesForFileReadsAndParents()
		{
			SimulatedEndpoint simulator = new SimulatedEndpoint(
				new SimulationOptions(InitialProcessCount: 8, Seed: 123));
			HashSet<int> knownProcessIds = simulator.Initialize()
				.Select(securityEvent => securityEvent.Pid)
				.ToHashSet();
			int fileReads = 0;

			for (int index = 0; index < 100; index++)
			{
				RawSecurityEvent securityEvent = simulator.GenerateNext();
				if (securityEvent.EventType == SecurityEventTypes.ProcessStart)
				{
					Assert.Contains(securityEvent.Ppid, knownProcessIds);
					knownProcessIds.Add(securityEvent.Pid);
				}
				else
				{
					Assert.Equal(SecurityEventTypes.FileRead, securityEvent.EventType);
					Assert.Contains(securityEvent.Pid, knownProcessIds);
					Assert.False(string.IsNullOrWhiteSpace(securityEvent.FilePath));
					fileReads++;
				}
			}

			Assert.True(fileReads > 0);
		}

		[Fact]
		public void GenerateNext_UsesConfiguredAlertPercentage()
		{
			SimulatedEndpoint simulator = new SimulatedEndpoint(
				new SimulationOptions(InitialProcessCount: 2, AlertPercentage: 100, Seed: 9));

			IReadOnlyList<RawSecurityEvent> initialEvents = simulator.Initialize();
			RawSecurityEvent nextEvent = simulator.GenerateNext();

			Assert.All(initialEvents, securityEvent => Assert.NotNull(securityEvent.AlertScore));
			Assert.NotNull(nextEvent.AlertScore);
			Assert.False(string.IsNullOrWhiteSpace(nextEvent.AlertReason));
		}
	}
}
