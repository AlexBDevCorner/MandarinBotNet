resource "oci_core_vcn" "bot" {
  compartment_id = local.compartment_ocid
  cidr_blocks    = ["10.42.0.0/16"]
  display_name   = "mandarin-bot-vcn"
  dns_label      = "mandarinbot"
  freeform_tags  = local.common_tags
}

resource "oci_core_internet_gateway" "bot" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.bot.id
  display_name   = "mandarin-bot-internet-gateway"
  enabled        = true
  freeform_tags  = local.common_tags
}

resource "oci_core_route_table" "public" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.bot.id
  display_name   = "mandarin-bot-public-routes"
  freeform_tags  = local.common_tags

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_internet_gateway.bot.id
  }
}

resource "oci_core_security_list" "public" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.bot.id
  display_name   = "mandarin-bot-public-security-list"
  freeform_tags  = local.common_tags

  egress_security_rules {
    destination      = "0.0.0.0/0"
    destination_type = "CIDR_BLOCK"
    protocol         = "all"
    stateless        = false
  }
}

resource "oci_core_subnet" "public" {
  compartment_id             = local.compartment_ocid
  vcn_id                     = oci_core_vcn.bot.id
  cidr_block                 = "10.42.1.0/24"
  display_name               = "mandarin-bot-public-subnet"
  dns_label                  = "bot"
  prohibit_public_ip_on_vnic = false
  route_table_id             = oci_core_route_table.public.id
  security_list_ids          = [oci_core_security_list.public.id]
  freeform_tags              = local.common_tags
}

resource "oci_core_network_security_group" "bot" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.bot.id
  display_name   = "mandarin-bot-nsg"
  freeform_tags  = local.common_tags
}

resource "oci_core_network_security_group_security_rule" "ssh" {
  count = var.ssh_ingress_cidr == null ? 0 : 1

  network_security_group_id = oci_core_network_security_group.bot.id
  direction                 = "INGRESS"
  protocol                  = "6"
  source                    = coalesce(var.ssh_ingress_cidr, "0.0.0.0/32")
  source_type               = "CIDR_BLOCK"
  stateless                 = false

  tcp_options {
    destination_port_range {
      min = 22
      max = 22
    }
  }
}
