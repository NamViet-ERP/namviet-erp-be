$ErrorActionPreference = 'Stop'
$base = 'http://localhost:8080'
$results = [System.Collections.Generic.List[object]]::new()

function Record([string]$area, [string]$check, [bool]$ok, [string]$detail = '') {
    $results.Add([pscustomobject]@{ Area = $area; Check = $check; Ok = $ok; Detail = $detail })
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    Write-Host "[$mark] $area :: $check $(if ($detail) { "- $detail" })"
}

function Invoke-Api {
    param(
        [string]$Method = 'GET',
        [string]$Path,
        $Body = $null,
        [string]$Token = $script:token
    )
    $headers = @{ Accept = 'application/json' }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $uri = "$base$Path"
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $bytes
    }
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
}

try {
    $username = $env:SMOKE_API_USERNAME
    $password = $env:SMOKE_API_PASSWORD
    if (-not $username -or -not $password) {
        throw 'Set SMOKE_API_USERNAME and SMOKE_API_PASSWORD (no hardcoded credentials).'
    }
    $login = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -ContentType 'application/json' -Body (@{
        username = $username; password = $password; rememberMe = $false
    } | ConvertTo-Json)
    $script:token = $login.token
    if (-not $script:token -and $login.accessToken) { $script:token = $login.accessToken }
    Record 'Auth' 'Login' ($null -ne $script:token) "user=$($login.user.email)"
} catch {
    Record 'Auth' 'Login' $false $_.Exception.Message
    $results | Format-Table -AutoSize
    exit 1
}

$getChecks = @(
    @{ Area = 'WMS'; Name = 'Projects list'; Path = '/api/warehouse/projects?page=1&pageSize=5' },
    @{ Area = 'WMS'; Name = 'Stock matrix'; Path = '/api/stock/matrix' },
    @{ Area = 'WMS'; Name = 'Imports list'; Path = '/api/imports' },
    @{ Area = 'WMS'; Name = 'Exports list'; Path = '/api/exports' },
    @{ Area = 'WMS'; Name = 'Transfers list'; Path = '/api/transfers' },
    @{ Area = 'WMS'; Name = 'Export returns list'; Path = '/api/export-returns' },
    @{ Area = 'WMS'; Name = 'Stocktakes list'; Path = '/api/stocktakes' },
    @{ Area = 'WMS'; Name = 'Materials list'; Path = '/api/warehouse/materials?page=1&pageSize=5' },
    @{ Area = 'WMS'; Name = 'Dashboard KPI'; Path = '/api/dashboard/kpi' },
    @{ Area = 'WMS'; Name = 'Notifications'; Path = '/api/warehouse/notifications/pending' },
    @{ Area = 'WMS'; Name = 'Low stock alerts'; Path = '/api/warehouse/alerts/low-stock' },
    @{ Area = 'HRM'; Name = 'Employees list'; Path = '/api/employees?page=1&pageSize=5' },
    @{ Area = 'HRM'; Name = 'Candidates list'; Path = '/api/candidates?page=1&pageSize=5' },
    @{ Area = 'Admin'; Name = 'Admin users'; Path = '/api/admin/users?page=1&pageSize=5' }
)

foreach ($c in $getChecks) {
    try {
        $data = Invoke-Api -Path $c.Path
        $ok = $null -ne $data
        $detail = if ($data -is [array]) { "count=$($data.Count)" } elseif ($data.items) { "count=$($data.items.Count)" } else { 'ok' }
        Record $c.Area $c.Name $ok $detail
    } catch {
        Record $c.Area $c.Name $false $_.Exception.Message
    }
}

# Stocktake create — payload giống FE (không id, không voucherCode)
try {
    $projects = Invoke-Api -Path '/api/warehouse/projects?page=1&pageSize=20'
    $project = $projects.items | Where-Object { $_.status -eq 'Active' } | Select-Object -First 1
    if (-not $project) { throw 'No active project' }

    $materials = Invoke-Api -Path '/api/warehouse/materials?page=1&pageSize=20'
    $mat = $materials.items | Select-Object -First 1
    if (-not $mat) { throw 'No material' }

    $createBody = @{
        date = (Get-Date).ToString('dd/MM/yyyy')
        status = 'pending'
        warehouseId = $project.projectCode
        warehouseName = $project.projectName
        condition = 'counting'
        isFrozen = $true
        note = 'SMOKE-TEST auto'
        items = @(
            @{
                productId = $mat.materialCode
                sku = $mat.materialCode
                name = if ($mat.materialName) { $mat.materialName } else { 'Smoke material' }
                systemQty = 0
                actualQty = 1
                diff = 1
            }
        )
    }

    $created = Invoke-Api -Method POST -Path '/api/stocktakes' -Body $createBody
    $codeOk = $created.voucherCode -match '^KK-\d{4}$'
    Record 'Stocktake' 'Create without voucherCode' ($null -ne $created.id) "code=$($created.voucherCode)"
    Record 'Stocktake' 'Generated KK format' $codeOk $created.voucherCode
    Record 'Stocktake' 'Default frozen on create' ($created.isFrozen -eq $true) "isFrozen=$($created.isFrozen)"

    # Freeze guard — export create should fail while frozen
    try {
        $exportBody = @{
            voucherCode = ''
            date = (Get-Date).ToString('dd/MM/yyyy')
            soCode = 'SMOKE'
            customer = 'SMOKE'
            warehouseId = 'KHO-TONG'
            items = @(@{ materialCode = $mat.materialCode; sku = $mat.materialCode; name = $mat.materialName; qty = 1; unit = $mat.unit })
            status = 'pending'
            createdBy = 'SMOKE'
            note = ''
            project = $project.projectCode
        }
        Invoke-Api -Method POST -Path '/api/exports' -Body $exportBody | Out-Null
        Record 'Freeze' 'Block export when frozen' $false 'export succeeded unexpectedly'
    } catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        $msg = $_.ErrorDetails.Message
        if (-not $msg) { $msg = $_.Exception.Message }
        $blocked = ($status -eq 400) -and ($msg.Length -gt 0)
        Record 'Freeze' 'Block export when frozen' $blocked "status=$status"
    }

    # Unfreeze then cancel smoke stocktake
    $updateBody = @{
        id = $created.id
        voucherCode = $created.voucherCode
        date = $created.date
        createdBy = $created.createdBy
        status = 'cancelled'
        warehouseId = $created.warehouseId
        warehouseName = $created.warehouseName
        condition = $created.condition
        isFrozen = $false
        note = 'SMOKE-TEST cancelled'
        items = $created.items
    }
    $cancelled = Invoke-Api -Method PUT -Path "/api/stocktakes/$($created.id)" -Body $updateBody
    Record 'Stocktake' 'Cancel smoke stocktake' ($cancelled.status -eq 'cancelled') $cancelled.status
} catch {
    Record 'Stocktake' 'Create flow' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== SMOKE SUMMARY ==='
$passed = @($results | Where-Object Ok).Count
$failed = @($results | Where-Object { -not $_.Ok }).Count
Write-Host "Passed: $passed | Failed: $failed | Total: $($results.Count)"
if ($failed -gt 0) {
    Write-Host ''
    Write-Host 'Failures:'
    $results | Where-Object { -not $_.Ok } | Format-Table -AutoSize
    exit 1
}
exit 0
