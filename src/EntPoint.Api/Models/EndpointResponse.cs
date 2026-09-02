using EntPoint.Core;
using EntPoint.Persistence;

namespace EntPoint.Api.Models
{
	public sealed record EndpointResponse(
		Guid EndpointId,
		EndpointOperatingSystem OperatingSystem)
	{
		public static EndpointResponse FromDescriptor(EndpointDescriptor descriptor) =>
			new EndpointResponse(descriptor.EndpointId, descriptor.OperatingSystem);
	}
}
