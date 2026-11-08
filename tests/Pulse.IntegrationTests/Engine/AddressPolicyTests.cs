using System.Net;
using Pulse.Infrastructure.Checks;
using Pulse.Infrastructure.Monitoring;

namespace Pulse.IntegrationTests.Engine;

public class AddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.18.0.5")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    public void Refuses_private_addresses_by_default(string address)
    {
        new PublicAddressPolicy().IsAllowed(IPAddress.Parse(address)).Should().BeFalse();
    }

    [Fact]
    public void Allows_public_addresses()
    {
        new PublicAddressPolicy().IsAllowed(IPAddress.Parse("93.184.216.34")).Should().BeTrue();
    }

    [Theory]
    [InlineData("172.18.0.5")]
    [InlineData("127.0.0.1")]
    public void The_end_to_end_stack_can_allow_private_addresses(string address)
    {
        new PublicAddressPolicy(allowPrivate: true).IsAllowed(IPAddress.Parse(address)).Should().BeTrue();
    }

    [Fact]
    public async Task The_save_time_guard_refuses_a_name_that_resolves_inside_unless_allowed()
    {
        var uri = new Uri("http://localhost/health");

        (await new DnsUrlGuard().CheckHostAsync(uri, CancellationToken.None)).Should().NotBeNull();
        (await new DnsUrlGuard(allowPrivate: true).CheckHostAsync(uri, CancellationToken.None)).Should().BeNull();
    }
}
