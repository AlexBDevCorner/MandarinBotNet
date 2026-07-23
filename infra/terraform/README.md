# MandarinBot OCI infrastructure

This Terraform stack creates a small Oracle Cloud Always Free-eligible host in
Frankfurt:

- one `VM.Standard.A1.Flex` VM with 1 OCPU and 2 GB RAM;
- a 50 GB boot volume;
- Ubuntu 24.04 AArch64 with Docker installed by cloud-init;
- a dedicated VCN, public subnet, internet gateway, and network security group;
- no inbound network access by default;
- optional SSH access from one trusted IPv4 CIDR.

The stack intentionally does not create compartments, IAM users, policies, API
keys, database resources, or application secrets. These have a different
security and lifecycle boundary. See
[`docs/oci-infrastructure.md`](../../docs/oci-infrastructure.md) for the
one-time setup and workflow instructions.

Oracle can report temporary A1 capacity shortages even when the shape is
offered in a region. If that happens, change `availability_domain_number` on
the Resource Manager stack to 2 or 3 and plan again.
