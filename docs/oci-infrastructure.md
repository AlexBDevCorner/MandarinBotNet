# OCI infrastructure deployment

The repository contains a manual-only provisioning workflow and an OCI
Resource Manager Terraform stack. No OCI resources are created by committing
these files.

## What it creates

The stack in [`infra/terraform`](../infra/terraform) provisions:

- an Always Free-eligible `VM.Standard.E2.1.Micro` VM in `eu-frankfurt-1`;
- 1 GB RAM and a 50 GB boot volume;
- Ubuntu 24.04 x86_64 and Docker;
- a dedicated VCN and public subnet;
- outbound internet access, with all inbound access closed by default.

Set `ssh_ingress_cidr` only when direct administration is needed. Use a single
trusted address such as `203.0.113.10/32`; never use `0.0.0.0/0`.

Always Free eligibility and capacity are account- and region-dependent. Review
the Resource Manager plan before approval, and confirm that every planned
resource is marked Always Free-eligible in the OCI Console.

## One-time OCI setup

1. Create a dedicated compartment, for example `MandarinBot`. The Terraform
   stack intentionally does not need permission to create compartments.
2. Create a dedicated IAM user and group for GitHub provisioning, upload an API
   signing public key to that user, and retain the private key securely.
3. Grant the group only the permissions needed in the bot compartment. Replace
   the names below with your group and compartment names:

   ```text
   Allow group MandarinBotDeployers to use orm-stacks in compartment MandarinBot
   Allow group MandarinBotDeployers to read orm-jobs in compartment MandarinBot
   Allow group MandarinBotDeployers to manage orm-jobs in compartment MandarinBot where any {target.job.operation = 'PLAN', target.job.operation = 'APPLY'}
   Allow group MandarinBotDeployers to manage instance-family in compartment MandarinBot
   Allow group MandarinBotDeployers to manage virtual-network-family in compartment MandarinBot
   Allow group MandarinBotDeployers to manage volume-family in compartment MandarinBot
   ```

   The job condition excludes Resource Manager destroy jobs. The dedicated API
   user still has the listed core permissions, so keep its key restricted to
   this repository and rotate it if exposed.
4. In OCI Resource Manager, create a GitHub configuration source provider with
   read access to this repository. Then create a stack in the bot compartment
   from the default branch, select Terraform `1.5.x` (`1.5.7`), and use
   `infra/terraform` as its working directory.
5. Configure these Resource Manager stack variables:

   | Variable | Value |
   | --- | --- |
   | `tenancy_ocid` | Tenancy OCID |
   | `compartment_ocid` | Dedicated bot compartment OCID |
   | `availability_domain_number` | `3`; the E2.1.Micro shape is offered only in Frankfurt AD 3 |
   | `ssh_authorized_keys` | One or more OpenSSH public keys |
   | `ssh_ingress_cidr` | Leave unset for closed inbound access, or set one trusted `/32` |
   | `instance_display_name` | Optional; defaults to `mandarin-bot` |

Do not put bot tokens, database passwords, OCI private keys, or other
application secrets in Terraform variables. Terraform values can be retained
in state and job logs.

## GitHub repository setup

Create the following repository variables:

| Variable | Purpose |
| --- | --- |
| `OCI_TENANCY_OCID` | OCI tenancy OCID |
| `OCI_RESOURCE_MANAGER_USER_OCID` | Dedicated provisioning user OCID |
| `OCI_RESOURCE_MANAGER_FINGERPRINT` | Fingerprint of its API signing key |
| `OCI_REGION` | `eu-frankfurt-1` |
| `OCI_RESOURCE_MANAGER_STACK_ID` | OCID of the Resource Manager stack |

Create one repository secret:

| Secret | Purpose |
| --- | --- |
| `OCI_RESOURCE_MANAGER_PRIVATE_KEY_B64` | Base64-encoded PEM private API signing key |

For example, encode the key locally without printing it:

```bash
base64 -w 0 ~/.oci/mandarin-bot-api-key.pem | gh secret set OCI_RESOURCE_MANAGER_PRIVATE_KEY_B64
```

On macOS, use `base64 < ~/.oci/mandarin-bot-api-key.pem` and remove any line
breaks before storing the value.

Create a GitHub environment named `oci-production` and enable required
reviewers. Keep the API signing key as a repository secret because the plan job
runs before the protected environment; the apply job cannot start until the
environment is approved.

## Running the workflow

1. Open **Actions → Provision OCI infrastructure → Run workflow**.
2. Select the default branch.
3. Enter `PROVISION`.
4. Inspect the plan log.
5. Approve the `oci-production` environment only when the plan is expected.

The apply job uses `FROM_PLAN_JOB_ID`, so it applies the exact plan that was
reviewed. The workflow has no scheduled, push, pull-request, or destroy trigger.
Concurrent infrastructure runs are serialized.

The workflow pins OCI CLI `3.89.3`; the stack pins the Resource Manager runtime
to Terraform `1.5.7` and the OCI provider to `8.23.0`. Upgrade these
intentionally and validate a plan before applying.

## Database later

The E2.1.Micro VM has only 1 GB RAM. A small SQLite database stored on the
persistent host-mounted data directory is suitable for the bot, but a
PostgreSQL container on the same VM is not recommended.

When the workload outgrows SQLite or the data needs independent availability,
prefer adding either:

- OCI Autonomous Database Always Free, accessed over TLS; or
- a separate database host or service on a private network.

Add database infrastructure in a separate Terraform module/state before
production data exists. Store credentials outside Terraform state and back up
to OCI Object Storage. No database port should be exposed to the public
internet.
