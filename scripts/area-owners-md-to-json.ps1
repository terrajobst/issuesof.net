$owner = "dotnet"
$repo = "runtime"
$markdown = (Invoke-WebRequest -Uri "https://raw.githubusercontent.com/$owner/$repo/main/docs/area-owners.md").Content -Split "\n"

Function Parse-LabelLine {
    param ([String]$line)

    $cols = $line -split "\|"

    $label = $cols[1].Trim()
    $lead = $cols[2].Trim().Replace("@", "")
    $owners = $cols[3].Trim().Replace("@", "").Replace(",", "") -split " "

    $people = $owners -notmatch "$owner/"
    $teams = $owners -match "$owner/"
    $teamMembers = @()

    foreach ($team in $teams) {
        $teamParts = $team -split "/"
        $org = $teamParts[0]
        $teamName = $teamParts[1]

        Write-Host "Loading members of team $org/$teamName"

        try {
            $teamMembers = (gh api orgs/$org/teams/$teamName/members | ConvertFrom-Json).login
        }
        catch {
            Write-Host "Could not load team members for $org/$teamName"
        }
    }

    @{
        label = [String]$label;
        lead = [String]$lead;
        owners = [String[]]((($people + $teamMembers) | Sort-Object -Unique) + $teams)
    }
}

[Object[]]$areas = ($markdown | Select-String -Pattern "^\|\s*area-").Line | Foreach-Object -Process {Parse-LabelLine $_}
[Object[]]$operatingSystems = ($markdown | Select-String -Pattern "^\|\s*os-").Line | Foreach-Object -Process {Parse-LabelLine $_}
[Object[]]$architectures = ($markdown | Select-String -Pattern "^\|\s*arch-").Line | Foreach-Object -Process {Parse-LabelLine $_}

$data = @{ areas = $areas; operatingSystems = $operatingSystems; architectures = $architectures }
$data | ConvertTo-Json -Depth 5 > area-owners.json
