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

## One-time application deployment setup

Application deployment uses two runners:

- a GitHub-hosted runner builds the image and publishes it to this private
  repository's GitHub Container Registry package;
- a repository-scoped self-hosted runner on the VM pulls and starts that image.

The self-hosted runner connects outbound to GitHub, so the network security
group can keep all inbound ports closed. The 1 GB VM never compiles the
application.

Register the runner as follows:

1. Temporarily enable SSH only from your trusted `/32`, or use an OCI console
   connection. Do not open SSH to `0.0.0.0/0`.
2. Create a dedicated service account and grant it Docker access:

   ```bash
   sudo useradd --create-home --shell /bin/bash github-runner
   sudo usermod --append --groups docker github-runner
   ```

   Membership in the `docker` group is root-equivalent. Keep this runner scoped
   only to this private repository and do not add pull-request triggers to the
   deployment workflow.
3. In **Repository settings → Actions → Runners**, choose **New self-hosted
   runner**, then select Linux and x64. Run GitHub's displayed download commands
   under the `github-runner` account. Configure it with the additional label
   `mandarinbot-production`; the registration token is short-lived and must not
   be saved in the repository:

   ```bash
   ./config.sh \
     --url https://github.com/AlexBDevCorner/MandarinBotNet \
     --token <SHORT-LIVED-REGISTRATION-TOKEN> \
     --name mandarinbot-production \
     --labels mandarinbot-production \
     --unattended
   sudo ./svc.sh install github-runner
   sudo ./svc.sh start
   sudo ./svc.sh status
   ```

4. Store the Discord token only on the VM. The deployment script reads it but
   never copies it into an image or GitHub Actions:

   ```bash
   sudo install -d -m 0750 -o ubuntu -g docker /opt/mandarinbot/data
   sudo install -m 0640 -o root -g docker /dev/null /opt/mandarinbot/mandarinbot.env
   sudoedit /opt/mandarinbot/mandarinbot.env
   ```

   The file must contain:

   ```dotenv
   Bot__Discord__Token=<DISCORD-BOT-TOKEN>
   Bot__Discord__Commands__RegistrationMode=Global
   Bot__FantasyPremierLeague__ClassicLeagueId=<CLASSIC-LEAGUE-ID>
   Bot__FantasyPremierLeague__HeadToHeadLeagueId=<HEAD-TO-HEAD-LEAGUE-ID>
   Bot__Schedules__PremierLeagueNotifications__Enabled=true
   Bot__Schedules__ClassicStandings__Enabled=true
   Bot__Schedules__HeadToHeadStandings__Enabled=true
   Bot__Notifications__Targets__0__GuildId=<DISCORD-GUILD-ID>
   Bot__Notifications__Targets__0__ChannelId=<DISCORD-CHANNEL-ID>
   Bot__Notifications__Targets__0__MentionEveryone=false
   ```

   See [bot configuration](bot-configuration.md) for schedule overrides and
   additional notification targets.

5. Confirm the runner appears online, then remove temporary SSH ingress if it
   is no longer needed.

Do not reuse the OCI Resource Manager API key as a runner or application
credential.

## Deploying the application

Every push to the `main` branch automatically builds and deploys that commit.
Deployments are serialized, so concurrent pushes cannot run overlapping
container rollouts.

For a manual redeployment:

1. Open **Actions → Build and deploy MandarinBot to OCI VM → Run workflow**.
2. Select the default branch and enter `DEPLOY`.
3. Approve the `oci-production` environment if protection rules are configured.

The workflow publishes an immutable commit-SHA image to GHCR. On the VM it
stops and retains the previous container, starts the new one with
`--restart unless-stopped`, and runs the image's machine-readable health probe.
The rollout succeeds only after Discord and SQLite readiness remains healthy
for 15 continuous seconds. A failed probe resets that stabilization window. If
startup fails or sustained readiness is not reached within 90 seconds, the new
container is removed and the previous container is restored automatically.
Deployments are serialized.

Useful VM checks are:

```bash
docker ps --filter name=mandarinbot
docker inspect --format '{{json .State.Health}}' mandarinbot
docker exec mandarinbot dotnet MandarinBotNet.dll --health-check /app/data/health-state.json 15
docker logs --tail 100 mandarinbot
sudo systemctl status actions.runner.*
```

## Health semantics

The container publishes `/app/data/health-state.json` every five seconds and
the Docker `HEALTHCHECK` rejects state older than 15 seconds.

- **Liveness** means the worker process is running and continuing to publish a
  fresh health state. A stopped or wedged process becomes unhealthy without
  inspecting logs.
- **Readiness** requires an active Discord gateway connection and a successful
  SQLite integrity and writeability check against the persistent notification
  database.
- FPL API availability is deliberately excluded. Its transient failures are
  handled by the client retry policy and do not kill liveness or readiness.

The health state contains only timestamps, booleans, and named check statuses;
it never contains the Discord token or other configuration values.

After the first successful OCI deployment, remove the obsolete Azure App
Service publish-profile secret and the old Docker Hub credentials from the
repository if no other workflow uses them.

## Running the infrastructure workflow

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
