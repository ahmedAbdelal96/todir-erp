locals {
  project = "kernel-${var.image_tag}"
}

resource "docker_volume" "data_protection" {
  name = "${local.project}-data-protection"
}

resource "docker_network" "private" {
  name = "${local.project}-private"
}

resource "docker_container" "postgres" {
  name  = "${local.project}-postgres"
  image = "postgres:18"
  env = [
    "POSTGRES_USER=postgres",
    "POSTGRES_PASSWORD=${var.database_password}",
    "POSTGRES_DB=${var.database_name}",
  ]
  healthcheck {
    test     = ["CMD-SHELL", "pg_isready -U postgres -d ${var.database_name}"]
    interval = "5s"
    timeout  = "5s"
    retries  = 12
  }
  networks_advanced {
    name = docker_network.private.name
  }
}

resource "docker_container" "rabbitmq" {
  name  = "${local.project}-rabbitmq"
  image = "rabbitmq:4-management"
  # The image entrypoint drops to the rabbitmq account after fixing ownership
  # of the mounted TLS material; the container never runs the broker as root.
  user = "0"
  env = [
    "RABBITMQ_DEFAULT_USER=todir",
    "RABBITMQ_DEFAULT_PASS=${var.rabbit_password}",
    "RABBITMQ_DEFAULT_VHOST=/erp-staging",
  ]
  healthcheck {
    test     = ["CMD-SHELL", "rabbitmq-diagnostics -q ping"]
    interval = "5s"
    timeout  = "5s"
    retries  = 12
  }
  volumes {
    host_path      = var.rabbit_tls_directory
    container_path = "/etc/rabbitmq/tls"
    read_only      = true
  }
  volumes {
    host_path      = abspath("${path.root}/../containers/rabbitmq.conf")
    container_path = "/etc/rabbitmq/rabbitmq.conf"
    read_only      = true
  }
  networks_advanced {
    name    = docker_network.private.name
    aliases = ["rabbitmq"]
  }
}

resource "docker_container" "minio" {
  name  = "${local.project}-minio"
  image = "minio/minio:RELEASE.2024-06-13T22-53-53Z"
  command = ["server", "/data", "--console-address", ":9001"]
  env = [
    "MINIO_ROOT_USER=todir",
    "MINIO_ROOT_PASSWORD=${var.rabbit_password}",
  ]
  networks_advanced {
    name = docker_network.private.name
  }
}

resource "docker_container" "minio_init" {
  name       = "${local.project}-minio-init"
  image      = "minio/mc:RELEASE.2024-06-13T22-53-53Z"
  entrypoint = ["sh", "-c"]
  command = [
    "until mc alias set local http://${docker_container.minio.name}:9000 todir ${var.rabbit_password}; do sleep 1; done; mc mb --ignore-existing local/private-files; mc anonymous set none local/private-files",
  ]
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.minio]
  must_run   = false
}

resource "docker_container" "migrator" {
  name  = "${local.project}-migrator"
  image = var.migrator_image
  env = [
    "TODIR_MIGRATOR_CONNECTION=Host=${docker_container.postgres.name};Database=${var.database_name};Username=postgres;Password=${var.database_password}",
    "TODIR_RUNTIME_PASSWORD=${var.database_password}",
    "TODIR_REPOSITORY_ROOT=/app",
  ]
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.postgres]
}

resource "docker_container" "api" {
  name  = "${local.project}-api"
  image = var.api_image
  env = [
    "ASPNETCORE_ENVIRONMENT=${var.environment_name}",
    "TODIR_DATA_PROTECTION_KEYS=/app/keys",
    "TODIR_DATA_PROTECTION_CERTIFICATE_BASE64=${var.data_protection_certificate_base64}",
    "TODIR_DATA_PROTECTION_CERTIFICATE_PASSWORD=${var.data_protection_certificate_password}",
    "TODIR_DB_COMMAND_TIMEOUT_SECONDS=15",
    "TODIR_DATABASE_CONNECTION=Host=${docker_container.postgres.name};Database=${var.database_name};Username=erp_app;Password=${var.database_password}",
    "TODIR_RABBITMQ_HOST=${docker_container.rabbitmq.name}",
    "TODIR_RABBITMQ_PORT=5671",
    "TODIR_RABBITMQ_VHOST=/erp-staging",
    "TODIR_RABBITMQ_USER=todir",
    "TODIR_RABBITMQ_PASSWORD=${var.rabbit_password}",
    "TODIR_RABBITMQ_TLS=true",
    "TODIR_RABBITMQ_TLS_SERVER_NAME=rabbitmq",
    "TODIR_RABBITMQ_CA_CERTIFICATE_PATH=/etc/rabbitmq/tls/ca.crt",
    "TODIR_CORS_ORIGINS=https://127.0.0.1:${var.web_port}",
  ]
  ports {
    internal = 8080
    external = var.api_port
  }
  volumes {
    volume_name    = docker_volume.data_protection.name
    container_path = "/app/keys"
  }
  volumes {
    host_path      = abspath("${var.rabbit_tls_directory}/ca.crt")
    container_path = "/etc/rabbitmq/tls/ca.crt"
    read_only      = true
  }
  networks_advanced {
    name    = docker_network.private.name
    aliases = ["api"]
  }
  depends_on = [docker_container.migrator, docker_container.rabbitmq]
}

resource "docker_container" "worker" {
  name  = "${local.project}-worker"
  image = var.worker_image
  env = [
    "TODIR_DB_COMMAND_TIMEOUT_SECONDS=15",
    "TODIR_DATABASE_CONNECTION=Host=${docker_container.postgres.name};Database=${var.database_name};Username=erp_app;Password=${var.database_password}",
    "TODIR_RABBITMQ_HOST=${docker_container.rabbitmq.name}",
    "TODIR_RABBITMQ_PORT=5671",
    "TODIR_RABBITMQ_VHOST=/erp-staging",
    "TODIR_RABBITMQ_USER=todir",
    "TODIR_RABBITMQ_PASSWORD=${var.rabbit_password}",
    "TODIR_RABBITMQ_TLS=true",
    "TODIR_RABBITMQ_TLS_SERVER_NAME=rabbitmq",
    "TODIR_RABBITMQ_CA_CERTIFICATE_PATH=/etc/rabbitmq/tls/ca.crt",
    "TODIR_RABBITMQ_CONSUMER=true",
    "TODIR_FAILURE_INJECTION=${var.failure_injection}",
    "TODIR_FAILURE_INJECTION_MARKER=/tmp/crash-after-inbox-commit",
  ]
  volumes {
    host_path      = abspath("${var.rabbit_tls_directory}/ca.crt")
    container_path = "/etc/rabbitmq/tls/ca.crt"
    read_only      = true
  }
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.migrator, docker_container.rabbitmq]
}

resource "docker_container" "web" {
  name  = "${local.project}-web"
  image = var.web_image
  ports {
    internal = 8443
    external = var.web_port
  }
  volumes {
    host_path      = abspath("${path.root}/../containers/staging-web.conf")
    container_path = "/etc/nginx/conf.d/default.conf"
    read_only      = true
  }
  volumes {
    host_path      = abspath(var.web_tls_directory)
    container_path = "/etc/nginx/tls"
    read_only      = true
  }
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.api]
}
