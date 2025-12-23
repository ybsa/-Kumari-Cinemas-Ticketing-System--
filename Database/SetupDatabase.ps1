# Database Setup Script
$User = "SYSTEM"
$Password = Read-Host -Prompt "Enter Oracle DB Password for SYSTEM" -AsSecureString
$PasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
$Service = "localhost:1521/XE"
$ConnectionString = "$User/$PasswordPlain@$Service"

Write-Host "Connecting to Oracle Database ($Service)..." -ForegroundColor Cyan

# Check if sqlplus is available
if (-not (Get-Command "sqlplus" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: 'sqlplus' command not found. Please ensure Oracle XE is installed and added to your PATH."
    exit 1
}

# Run Main Setup Script
Write-Host "Executing Main Setup Script (Recreating Schema & Data)..." -ForegroundColor Yellow
$Output = echo "exit" | sqlplus -S $ConnectionString "@setup_database.sql"
Write-Host $Output

Write-Host "Database Setup Completed Successfully!" -ForegroundColor Green
