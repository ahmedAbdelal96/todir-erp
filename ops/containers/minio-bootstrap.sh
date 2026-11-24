#!/bin/sh
set -eu

mc alias set root "$MINIO_ROOT_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
mc mb --ignore-existing root/private-files
mc anonymous set none root/private-files

if ! mc admin policy info root private-files-app >/dev/null 2>&1; then
  mc admin policy create root private-files-app /tmp/private-files-policy.json >/dev/null
fi

if ! mc admin user info root "$MINIO_APP_ACCESS_KEY" >/dev/null 2>&1; then
  mc admin user add root "$MINIO_APP_ACCESS_KEY" "$MINIO_APP_PASSWORD" >/dev/null
fi

mc admin policy attach root private-files-app --user "$MINIO_APP_ACCESS_KEY" >/dev/null
mc alias set app "$MINIO_ROOT_ENDPOINT" "$MINIO_APP_ACCESS_KEY" "$MINIO_APP_PASSWORD" >/dev/null
mc ls app/private-files >/dev/null
