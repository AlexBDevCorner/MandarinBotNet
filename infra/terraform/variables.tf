variable "tenancy_ocid" {
  description = "OCI tenancy OCID. Resource Manager also uses it to find availability domains."
  type        = string

  validation {
    condition     = can(regex("^ocid1\\.tenancy\\.", var.tenancy_ocid))
    error_message = "tenancy_ocid must be an OCI tenancy OCID."
  }
}

variable "compartment_ocid" {
  description = "Dedicated compartment for the bot. Null places resources in the tenancy root."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition = (
      var.compartment_ocid == null ||
      can(regex("^ocid1\\.(compartment|tenancy)\\.", var.compartment_ocid))
    )
    error_message = "compartment_ocid must be null or an OCI compartment/tenancy OCID."
  }
}

variable "availability_domain_number" {
  description = "Frankfurt availability-domain number. VM.Standard.E2.1.Micro is offered in AD 3."
  type        = number
  default     = 3

  validation {
    condition     = var.availability_domain_number == 3
    error_message = "availability_domain_number must be 3 for VM.Standard.E2.1.Micro in Frankfurt."
  }
}

variable "ssh_authorized_keys" {
  description = "One or more OpenSSH public keys. Never provide a private key."
  type        = string
  sensitive   = true

  validation {
    condition = (
      length(trimspace(var.ssh_authorized_keys)) > 0 &&
      !can(regex("PRIVATE KEY", var.ssh_authorized_keys))
    )
    error_message = "ssh_authorized_keys must contain public keys, not a private key."
  }
}

variable "ssh_ingress_cidr" {
  description = "Optional trusted IPv4 CIDR allowed to SSH. Null keeps every inbound port closed."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition     = var.ssh_ingress_cidr == null || can(cidrnetmask(var.ssh_ingress_cidr))
    error_message = "ssh_ingress_cidr must be null or a valid IPv4 CIDR."
  }
}

variable "instance_display_name" {
  description = "Display name for the VM."
  type        = string
  default     = "mandarin-bot"

  validation {
    condition     = length(trimspace(var.instance_display_name)) > 0
    error_message = "instance_display_name cannot be empty."
  }
}
