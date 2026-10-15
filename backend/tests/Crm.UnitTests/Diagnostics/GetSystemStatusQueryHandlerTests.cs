using Crm.Application.Diagnostics.GetSystemStatus;

namespace Crm.UnitTests.Diagnostics;

public sealed class GetSystemStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsHealthyStatus()
    {
        var handler = new GetSystemStatusQueryHandler(TimeProvider.System);

        var result = await handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        Assert.Equal(GetSystemStatusQueryHandler.Healthy, result.Status);
    }
}
