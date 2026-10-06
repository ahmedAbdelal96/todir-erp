param(
    [Parameter(Mandatory = $true)][string]$Container,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

docker exec $Container pg_dump --username=postgres --format=custom --file=/tmp/platform-backup.dump $Database
docker cp "${Container}:/tmp/platform-backup.dump" $OutputPath
docker exec $Container rm --force /tmp/platform-backup.dump
