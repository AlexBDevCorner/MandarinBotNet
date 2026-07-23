provider "oci" {
  region = "eu-frankfurt-1"
}

locals {
  compartment_ocid = coalesce(var.compartment_ocid, var.tenancy_ocid)

  common_tags = {
    Application = "MandarinBot"
    ManagedBy   = "Terraform"
  }
}
