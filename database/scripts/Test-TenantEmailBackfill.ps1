param(
    [Parameter(Mandatory)]
    [string]$InventoryPath
)

$ErrorActionPreference = 'Stop'
try {
    $inventory = @(Import-Csv -LiteralPath $InventoryPath -Encoding utf8 -WarningAction Stop)
}
catch {
    [Console]::Error.WriteLine('The tenant email inventory could not be read.')
    exit 1
}

$groups = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new([StringComparer]::Ordinal)
$rowDigests = [Collections.Generic.SortedDictionary[string, byte[]]]::new([StringComparer]::Ordinal)
$invalid = [Collections.Generic.List[string]]::new()
$parity = [Collections.Generic.List[string]]::new()

foreach ($row in $inventory) {
    $userId = [Guid]::Empty
    if (-not [Guid]::TryParse($row.id, [ref]$userId) -or
        @('email', 'sql_backfill').Where({ $null -eq $row.PSObject.Properties[$_] }).Count) {
        [Console]::Error.WriteLine('The tenant email inventory has an invalid ID or missing column.')
        exit 1
    }

    $id = $userId.ToString('D')
    if ([string]::IsNullOrWhiteSpace($row.email)) {
        $invalid.Add($id)
        continue
    }

    $canonical = $row.email.Trim().ToLowerInvariant()
    $at = $canonical.IndexOf('@')
    if ($row.email.Length -gt 254 -or $at -le 0 -or $at -eq $canonical.Length - 1 -or $at -ne $canonical.LastIndexOf('@')) {
        $invalid.Add($id)
    }
    if (-not [string]::Equals($canonical, $row.sql_backfill, [StringComparison]::Ordinal)) {
        $parity.Add($id)
    }
    if (-not $groups.ContainsKey($canonical)) {
        $groups.Add($canonical, [Collections.Generic.List[string]]::new())
    }
    $groups[$canonical].Add($id)

    if ($rowDigests.ContainsKey($id)) {
        [Console]::Error.WriteLine('The tenant email inventory contains a repeated ID.')
        exit 1
    }
    $rowBytes = [Text.Encoding]::UTF8.GetBytes($id + $row.email + [char]0 + $canonical)
    $rowDigests.Add($id, [Security.Cryptography.SHA256]::HashData($rowBytes))
}

$duplicates = @($groups.Values | Where-Object Count -gt 1 | ForEach-Object { $_ -join ', ' })
$emailProof = $null
if (-not ($duplicates.Count -or $invalid.Count -or $parity.Count)) {
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($digest in $rowDigests.Values) {
            $hash.AppendData($digest)
        }
        $emailProof = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
    }
}

[pscustomobject]@{
    DuplicateGroupCount = $duplicates.Count
    DuplicateIdGroups = $duplicates
    InvalidCount = $invalid.Count
    InvalidIds = $invalid.ToArray()
    BackfillMismatchCount = $parity.Count
    BackfillMismatchIds = $parity.ToArray()
    EmailProof = $emailProof
} | ConvertTo-Json -Depth 3

if ($null -eq $emailProof) {
    exit 1
}
