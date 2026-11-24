variable "api_image" {
  type        = string
  description = "Digest-pinned API image reference whose tag is the release commit SHA."
  validation {
    condition     = can(regex(":${var.image_tag}@sha256:[0-9a-f]{64}$", var.api_image))
    error_message = "api_image must use the exact image_tag commit SHA tag and an immutable sha256 digest."
  }
}

variable "worker_image" {
  type        = string
  description = "Digest-pinned Worker image reference whose tag is the release commit SHA."
  validation {
    condition     = can(regex(":${var.image_tag}@sha256:[0-9a-f]{64}$", var.worker_image))
    error_message = "worker_image must use the exact image_tag commit SHA tag and an immutable sha256 digest."
  }
}

variable "migrator_image" {
  type        = string
  description = "Digest-pinned database migrator image reference whose tag is the release commit SHA."
  validation {
    condition     = can(regex(":${var.image_tag}@sha256:[0-9a-f]{64}$", var.migrator_image))
    error_message = "migrator_image must use the exact image_tag commit SHA tag and an immutable sha256 digest."
  }
}

variable "web_image" {
  type        = string
  description = "Digest-pinned Web image reference whose tag is the release commit SHA."
  validation {
    condition     = can(regex(":${var.image_tag}@sha256:[0-9a-f]{64}$", var.web_image))
    error_message = "web_image must use the exact image_tag commit SHA tag and an immutable sha256 digest."
  }
}

variable "database_name" {
  type    = string
  default = "todir_staging"
}

variable "private_network_subnet" {
  type        = string
  default     = "172.31.0.0/24"
  description = "Private Docker network CIDR. The API trusts forwarded headers only from this network's reverse proxy."
  validation {
    condition     = can(cidrhost(var.private_network_subnet, 0))
    error_message = "private_network_subnet must be a valid CIDR block."
  }
}

variable "database_admin_password" {
  type        = string
  sensitive   = true
  description = "Password for the deployment-only PostgreSQL administrator used by the migrator. Never give this value to API or Worker."
  validation {
    condition     = length(var.database_admin_password) >= 16
    error_message = "database_admin_password must be at least 16 characters."
  }
}

variable "database_runtime_password" {
  type        = string
  sensitive   = true
  description = "Independent password for the non-superuser erp_app runtime role."
  validation {
    condition     = length(var.database_runtime_password) >= 16 && var.database_runtime_password != var.database_admin_password
    error_message = "database_runtime_password must be at least 16 characters and differ from database_admin_password."
  }
}

variable "rabbit_password" {
  type        = string
  sensitive   = true
  description = "Password for the staging RabbitMQ application identity, scoped to the staging vhost."
  validation {
    condition     = length(var.rabbit_password) >= 16
    error_message = "rabbit_password must be at least 16 characters."
  }
}

variable "object_storage_access_key" {
  type        = string
  description = "Non-root MinIO identity used by the API for the private-files bucket."
  default     = "todir-files"
  validation {
    condition     = can(regex("^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$", var.object_storage_access_key)) && lower(var.object_storage_access_key) != "minio-admin" && lower(var.object_storage_access_key) != "minioadmin"
    error_message = "object_storage_access_key must be a valid non-root MinIO identity."
  }
}

variable "object_storage_password" {
  type        = string
  sensitive   = true
  description = "Independent password for the non-root MinIO private-files identity."
  validation {
    condition     = length(var.object_storage_password) >= 16 && var.object_storage_password != var.rabbit_password
    error_message = "object_storage_password must be at least 16 characters and differ from rabbit_password."
  }
}

variable "object_storage_root_password" {
  type        = string
  sensitive   = true
  description = "Deployment-only MinIO root password used only by bucket initialization."
  validation {
    condition     = length(var.object_storage_root_password) >= 16 && var.object_storage_root_password != var.object_storage_password
    error_message = "object_storage_root_password must be at least 16 characters and differ from object_storage_password."
  }
}

variable "entitlement_hmac_key" {
  type        = string
  sensitive   = true
  description = "Ephemeral HMAC key used to verify staging entitlement snapshots."
}

variable "data_protection_certificate_base64" {
  type      = string
  sensitive = true
}

variable "data_protection_certificate_password" {
  type      = string
  sensitive = true
}

variable "rabbit_tls_directory" {
  type        = string
  description = "Runner-local directory containing ca.crt, server.crt, and server.key for the isolated RabbitMQ container."
}

variable "web_tls_directory" {
  type        = string
  description = "Runner-local directory containing the short-lived staging web certificate and private key."
}

variable "web_port" {
  type    = number
  default = 18081
}

variable "web_bind_ip" {
  type        = string
  default     = "127.0.0.1"
  description = "Host bind address for the staging web boundary. Use a reviewed ingress address only when explicitly approved."
  validation {
    condition     = can(cidrhost("${var.web_bind_ip}/32", 0))
    error_message = "web_bind_ip must be an IPv4 address."
  }
}

variable "environment_name" {
  type    = string
  default = "staging"
}

variable "failure_injection" {
  type        = string
  default     = ""
  description = "Explicit staging-only failure injection; empty in normal deployments."
}

variable "staging_replay_token" {
  type        = string
  sensitive   = true
  default     = ""
  description = "Ephemeral token for the staging-only message replay verification route."
}

variable "image_tag" {
  type = string
  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_tag))
    error_message = "Staging image_tag must be a full 40-character immutable commit SHA."
  }
}
