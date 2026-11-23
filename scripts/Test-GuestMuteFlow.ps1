#Requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [string]$BaseUrl,
    [Parameter(Mandatory)]
    [string]$Slug,
    [Parameter(Mandatory)]
    [string]$Code,
    [Parameter(Mandatory)]
    [string]$AccessToken,
    [string]$GuestDevice = 'manual-check-device-0001',
    [string]$Reason = 'Manual check: repeated rejected orders',
    [ValidateRange(1, 168)]
    [int]$ExpiryHours = 1
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$guestPath = "/t/$Slug/api/v1/guest"
$ordersPath = "/t/$Slug/api/v1/orders"
$mutesPath = "/t/$Slug/api/v1/guest-mutes"
$staffHeaders = @{ Authorization = "Bearer $AccessToken" }
$failures = [Collections.Generic.List[string]]::new()

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $parameters = @{
        Method = $Method
        Uri = "$base$Path"
        Headers = $Headers
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $parameters['ContentType'] = 'application/json'
        $parameters['Body'] = $Body | ConvertTo-Json -Depth 8
    }

    Invoke-WebRequest @parameters
}

function Confirm-Step {
    param(
        [string]$Name,
        [int]$Actual,
        [int]$Expected
    )

    if ($Actual -eq $Expected) {
        Write-Output "PASS $Name ($Actual)"
        return
    }

    Write-Output "FAIL $Name (expected $Expected, received $Actual)"
    $failures.Add($Name)
}

function Confirm-True {
    param(
        [string]$Name,
        [bool]$Condition
    )

    if ($Condition) {
        Write-Output "PASS $Name"
        return
    }

    Write-Output "FAIL $Name"
    $failures.Add($Name)
}

$menu = Invoke-Api -Method Get -Path "$guestPath/qr/$Code/menu?page=1&pageSize=20"
Confirm-Step -Name 'menu' -Actual $menu.StatusCode -Expected 200

$item = ($menu.Content | ConvertFrom-Json).items |
    Where-Object { $_.sizes.Count -gt 0 } |
    Select-Object -First 1
if (-not $item) {
    Write-Output 'FAIL submit (the menu has no product with a size to order)'
    exit 1
}

$size = $item.sizes | Where-Object { $_.isDefault } | Select-Object -First 1
if (-not $size) {
    $size = $item.sizes[0]
}

$body = @{
    code = $Code
    serviceType = 'DineIn'
    guestDisplayName = 'Mute check guest'
    expectedTotal = [decimal]$size.price
    lines = @(
        @{
            productId = $item.id
            sizeId = $size.id
            addonIds = @()
            quantity = 1
            expectedUnitPrice = [decimal]$size.price
            expectedAddonPrices = @()
        }
    )
}

$orderNumbers = [Collections.Generic.List[int]]::new()
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $headers = @{
        'Idempotency-Key' = [Guid]::NewGuid().ToString('N')
        'X-Guest-Device' = $GuestDevice
    }
    $submission = Invoke-Api -Method Post -Path "$guestPath/orders" -Headers $headers -Body $body
    Confirm-Step -Name "submit order $attempt" -Actual $submission.StatusCode -Expected 201
    $orderNumbers.Add([int](($submission.Content | ConvertFrom-Json).orderNumber))
}

$orderIds = [Collections.Generic.List[Guid]]::new()
foreach ($orderNumber in $orderNumbers) {
    $feed = Invoke-Api -Method Get -Path "$ordersPath?status=Pending&pageSize=100" -Headers $staffHeaders
    Confirm-Step -Name "pending feed for $orderNumber" -Actual $feed.StatusCode -Expected 200
    $pending = ($feed.Content | ConvertFrom-Json).items |
        Where-Object { $_.orderNumber -eq $orderNumber } |
        Select-Object -First 1
    if (-not $pending) {
        Write-Output "FAIL order $orderNumber is missing from the pending feed"
        $failures.Add("pending order $orderNumber")
        continue
    }

    $rejection = Invoke-Api -Method Post -Path "$ordersPath/$($pending.id)/reject" -Headers $staffHeaders -Body @{
        reason = 'Manual check rejection'
    }
    Confirm-Step -Name "reject order $orderNumber" -Actual $rejection.StatusCode -Expected 200
    $orderIds.Add([Guid]$pending.id)
}

