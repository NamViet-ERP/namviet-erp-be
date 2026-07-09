using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace NamViet.ERP.Api.Infrastructure;

public static class ApiValidationResponses
{
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var (key, state) in context.ModelState)
        {
            if (state?.Errors.Count is not > 0) continue;

            var field = NormalizeField(key);
            foreach (var error in state.Errors)
            {
                var message = MapMessage(field, error.ErrorMessage, error.Exception);
                if (string.IsNullOrWhiteSpace(message)) continue;

                if (!errors.TryGetValue(field, out var list))
                {
                    list = [];
                    errors[field] = list;
                }

                if (!list.Contains(message)) list.Add(message);
            }
        }

        if (errors.Count == 0)
        {
            return new BadRequestObjectResult(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["_form"] = ["Vui lòng kiểm tra lại thông tin."]
                }
            });
        }

        return new BadRequestObjectResult(new
        {
            errors = errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())
        });
    }

    private static string NormalizeField(string key)
    {
        var field = key;
        if (field.StartsWith("request.", StringComparison.OrdinalIgnoreCase))
            field = field["request.".Length..];

        if (field.Equals("request", StringComparison.OrdinalIgnoreCase))
            return "_form";

        if (field.StartsWith("$."))
            field = field[2..];

        return JsonNamingPolicy.CamelCase.ConvertName(field);
    }

    private static string MapMessage(string field, string? errorMessage, Exception? exception)
    {
        var raw = !string.IsNullOrWhiteSpace(errorMessage) ? errorMessage : exception?.Message ?? string.Empty;

        if (raw.Contains("could not be converted", StringComparison.OrdinalIgnoreCase))
        {
            return field switch
            {
                "projectType" => "Loại dự án không hợp lệ.",
                "startDate" => "Ngày bắt đầu không hợp lệ.",
                "materialBudget" => "Ngân sách vật tư không hợp lệ.",
                "pmId" => "Người phụ trách không hợp lệ.",
                _ => "Dữ liệu không hợp lệ."
            };
        }

        if (raw.Contains("required", StringComparison.OrdinalIgnoreCase))
        {
            return field switch
            {
                "projectCode" => "Vui lòng nhập mã kho dự án",
                "projectName" => "Vui lòng nhập tên công trình",
                "startDate" => "Vui lòng chọn ngày bắt đầu",
                "materialBudget" => "Vui lòng nhập ngân sách vật tư",
                "createdBy" => "Không xác định được người lập phiếu.",
                "_form" => "Vui lòng kiểm tra lại thông tin.",
                _ => "Trường này là bắt buộc"
            };
        }

        return string.IsNullOrWhiteSpace(raw) ? "Dữ liệu không hợp lệ." : raw;
    }
}
