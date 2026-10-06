param(
    [Parameter(Mandatory = $true)][string]$Container,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$BackupPath
)

$restoreDatabase = "${Database}_restore_verify"
docker exec $Container dropdb --if-exists --username=postgres $restoreDatabase
docker exec $Container createdb --username=postgres $restoreDatabase
docker cp $BackupPath "${Container}:/tmp/platform-backup.dump"
docker exec $Container pg_restore --username=postgres --exit-on-error --dbname=$restoreDatabase /tmp/platform-backup.dump
docker exec $Container psql --username=postgres --dbname=$restoreDatabase --command="select count(*) from platform_tenancy.tenant"
docker exec $Container rm --force /tmp/platform-backup.dump
docker exec $Container dropdb --username=postgres $restoreDatabase
