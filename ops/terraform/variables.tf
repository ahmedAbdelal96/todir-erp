variable "api_image" {
  type        = string
  description = "Immutable API image reference tagged with the release commit SHA."
}

variable "worker_image" {
  type        = string
  description = "Immutable Worker image reference tagged with the release commit SHA."
}

variable "migrator_image" {
  type        = string
  description = "Immutable database migrator image reference tagged with the release commit SHA."
}

variable "web_image" {
  type        = string
  description = "Immutable Web image reference tagged with the release commit SHA."
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

variable "api_port" {
  type    = number
  default = 18080
}

variable "web_port" {
  type    = number
  default = 18081
}

variable "environment_name" {
  type    = string
  default = "staging"
}

variable "image_tag" {
  type = string
  validation {
    condition     = var.image_tag != "latest" && length(var.image_tag) >= 7
    error_message = "Staging image_tag must be an immutable commit SHA and never latest."
  }
}
