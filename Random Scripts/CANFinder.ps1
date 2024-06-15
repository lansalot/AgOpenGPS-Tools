$unique = @()
ForEach ($csv in (dir *.csv |where {$_.name -notmatch 'summary'})) {
    Write-Output "parsing $($csv.name)"
    $imported = import-csv $csv
    $unique += $imported | Group-Object id | select Name,Count
}

 # $CANIDs = $unique| group-object name|select name, count
$current = 1
$Output = @()
ForEach ($ID in $unique) {
    Write-Progress "Progress" -percentcomplete ($current / $CANIDs.count * 100)
    Write-Output "Checking $($ID.Name)"
    $api = irm "https://www.decoda.cc/v1/decode/canid/$($ID.Name)"
    $Output += [PSCustomObject]@{
        CANID = $ID.Name
        daName = $api.da.Name
        daNum = $api.da.num
        pgnID = $api.pgn.id
        pgnHexID = '0x{0:x}' -f $api.pgn.id
        pgnName = $api.pgn.name
        priority = $api.priority
        saName = $api.sa.name
        saID = $api.sa.num
        saHex = '0x{0:x}' -f $api.sa.num
        pgnDesc = $api.pgn.description
        count = $ID.count
    }
    $current++
    Start-Sleep -Seconds 5 # rate limited, 15 calls per minute
}

$Output | export-csv summary.csv -notypeinformation