param([Parameter(Mandatory)][string[]]$Ids)
$taskPath = Join-Path $PSScriptRoot '../specs/001-core-ticket-management/tasks.md'
$taskContent = [IO.File]::ReadAllText($taskPath)
foreach ($taskId in $Ids) {
    if ($taskId -notmatch '^T\d{3}$' -or [int]$taskId.Substring(1) -gt 81) { throw 'Outside authorized task scope.' }
    $taskContent = $taskContent.Replace("- [ ] $taskId ", "- [X] $taskId ")
}
[IO.File]::WriteAllText($taskPath, $taskContent)
