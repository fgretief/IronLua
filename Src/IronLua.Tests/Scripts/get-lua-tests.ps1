# Make sure the package is installed
if (-not (Get-Command Expand-7Zip -ErrorAction Ignore)) {
    Install-Package -Scope CurrentUser -Force 7Zip4PowerShell > $null
}

$wc = New-Object System.Net.WebClient

$url = "https://www.lua.org/tests/lua-5.3.4-tests.tar.gz"
$output = "$PSScriptRoot\lua-5.3.4-tests.tar.gz"
$wc.DownloadFile($url, $output)
#Expand-7Zip -ArchiveFileName "$output" -TargetPath $PSScriptRoot

$url = "https://www.lua.org/tests/lua-5.2.2-tests.tar.gz"
$output = "$PSScriptRoot\lua-5.2.2-tests.tar.gz"
$wc.DownloadFile($url, $output)
#Expand-7Zip -ArchiveFileName "$output" -TargetPath $PSScriptRoot

$url = "https://www.lua.org/tests/lua5.1-tests.tar.gz"
$output = "$PSScriptRoot\lua-5.1-tests.tar.gz"
$wc.DownloadFile($url, $output)
#Expand-7Zip -ArchiveFileName "$output" -TargetPath $PSScriptRoot
