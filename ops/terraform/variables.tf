variable "api_image" {
  type        = string
  description = "Immutable API image reference tagged with the release commit SHA."
  validation {
    condition     = endswith(var.api_image, ":${var.image_tag}")
    error_message = "api_image must use the exact immutable image_tag SHA."
  }
}

variable "worker_image" {
  type        = string
  description = "Immutable Worker image reference tagged with the release commit SHA."
  validation {
    condition     = endswith(var.worker_image, ":${var.image_tag}")
    error_message = "worker_image must use the exact immutable image_tag SHA."
  }
}

variable "migrator_image" {
  type        = string
  description = "Immutable database migrator image reference tagged with the release commit SHA."
  validation {
    condition     = endswith(var.migrator_image, ":${var.image_tag}")
    error_message = "migrator_image must use the exact immutable image_tag SHA."
  }
}

variable "web_image" {
  type        = string
  description = "Immutable Web image reference tagged with the release commit SHA."
  validation {
    condition     = endswith(var.web_image, ":${var.image_tag}")
    error_message = "web_image must use the exact immutable image_tag SHA."
  }
}

variable "database_name" {
  type    = string
  default = "todir_staging"
}

variable "database_password" {
  type      = string
  sensitive = true
}

variable "rabbit_password" {
  type      = string
  sensitive = true
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

variable "environment_name" {
  type    = string
  default = "staging"
}

variable "failure_injection" {
  type        = string
  default     = ""
  description = "Explicit staging-only failure injection; empty in normal deployments."
}

variable "image_tag" {
  type = string
  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_tag))
    error_message = "Staging image_tag must be a full 40-character immutable commit SHA."
  }
}
