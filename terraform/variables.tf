# Ce fichier contient la définition des variables en entrée du main.tf
variable "rg_location" {
  type        = string
  default     = ""
  description = "The location of the resource group."
}

variable "rg_name" {
  type        = string
  default     = ""
  description = "The name of the resource group."
}

variable "app_service_plan_name" {
  type        = string
  default     = ""
  description = "The name of the app service plan."
}

variable "app_service_sku_name" {
  type        = string
  default     = ""
  description = "The name of the app service plan sku."
}

variable "app_service_os_type" {
  type        = string
  default     = ""
  description = "The operating system of the app service plan."
}

variable "app_service_name" {
  type        = string
  default     = ""
  description = "The name of the app service."
}

variable "db_server_name" {
  type        = string
  description = "The name of the database server."
}

variable "db_login" {
  type        = string
  description = "The login of the database server."
}

variable "db_password" {
  type        = string
  description = "The password of the database server."
}

variable "db_account_name" {
  type        = string
  description = "The name of the database account."
}

variable "db_name" {
  type        = string
  description = "The name of the database."
}
