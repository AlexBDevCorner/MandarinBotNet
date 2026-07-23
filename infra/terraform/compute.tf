data "oci_identity_availability_domain" "selected" {
  compartment_id = var.tenancy_ocid
  ad_number      = var.availability_domain_number
}

data "oci_core_images" "ubuntu_arm" {
  compartment_id           = local.compartment_ocid
  operating_system         = "Canonical Ubuntu"
  operating_system_version = "24.04"
  shape                    = "VM.Standard.A1.Flex"
  state                    = "AVAILABLE"
  sort_by                  = "TIMECREATED"
  sort_order               = "DESC"
}

locals {
  ubuntu_image_id = try(data.oci_core_images.ubuntu_arm.images[0].id, null)
}

resource "oci_core_instance" "bot" {
  availability_domain = data.oci_identity_availability_domain.selected.name
  compartment_id      = local.compartment_ocid
  display_name        = var.instance_display_name
  shape               = "VM.Standard.A1.Flex"
  freeform_tags       = local.common_tags

  shape_config {
    ocpus         = 1
    memory_in_gbs = 2
  }

  create_vnic_details {
    assign_private_dns_record = true
    assign_public_ip          = true
    display_name              = "mandarin-bot-vnic"
    hostname_label            = "mandarin-bot"
    nsg_ids                   = [oci_core_network_security_group.bot.id]
    subnet_id                 = oci_core_subnet.public.id
  }

  metadata = {
    ssh_authorized_keys = trimspace(var.ssh_authorized_keys)
    user_data           = base64encode(file("${path.module}/cloud-init.yaml"))
  }

  source_details {
    source_type             = "image"
    source_id               = local.ubuntu_image_id
    boot_volume_size_in_gbs = 50
  }

  lifecycle {
    # Use the newest Ubuntu image for initial creation without replacing a
    # healthy VM whenever Oracle publishes a newer platform image.
    ignore_changes = [source_details[0].source_id]

    precondition {
      condition     = length(data.oci_core_images.ubuntu_arm.images) > 0
      error_message = "No Ubuntu 24.04 AArch64 image is available for VM.Standard.A1.Flex."
    }
  }
}
