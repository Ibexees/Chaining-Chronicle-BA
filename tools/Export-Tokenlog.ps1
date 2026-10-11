<#
.SYNOPSIS
  Exports the token usage per user prompt of a Claude Code session as CSV (thesis requirement M3).

.DESCRIPTION
  Reads the session transcript (~/.claude/projects/<project>/<session>.jsonl) and sums the API usage
  of all model responses that belong to one user prompt. Responses streamed in several records are
  counted once (by message id). Tool results and system messages do not start a new prompt.

  Columns: Input_neu = uncached input, Cache_geschrieben / Cache_gelesen = prompt cache writes/reads,
  Input_gesamt = sum of the three (every model call re-reads the whole context, so this is processed
  tokens, not unique tokens), Output = generated tokens including thinking. Dauer_min = prompt to last
  response, includes tool run time but not waiting for the next prompt.

.PARAMETER Transcript
  Session transcript. Default: the newest .jsonl of this project.

.PARAMETER OutCsv
  Target file. Default: Assets/Docs/Tokenlog.csv

.PARAMETER SkipLastPrompt
  Leaves out the last prompt, e.g. the one that runs the export and is not finished yet.
#>
param(
    [string]$Transcript,
    [string]$OutCsv,
    [switch]$SkipLastPrompt
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutCsv) { $OutCsv = Join-Path $repo 'Assets\Docs\Tokenlog.csv' }
if (-not $Transcript) {
    $projectKey = ($repo -replace '[:\\/_ ]', '-')
    $dir = Join-Path $env:USERPROFILE ".claude\projects\$projectKey"
    $Transcript = (Get-ChildItem $dir -Filter *.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
Write-Host "Transcript: $Transcript"
$culture = [Globalization.CultureInfo]::InvariantCulture

function Get-PromptText($content) {
    if ($content -is [string]) { $t = $content }
    else {
        $parts = @()
        foreach ($b in $content) { if ($b.type -eq 'text' -and $b.text) { $parts += $b.text } }
        $t = $parts -join "`n"
    }
    foreach ($tag in 'system-reminder', 'local-command-caveat', 'local-command-stdout', 'command-name', 'command-message', 'command-args', 'pasted_content') {
        $t = [regex]::Replace($t, "(?s)<$tag[^>]*>.*?</$tag[^>]*>", '')
    }
    return $t.Trim()
}

function Test-ToolResult($content) {
    if ($content -is [string]) { return $false }
    foreach ($b in $content) { if ($b.type -eq 'tool_result') { return $true } }
    return $false
}

$prompts = New-Object System.Collections.Generic.List[object]
$current = $null
$seen = @{}   # message id -> record index in current prompt (dedupe streamed duplicates)

foreach ($line in [IO.File]::ReadLines($Transcript)) {
    if (-not $line.Trim()) { continue }
    try { $r = $line | ConvertFrom-Json } catch { continue }

    if ($r.type -eq 'user' -and $r.message -and -not $r.isMeta -and -not (Test-ToolResult $r.message.content)) {
        $text = Get-PromptText $r.message.content
        if ($text -and $text -notmatch '^(?i)tool loaded\.?$') {
            $current = [pscustomobject]@{
                Index = $prompts.Count + 1
                Start = [datetime]::Parse($r.timestamp, $culture, 'RoundtripKind')
                End = $null
                Text = ($text -replace '\s+', ' ')
                Calls = @{}
            }
            $prompts.Add($current)
        }
        continue
    }

    if ($r.type -eq 'assistant' -and $r.message -and $r.message.usage -and $current -and $r.message.model -ne '<synthetic>') {
        $u = $r.message.usage
        $id = $r.message.id
        $rec = [pscustomobject]@{
            In = [long]($u.input_tokens)
            Out = [long]($u.output_tokens)
            CacheWrite = [long]($u.cache_creation_input_tokens)
            CacheRead = [long]($u.cache_read_input_tokens)
        }
        if ($current.Calls.ContainsKey($id)) {
            # same API response streamed in several records: keep the largest output count
            $old = $current.Calls[$id]
            if ($rec.Out -lt $old.Out) { $rec.Out = $old.Out }
        }
        $current.Calls[$id] = $rec
        $ts = [datetime]::Parse($r.timestamp, $culture, 'RoundtripKind')
        if (-not $current.End -or $ts -gt $current.End) { $current.End = $ts }
    }
}

if ($SkipLastPrompt -and $prompts.Count -gt 0) { $prompts.RemoveAt($prompts.Count - 1) }

$rows = foreach ($p in $prompts) {
    $in = 0L; $out = 0L; $cw = 0L; $cr = 0L
    foreach ($c in $p.Calls.Values) { $in += $c.In; $out += $c.Out; $cw += $c.CacheWrite; $cr += $c.CacheRead }
    $end = if ($p.End) { $p.End } else { $p.Start }
    $summary = if ($p.Text.Length -gt 70) { $p.Text.Substring(0, 70) + '...' } else { $p.Text }
    [pscustomobject][ordered]@{
        Nr = $p.Index
        Start_lokal = $p.Start.ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss')
        Ende_lokal = $end.ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss')
        Dauer_min = [math]::Round(($end - $p.Start).TotalMinutes, 1)
        API_Aufrufe = $p.Calls.Count
        Input_neu = $in
        Cache_geschrieben = $cw
        Cache_gelesen = $cr
        Input_gesamt = $in + $cw + $cr
        Output = $out
        Prompt = $summary
    }
}

$rows | Export-Csv -Path $OutCsv -NoTypeInformation -Encoding UTF8 -Delimiter ';'
$rows | Format-Table Nr, Start_lokal, Dauer_min, API_Aufrufe, Input_neu, Cache_geschrieben, Cache_gelesen, Output -AutoSize | Out-String -Width 200
$t = $rows | Measure-Object Input_neu, Cache_geschrieben, Cache_gelesen, Input_gesamt, Output, API_Aufrufe -Sum
foreach ($m in $t) { "{0}: {1:N0}" -f $m.Property, $m.Sum }
