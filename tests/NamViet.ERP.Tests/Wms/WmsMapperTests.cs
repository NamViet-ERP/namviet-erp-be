using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Tests.Wms;

public class WmsMapperTests
{
    [Theory]
    [InlineData("approved", ApprovalStatus.Approved)]
    [InlineData("draft", ApprovalStatus.Draft)]
    [InlineData("rejected", ApprovalStatus.Rejected)]
    [InlineData("pending", ApprovalStatus.Pending)]
    [InlineData(null, ApprovalStatus.Pending)]
    public void FromClientApprovalStatus_maps_known_values(string? input, ApprovalStatus expected)
    {
        Assert.Equal(expected, WmsMapper.FromClientApprovalStatus(input));
    }

    [Theory]
    [InlineData("admin", "Admin")]
    [InlineData("storekeeper", "Warehouse_Keeper")]
    [InlineData("pm", "PM")]
    public void ToIdentityRole_maps_client_roles(string clientRole, string expected)
    {
        Assert.Equal(expected, WmsMapper.ToIdentityRole(clientRole));
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved, "approved")]
    [InlineData(ApprovalStatus.Draft, "draft")]
    [InlineData(ApprovalStatus.Pending, "pending")]
    public void ToClientApprovalStatus_round_trips(ApprovalStatus status, string expected)
    {
        Assert.Equal(expected, WmsMapper.ToClientApprovalStatus(status));
    }
}
