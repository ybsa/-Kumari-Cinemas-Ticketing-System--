# Database Setup Script
$User = "SYSTEM"
$Password = "windxebec@2129"
$Service = "localhost:1521/XE"
$ConnectionString = "$User/$Password@$Service"

Write-Host "Connecting to Oracle Database ($Service)..." -ForegroundColor Cyan

# Check if sqlplus is available
if (-not (Get-Command "sqlplus" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: 'sqlplus' command not found. Please ensure Oracle XE is installed and added to your PATH."
    exit 1
}

# Run Schema Script
Write-Host "Executing Schema Script..." -ForegroundColor Yellow
$SchemaOutput = echo "exit" | sqlplus -S $ConnectionString "@database_schema.sql"
Write-Host $SchemaOutput

# Run Sample Data Script
Write-Host "Executing Sample Data Script..." -ForegroundColor Yellow
$DataOutput = echo "exit" | sqlplus -S $ConnectionString "@sample_data.sql"
Write-Host $DataOutput

Write-Host "Database Setup Completed!" -ForegroundColor Green
