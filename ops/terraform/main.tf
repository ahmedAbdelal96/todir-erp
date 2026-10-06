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
  networks_advanced {
    name = docker_network.private.name
  }
}

resource "docker_container" "rabbitmq" {
  name  = "${local.project}-rabbitmq"
  image = "rabbitmq:4-management"
  env = [
    "RABBITMQ_DEFAULT_USER=todir",
    "RABBITMQ_DEFAULT_PASS=${var.rabbit_password}",
    "RABBITMQ_DEFAULT_VHOST=/erp-staging",
  ]
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
    "TODIR_DATABASE_CONNECTION=Host=${docker_container.postgres.name};Database=${var.database_name};Username=erp_app;Password=${var.database_password}",
    "TODIR_RABBITMQ_HOST=${docker_container.rabbitmq.name}",
    "TODIR_RABBITMQ_PORT=5671",
    "TODIR_RABBITMQ_VHOST=/erp-staging",
    "TODIR_RABBITMQ_USER=todir",
    "TODIR_RABBITMQ_PASSWORD=${var.rabbit_password}",
    "TODIR_RABBITMQ_TLS=true",
    "TODIR_RABBITMQ_TLS_SERVER_NAME=rabbitmq",
    "TODIR_CORS_ORIGINS=http://127.0.0.1:${var.web_port}",
  ]
  ports {
    internal = 8080
    external = var.api_port
  }
  volumes {
    volume_name    = docker_volume.data_protection.name
    container_path = "/app/keys"
  }
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.migrator, docker_container.rabbitmq]
}

resource "docker_container" "worker" {
  name  = "${local.project}-worker"
  image = var.worker_image
  env = [
    "TODIR_DATABASE_CONNECTION=Host=${docker_container.postgres.name};Database=${var.database_name};Username=erp_app;Password=${var.database_password}",
    "TODIR_RABBITMQ_HOST=${docker_container.rabbitmq.name}",
    "TODIR_RABBITMQ_PORT=5671",
    "TODIR_RABBITMQ_VHOST=/erp-staging",
    "TODIR_RABBITMQ_USER=todir",
    "TODIR_RABBITMQ_PASSWORD=${var.rabbit_password}",
    "TODIR_RABBITMQ_TLS=true",
    "TODIR_RABBITMQ_TLS_SERVER_NAME=rabbitmq",
    "TODIR_RABBITMQ_CONSUMER=true",
  ]
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.migrator, docker_container.rabbitmq]
}

resource "docker_container" "web" {
  name  = "${local.project}-web"
  image = var.web_image
  ports {
    internal = 8080
    external = var.web_port
  }
  networks_advanced {
    name = docker_network.private.name
  }
  depends_on = [docker_container.api]
}
