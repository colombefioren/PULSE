using FluentValidation.Results;
using Pulse.Application.Monitors;
using Pulse.Domain.Monitors;

namespace Pulse.UnitTests.Monitors;

public class MonitorInputValidatorTests
{
    private static readonly MonitorInputValidator Validator = new();

    private static MonitorInput Valid => new("API", "https://example.com/health");

    private static ValidationResult Run(MonitorInput input) => Validator.Validate(input);

    [Fact]
    public void Accepts_a_monitor_with_only_a_name_and_address()
    {
        Run(Valid).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_every_option_filled_in()
    {
        var input = Valid with
        {
            Method = CheckMethod.Post,
            IntervalSeconds = 30,
            TimeoutMs = 30_000,
            ExpectedCodes = "200-204, 301",
            Keyword = "ok",
            Headers = new() { ["Authorization"] = "Bearer abc", ["X-Env"] = "prod" },
            FailureThreshold = 5,
            RecoveryThreshold = 1,
        };

        Run(input).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Needs_a_name(string name) =>
        Run(Valid with { Name = name }).Errors.Should().Contain(e => e.PropertyName == "Name");

    [Fact]
    public void Limits_the_name_length() =>
        Run(Valid with { Name = new string('a', 81) }).Errors.Should().Contain(e => e.PropertyName == "Name");

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    public void Refuses_addresses_that_cannot_be_checked(string url) =>
        Run(Valid with { Url = url }).Errors.Should().Contain(e => e.PropertyName == "Url");

    [Theory]
    [InlineData(45)]
    [InlineData(0)]
    [InlineData(3600)]
    public void Refuses_intervals_that_are_not_on_the_list(int seconds) =>
        Run(Valid with { IntervalSeconds = seconds }).Errors.Should().Contain(e => e.PropertyName == "IntervalSeconds");

    [Theory]
    [InlineData(999)]
    [InlineData(30_001)]
    public void Keeps_the_timeout_between_one_and_thirty_seconds(int timeout) =>
        Run(Valid with { TimeoutMs = timeout }).Errors.Should().Contain(e => e.PropertyName == "TimeoutMs");

    [Fact]
    public void The_timeout_cannot_outlast_the_interval()
    {
        var result = Run(Valid with { IntervalSeconds = 30, TimeoutMs = 30_000 });
        result.IsValid.Should().BeTrue();

        // The interval list starts at 30s and the timeout tops out at 30s, so equality is the edge.
        Run(Valid with { IntervalSeconds = 30, TimeoutMs = 29_000 }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2xx")]
    [InlineData("700")]
    [InlineData("299-200")]
    public void Refuses_status_codes_it_cannot_read(string codes) =>
        Run(Valid with { ExpectedCodes = codes }).Errors.Should().Contain(e => e.PropertyName == "ExpectedCodes");

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Keeps_thresholds_between_one_and_ten(int threshold)
    {
        Run(Valid with { FailureThreshold = threshold }).Errors.Should().Contain(e => e.PropertyName == "FailureThreshold");
        Run(Valid with { RecoveryThreshold = threshold }).Errors.Should().Contain(e => e.PropertyName == "RecoveryThreshold");
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("content-length")]
    [InlineData("Bad Name")]
    [InlineData("")]
    public void Refuses_headers_that_are_reserved_or_malformed(string name) =>
        Run(Valid with { Headers = new() { [name] = "x" } }).Errors.Should().Contain(e => e.PropertyName == "Headers");

    [Fact]
    public void Refuses_a_header_value_with_a_line_break() =>
        Run(Valid with { Headers = new() { ["X-A"] = "one\r\nInjected: yes" } }).Errors.Should().Contain(e => e.PropertyName == "Headers");

    [Fact]
    public void Limits_the_number_of_headers()
    {
        var headers = Enumerable.Range(0, 11).ToDictionary(i => $"X-H{i}", _ => "v");

        Run(Valid with { Headers = headers }).Errors.Should().Contain(e => e.PropertyName == "Headers");
    }
}
