using NamViet.ERP.Api.Application.Mappers.Admin;

namespace NamViet.ERP.Tests.Admin;

public class AdminSystemCodeTests
{
    [Fact]
    public void SuggestNext_starts_at_SYS_0001()
    {
        Assert.Equal("SYS-0001", AdminSystemCode.SuggestNext([]));
    }

    [Fact]
    public void SuggestNext_ignores_timestamp_and_named_codes()
    {
        Assert.Equal(
            "SYS-0003",
            AdminSystemCode.SuggestNext(["SYS-20260907030104", "SYS-ADMIN", "SYS-0002"]));
    }

    [Fact]
    public void Resolve_uses_request_when_present()
    {
        Assert.Equal("SYS-ADMIN", AdminSystemCode.Resolve("  SYS-ADMIN  ", ["SYS-0001"]));
    }

    [Fact]
    public void IsHrEmployeeStyle_detects_NV_prefix()
    {
        Assert.True(AdminSystemCode.IsHrEmployeeStyle("NV-0101"));
        Assert.False(AdminSystemCode.IsHrEmployeeStyle("SYS-0001"));
    }
}
