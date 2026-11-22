#Requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [string]$BaseUrl,
    [Parameter(Mandatory)]
    [string]$Slug,
    [Parameter(Mandatory)]
    [string]$Code,
    [string]$GuestName = 'Table guest',
    [ValidateSet('DineIn', 'Takeaway')]
    [string]$ServiceType = 'DineIn'
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$guestPath = "/t/$Slug/api/v1/guest"
$failures = [Collections.Generic.List[string]]::new()

function Invoke-GuestApi {
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

$context = Invoke-GuestApi -Method Get -Path "$guestPath/qr/$Code"
Confirm-Step -Name 'scan' -Actual $context.StatusCode -Expected 200

$menu = Invoke-GuestApi -Method Get -Path "$guestPath/qr/$Code/menu?page=1&pageSize=20"
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

$device = -join (1..24 | ForEach-Object { 'abcdefghijklmnopqrstuvwxyz0123456789'[(Get-Random -Maximum 36)] })
$key = [Guid]::NewGuid().ToString('N')
$headers = @{
    'Idempotency-Key' = $key
    'X-Guest-Device' = $device
}
$body = @{
    code = $Code
    serviceType = $ServiceType
    guestDisplayName = $GuestName
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

$first = Invoke-GuestApi -Method Post -Path "$guestPath/orders" -Headers $headers -Body $body
Confirm-Step -Name 'submit' -Actual $first.StatusCode -Expected 201
$firstOrder = $first.Content | ConvertFrom-Json
Write-Output "INFO order $($firstOrder.orderNumber) reference $($firstOrder.publicReference)"
Write-Output "INFO location $($first.Headers.Location)"

$replay = Invoke-GuestApi -Method Post -Path "$guestPath/orders" -Headers $headers -Body $body
Confirm-Step -Name 'replay' -Actual $replay.StatusCode -Expected 201
$replayedOrder = $replay.Content | ConvertFrom-Json
if ($replayedOrder.orderNumber -eq $firstOrder.orderNumber -and
    $replayedOrder.publicReference -eq $firstOrder.publicReference) {
    Write-Output 'PASS replay returns the same order'
}
else {
    Write-Output 'FAIL replay returns the same order'
    $failures.Add('replay identity')
}

$body.guestDisplayName = 'Different guest'
$reused = Invoke-GuestApi -Method Post -Path "$guestPath/orders" -Headers $headers -Body $body
Confirm-Step -Name 'reused key, changed payload' -Actual $reused.StatusCode -Expected 409
if ($reused.Content -match 'Idempotency.KeyReused') {
    Write-Output 'PASS reused key code is Idempotency.KeyReused'
}
else {
    Write-Output 'FAIL reused key code is Idempotency.KeyReused'
    $failures.Add('reused key code')
}

$changedBody = @{
    code = $Code
    serviceType = $ServiceType
    guestDisplayName = $GuestName
    expectedTotal = [decimal]$size.price + 0.01
    lines = @(
        @{
            productId = $item.id
            sizeId = $size.id
            addonIds = @()
            quantity = 1
            expectedUnitPrice = [decimal]$size.price + 0.01
            expectedAddonPrices = @()
        }
    )
}
$changedKey = @{ 'Idempotency-Key' = [Guid]::NewGuid().ToString('N') }
$changedPrice = Invoke-GuestApi -Method Post -Path "$guestPath/orders" -Headers $changedKey -Body $changedBody
Confirm-Step -Name 'changed price' -Actual $changedPrice.StatusCode -Expected 409
if ($changedPrice.Content -match 'Orders.PriceChanged') {
    Write-Output 'PASS changed price code is Orders.PriceChanged'
}
else {
    Write-Output 'FAIL changed price code is Orders.PriceChanged'
    $failures.Add('changed price code')
}

if ($failures.Count -gt 0) {
    Write-Output "FAILED $($failures.Count) check(s): $($failures -join ', ')"
    exit 1
}

Write-Output 'PASSED all guest order submission checks'
exit 0
