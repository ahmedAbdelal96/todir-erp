[CmdletBinding()]
param(
  [string]$TestFile = "tests/e2e/navigation-restricted-roles-real-stack.spec.ts",
  [switch]$KeepDatabase
)

$ErrorActionPreference = "Stop"

if (-not [string]::Equals($env:TODIR_ACCEPTANCE_DISPOSABLE, "true", [StringComparison]::OrdinalIgnoreCase)) {
  throw "Set TODIR_ACCEPTANCE_DISPOSABLE=true to authorize the disposable restricted-role acceptance fixture."
}

if ([string]::IsNullOrWhiteSpace($env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD)) {
  throw "Set TODIR_RESTRICTED_ACCEPTANCE_PASSWORD before running the restricted-role acceptance fixture."
}

$container = if ([string]::IsNullOrWhiteSpace($env:TODIR_POSTGRES_CONTAINER)) { "todir-postgres-1" } else { $env:TODIR_POSTGRES_CONTAINER }
$database = "restricted_acceptance_$([Guid]::NewGuid().ToString('N'))"
if ($database -notmatch '^restricted_acceptance_[0-9a-f]{32}$') {
  throw "Refusing to operate on an invalid disposable database identifier."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$databaseCreated = $false
$previousDatabaseConnection = $env:TODIR_DATABASE_CONNECTION
$previousFixturePassword = $env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD

function Invoke-ContainerPsql {
  param(
    [Parameter(Mandatory = $true)][string]$DatabaseName,
    [Parameter(Mandatory = $true)][string]$Sql
  )

  $result = $Sql | docker exec -i $container psql -U postgres -d $DatabaseName -v ON_ERROR_STOP=1
  if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL fixture command failed for '$DatabaseName'."
  }
  return $result
}

function Get-ContainerScalar {
  param(
    [Parameter(Mandatory = $true)][string]$DatabaseName,
    [Parameter(Mandatory = $true)][string]$Sql
  )

  $result = docker exec $container psql -U postgres -d $DatabaseName -Atc $Sql
  if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL fixture lookup failed for '$DatabaseName'."
  }
  return ($result | Select-Object -First 1).Trim()
}

function Invoke-ProcessWithEnvironment {
  param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string[]]$ArgumentList,
    [Parameter(Mandatory = $true)][hashtable]$Environment
  )

  $saved = @{}
  try {
    foreach ($entry in $Environment.GetEnumerator()) {
      $saved[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
      [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, "Process")
    }
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
      throw "Process '$FilePath' failed with exit code $LASTEXITCODE."
    }
  }
  finally {
    foreach ($entry in $Environment.GetEnumerator()) {
      [Environment]::SetEnvironmentVariable($entry.Key, $saved[$entry.Key], "Process")
    }
  }
}

