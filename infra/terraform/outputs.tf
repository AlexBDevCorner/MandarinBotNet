output "instance_id" {
  description = "OCID of the bot VM."
  value       = oci_core_instance.bot.id
}

output "public_ip" {
  description = "Ephemeral public IPv4 address of the bot VM."
  value       = oci_core_instance.bot.public_ip
}

output "private_ip" {
  description = "Private IPv4 address of the bot VM."
  value       = oci_core_instance.bot.private_ip
}

output "availability_domain" {
  description = "Availability domain in which the VM was created."
  value       = oci_core_instance.bot.availability_domain
}

output "instance_image_id" {
  description = "OCID of the platform image currently used by the VM."
  value       = oci_core_instance.bot.source_details[0].source_id
}

output "ssh_command" {
  description = "SSH command when SSH ingress is configured; otherwise null."
  value       = var.ssh_ingress_cidr == null ? null : "ssh ubuntu@${oci_core_instance.bot.public_ip}"
}
