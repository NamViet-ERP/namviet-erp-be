using NamViet.ERP.Api.Application.Services.Admin;

namespace NamViet.ERP.Tests.Admin;

public class AdminRoleNamingTests
{
    [Theory]
    [InlineData("Nhân viên", "Nhân viên")]
    [InlineData("Nhân viên", "nhân viên")]
    [InlineData("Nhân viên", "  Nhân   viên ")]
    [InlineData("Nhân viên", "Nhan vien")]
    public void SameDisplayLabel_treats_vietnamese_aliases_as_duplicate(string a, string b)
    {
        Assert.True(AdminRoleNaming.SameDisplayLabel(a, b));
    }

    [Fact]
    public void SameDisplayLabel_distinguishes_different_roles()
    {
        Assert.False(AdminRoleNaming.SameDisplayLabel("Nhân viên", "Nhân sự (HR)"));
    }

    [Fact]
    public void DisplayLabel_uses_description_when_set()
    {
        Assert.Equal("Nhân viên", AdminRoleNaming.DisplayLabel("Nhân viên", "Staff"));
    }

    [Fact]
    public void DisplayLabel_falls_back_to_system_role_caption()
    {
        Assert.Equal("Nhân viên", AdminRoleNaming.DisplayLabel(null, "Staff"));
        Assert.Equal("Nhân viên", AdminRoleNaming.DisplayLabel("  ", "Staff"));
    }
}