try {
  Invoke-ContainerPsql -DatabaseName "postgres" -Sql "CREATE DATABASE $database ENCODING 'UTF8' LC_COLLATE 'C' LC_CTYPE 'C' TEMPLATE template0;" | Out-Null
  $databaseCreated = $true

  $connection = "Host=127.0.0.1;Port=15432;Database=$database;Username=postgres;Password=postgres;Include Error Detail=true"
  Invoke-ProcessWithEnvironment -FilePath "dotnet" -ArgumentList @("backend/DbMigrator/bin/Debug/net10.0/DbMigrator.dll", "--seed", "development") -Environment @{
    TODIR_MIGRATOR_CONNECTION = $connection
    TODIR_REPOSITORY_ROOT = $repositoryRoot
    TODIR_RUNTIME_PASSWORD = "todir-app-dev"
    SEED_DEFAULT_PASSWORD = $env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD
    DOTNET_ENVIRONMENT = "Testing"
  }

  $hash = Get-ContainerScalar -DatabaseName $database -Sql "select password_hash from platform.identity where email='admin@atlas.com';"
  $tenant = Get-ContainerScalar -DatabaseName $database -Sql "select id from platform.tenant where slug='atlas-trading';"
  $entity = Get-ContainerScalar -DatabaseName $database -Sql "select id from platform.legal_entity where tenant_id='$tenant' and code='MAIN';"
  $hrId = [Guid]::NewGuid(); $hrMembership = [Guid]::NewGuid(); $hrRole = [Guid]::NewGuid()
  $payrollId = [Guid]::NewGuid(); $payrollMembership = [Guid]::NewGuid(); $payrollRole = [Guid]::NewGuid()
  $projectsId = [Guid]::NewGuid(); $projectsMembership = [Guid]::NewGuid(); $projectsRole = [Guid]::NewGuid()
  $financeId = [Guid]::NewGuid(); $financeMembership = [Guid]::NewGuid(); $financeRole = [Guid]::NewGuid()

  $fixtureSql = @"
set role erp_owner;
set app.tenant_id = '$tenant';
insert into platform.identity (id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values
('$hrId','$tenant','acceptance-hr@atlas.com','ACCEPTANCE-HR@ATLAS.COM','acceptance-hr','Restricted HR Employee',true,'$hash',false,'Active',now(),gen_random_uuid(),1),
('$payrollId','$tenant','acceptance-payroll@atlas.com','ACCEPTANCE-PAYROLL@ATLAS.COM','acceptance-payroll','Restricted Payroll Employee',true,'$hash',false,'Active',now(),gen_random_uuid(),1),
('$projectsId','$tenant','acceptance-projects@atlas.com','ACCEPTANCE-PROJECTS@ATLAS.COM','acceptance-projects','Restricted Projects Operator',true,'$hash',false,'Active',now(),gen_random_uuid(),1),
('$financeId','$tenant','acceptance-finance@atlas.com','ACCEPTANCE-FINANCE@ATLAS.COM','acceptance-finance','Restricted Finance Operator',true,'$hash',false,'Active',now(),gen_random_uuid(),1);
insert into platform.membership (id,tenant_id,identity_id,display_name,status,default_legal_entity_id,locale,timezone,created_at,version) values
('$hrMembership','$tenant','$hrId','Restricted HR Employee','Active','$entity','en-US','Africa/Cairo',now(),1),
('$payrollMembership','$tenant','$payrollId','Restricted Payroll Employee','Active','$entity','en-US','Africa/Cairo',now(),1),
('$projectsMembership','$tenant','$projectsId','Restricted Projects Operator','Active','$entity','en-US','Africa/Cairo',now(),1),
('$financeMembership','$tenant','$financeId','Restricted Finance Operator','Active','$entity','en-US','Africa/Cairo',now(),1);
insert into platform.role (id,tenant_id,code,name,is_system_template,is_active,version) values
('$hrRole','$tenant','acceptance-hr-self','Restricted HR Employee',false,true,1),
('$payrollRole','$tenant','acceptance-payroll-self','Restricted Payroll Employee',false,true,1),
('$projectsRole','$tenant','acceptance-project-operator','Restricted Projects Operator',false,true,1),
('$financeRole','$tenant','acceptance-finance-operator','Restricted Finance Operator',false,true,1);
insert into platform.role_permission (tenant_id,role_id,permission_code) values
('$tenant','$hrRole','platform.organization.read'),('$tenant','$hrRole','hr.employee.read_self'),('$tenant','$hrRole','hr.employee.document.read_self'),('$tenant','$hrRole','hr.attendance.read_self'),('$tenant','$hrRole','hr.leave.read_self'),('$tenant','$hrRole','hr.leave.request_self'),
('$tenant','$payrollRole','platform.organization.read'),('$tenant','$payrollRole','payroll.payslip.read_self'),
('$tenant','$projectsRole','platform.organization.read'),('$tenant','$projectsRole','projects.manage_team'),
('$tenant','$financeRole','platform.organization.read'),('$tenant','$financeRole','expenses.read'),('$tenant','$financeRole','expenses.create'),('$tenant','$financeRole','expenses.submit'),('$tenant','$financeRole','payments.read');
insert into platform.membership_role_assignment (tenant_id,membership_id,role_id,scope_type,scope_id) values
('$tenant','$hrMembership','$hrRole','LegalEntity','$entity'),('$tenant','$payrollMembership','$payrollRole','LegalEntity','$entity'),('$tenant','$projectsMembership','$projectsRole','LegalEntity','$entity'),('$tenant','$financeMembership','$financeRole','LegalEntity','$entity');
reset app.tenant_id;
reset role;
"@
  Invoke-ContainerPsql -DatabaseName $database -Sql $fixtureSql | Out-Null

  $env:TODIR_DATABASE_CONNECTION = "Host=127.0.0.1;Port=15432;Database=$database;Username=erp_app;Password=todir-app-dev;Include Error Detail=true"
  Invoke-ProcessWithEnvironment -FilePath "pnpm" -ArgumentList @("--dir", "frontend", "exec", "playwright", "test", $TestFile, "--config=playwright.config.ts", "--reporter=line") -Environment @{
    TODIR_DATABASE_CONNECTION = $env:TODIR_DATABASE_CONNECTION
    TODIR_RESTRICTED_ACCEPTANCE_PASSWORD = $env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD
  }
}
finally {
  $env:TODIR_DATABASE_CONNECTION = $previousDatabaseConnection
  $env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD = $previousFixturePassword

  if ($databaseCreated -and -not $KeepDatabase) {
    $dropped = $false
    for ($attempt = 1; $attempt -le 10 -and -not $dropped; $attempt++) {
      try {
        Invoke-ContainerPsql -DatabaseName "postgres" -Sql "DROP DATABASE $database;" | Out-Null
        $dropped = $true
      }
      catch {
        if ($attempt -eq 10) { throw "The disposable database '$database' could not be dropped without terminating connections." }
        Start-Sleep -Seconds 1
      }
    }
  }
  elseif ($databaseCreated) {
    Write-Host "Kept disposable acceptance database: $database"
  }
}