if ($orderIds.Count -ne 3) {
    Write-Output 'FAIL mute check needs three rejected orders'
    exit 1
}

$rejected = Invoke-Api -Method Get -Path "$ordersPath?status=Rejected&pageSize=100" -Headers $staffHeaders
Confirm-Step -Name 'rejected feed' -Actual $rejected.StatusCode -Expected 200
$rejectedItems = ($rejected.Content | ConvertFrom-Json).items |
    Where-Object { $orderIds -contains [Guid]$_.id }
Confirm-True -Name 'three rejected orders are visible' -Condition ($rejectedItems.Count -eq 3)
Confirm-True -Name 'the feed suggests a mute after three rejections' -Condition (
    @($rejectedItems | Where-Object { $_.suggestMute }).Count -ge 3)

$muteTargetId = $orderIds[2]
$mute = Invoke-Api -Method Post -Path $mutesPath -Headers $staffHeaders -Body @{
    orderId = $muteTargetId
    reason = $Reason
    expiryHours = $ExpiryHours
}
Confirm-Step -Name 'mute' -Actual $mute.StatusCode -Expected 201
$muteRecord = $mute.Content | ConvertFrom-Json
Confirm-True -Name 'the mute names the acting staff member' -Condition (
    -not [string]::IsNullOrWhiteSpace($muteRecord.mutedByName))
Write-Output "INFO mute $($muteRecord.id) until $($muteRecord.expiresAt)"

$defaultFeed = Invoke-Api -Method Get -Path "$ordersPath?pageSize=100" -Headers $staffHeaders
Confirm-Step -Name 'default feed' -Actual $defaultFeed.StatusCode -Expected 200
$hiddenIds = ($defaultFeed.Content | ConvertFrom-Json).items | ForEach-Object { [Guid]$_.id }
Confirm-True -Name 'the default feed hides the muted device orders' -Condition (
    @($orderIds | Where-Object { $hiddenIds -contains $_ }).Count -eq 0)

$mutedFeed = Invoke-Api -Method Get -Path "$ordersPath?pageSize=100&includeMuted=true" -Headers $staffHeaders
Confirm-Step -Name 'feed with includeMuted' -Actual $mutedFeed.StatusCode -Expected 200
$mutedIds = ($mutedFeed.Content | ConvertFrom-Json).items | ForEach-Object { [Guid]$_.id }
Confirm-True -Name 'includeMuted returns the muted device orders' -Condition (
    @($orderIds | Where-Object { $mutedIds -contains $_ }).Count -eq 3)

$lift = Invoke-Api -Method Post -Path "$mutesPath/$($muteRecord.id)/lift" -Headers $staffHeaders
Confirm-Step -Name 'lift' -Actual $lift.StatusCode -Expected 200
Confirm-True -Name 'the lift records a time' -Condition ($null -ne ($lift.Content | ConvertFrom-Json).liftedAt)

$restoredFeed = Invoke-Api -Method Get -Path "$ordersPath?pageSize=100" -Headers $staffHeaders
$restoredIds = ($restoredFeed.Content | ConvertFrom-Json).items | ForEach-Object { [Guid]$_.id }
Confirm-True -Name 'the default feed returns the orders after the lift' -Condition (
    @($orderIds | Where-Object { $restoredIds -contains $_ }).Count -eq 3)

$register = Invoke-Api -Method Get -Path "$mutesPath?active=false&pageSize=100" -Headers $staffHeaders
Confirm-Step -Name 'register' -Actual $register.StatusCode -Expected 200
$registerIds = ($register.Content | ConvertFrom-Json).items | ForEach-Object { [Guid]$_.id }
Confirm-True -Name 'the register lists the lifted mute' -Condition ($registerIds -contains [Guid]$muteRecord.id)

$today = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
$summary = Invoke-Api -Method Get -Path "$mutesPath/summary?from=$today&to=$today" -Headers $staffHeaders
Confirm-Step -Name 'summary' -Actual $summary.StatusCode -Expected 200
$summaryRows = @($summary.Content | ConvertFrom-Json)
Confirm-True -Name 'the summary counts the mute per acting staff member' -Condition (
    @($summaryRows | Where-Object { $_.muteCount -ge 1 }).Count -ge 1)

if ($failures.Count -gt 0) {
    Write-Output "FAILED $($failures.Count) check(s): $($failures -join ', ')"
    exit 1
}

Write-Output 'PASSED all guest mute checks'
exit 0
