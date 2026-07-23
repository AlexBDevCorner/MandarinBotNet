# MandarinBot OCI infrastructure

This Terraform stack creates a small Oracle Cloud Always Free-eligible host in
Frankfurt:

- one fixed-size `VM.Standard.E2.1.Micro` VM with 1 GB RAM;
- a 50 GB boot volume;
- Ubuntu 24.04 x86_64 with Docker installed by cloud-init;
- a dedicated VCN, public subnet, internet gateway, and network security group;
- no inbound network access by default;
- optional SSH access from one trusted IPv4 CIDR.

The stack intentionally does not create compartments, IAM users, policies, API
keys, database resources, or application secrets. These have a different
security and lifecycle boundary. See
[`docs/oci-infrastructure.md`](../../docs/oci-infrastructure.md) for the
one-time setup and workflow instructions.

In Frankfurt, `VM.Standard.E2.1.Micro` is offered only in availability domain
3. Oracle can still report temporary capacity shortages; if that happens,
leave `availability_domain_number` set to `3` and retry later.
