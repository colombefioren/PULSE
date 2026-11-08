using System.Net;
using Pulse.Domain.Monitors;

namespace Pulse.UnitTests.Monitors;

public class NetworkPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    public void Refuses_addresses_that_are_not_on_the_public_internet(string address) =>
        NetworkPolicy.IsPublic(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2a00:1450:4007:80f::200e")]
    [InlineData("::ffff:8.8.8.8")]
    public void Allows_ordinary_public_addresses(string address) =>
        NetworkPolicy.IsPublic(IPAddress.Parse(address)).Should().BeTrue();
}

public class MonitorUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com:8080/health?x=1")]
    [InlineData("https://93.184.216.34/")]
    [InlineData("https://[2606:4700:4700::1111]/")]
    [InlineData("  https://example.com/  ")]
    public void Accepts_public_http_and_https_addresses(string text)
    {
        var (uri, error) = MonitorUrl.Parse(text);

        error.Should().BeNull();
        uri.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_what_is_not_an_http_address(string text) =>
        MonitorUrl.Parse(text).Error.Should().NotBeNull();

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST:5000/")]
    [InlineData("http://app.localhost/")]
    [InlineData("http://printer.local/")]
    [InlineData("http://db.internal/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.1/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f000001/")]
    [InlineData("http://10.0.0.1/admin")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://[fd00::1]/")]
    public void Rejects_private_loopback_and_metadata_targets(string text) =>
        MonitorUrl.Parse(text).Error.Should().NotBeNull();

    [Fact]
    public void Rejects_credentials_in_the_address()
    {
        var (_, error) = MonitorUrl.Parse("https://user:secret@example.com/");

        error.Should().Contain("username and password");
    }
}

public class ExpectedCodesTests
{
    [Theory]
    [InlineData("200-299", 200, true)]
    [InlineData("200-299", 299, true)]
    [InlineData("200-299", 300, false)]
    [InlineData("200-299", 199, false)]
    [InlineData("200,301,404", 301, true)]
    [InlineData("200,301,404", 302, false)]
    [InlineData("200-204, 301", 301, true)]
    [InlineData("200-204, 301", 205, false)]
    [InlineData(" 503 ", 503, true)]
    public void Matches_single_codes_and_ranges(string text, int code, bool expected) =>
        ExpectedCodes.Parse(text)!.Matches(code).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("200-")]
    [InlineData("-200")]
    [InlineData("299-200")]
    [InlineData("99")]
    [InlineData("600")]
    [InlineData("200-299-300")]
    [InlineData("2xx")]
    [InlineData("200,,")]
    public void Rejects_text_that_is_not_a_list_of_codes(string? text) =>
        ExpectedCodes.Parse(text).Should().BeNull();

    [Fact]
    public void Describes_itself_for_error_messages() =>
        ExpectedCodes.Parse("200-299, 301")!.Describe().Should().Be("200–299, 301");
}

public class UptimeMathTests
{
    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(100, 100, 100.0)]
    [InlineData(0, 10, 0.0)]
    [InlineData(2, 3, 66.67)]
    [InlineData(9998, 10000, 99.98)]
    public void Computes_uptime_as_a_percentage(long successes, long total, double? expected) =>
        UptimeMath.Percent(successes, total).Should().Be(expected);

    [Fact]
    public void Picks_percentiles_by_nearest_rank()
    {
        var values = Enumerable.Range(1, 100).ToList();

        UptimeMath.Percentile(values, 50).Should().Be(50);
        UptimeMath.Percentile(values, 95).Should().Be(95);
        UptimeMath.Percentile(values, 99).Should().Be(99);
        UptimeMath.Percentile(values, 100).Should().Be(100);
    }

    [Fact]
    public void Has_no_percentile_without_data() =>
        UptimeMath.Percentile([], 95).Should().BeNull();
}
