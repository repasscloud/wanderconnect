# Create the UAT VM on Vultr

The whole UAT stack runs on **one Vultr cloud VM** with Docker (owner, 11 November 2026, D361). This replaces the earlier bare-metal plan. This page covers the Vultr console. The [runbook](README.md) takes over from "1. Prepare the host".

Nothing here was run against a Vultr account. Vultr's console labels and plan names change; pick the closest match.

## 1. Size

The stack has about 20 containers: Caddy, Web, Admin, API, Keycloak, PostgreSQL, ClamAV, Mailpit, and Zammad with its PostgreSQL, Redis, Memcached and Elasticsearch.

| | Minimum | Recommended (DEFAULT) |
| --- | --- | --- |
| vCPU | 4 | 8 |
| RAM | 16 GB | 32 GB |
| Disk | 160 GB NVMe | 200 GB+ NVMe |

- **RAM:** Elasticsearch takes a 2 GB heap, ClamAV about 1.5 GB, and Zammad, Keycloak and the .NET apps the rest.
- **Disk:** images, Elasticsearch, Zammad attachments and logs.
- **Plan:** "Optimized Cloud Compute → General Purpose" or "Cloud Compute → High Performance" both fit. Resource sizing is unmeasured: watch RAM, CPU and disk in the first week (`docker stats`, `df -h`) and resize if needed.
- **Architecture:** x86-64 (amd64). The images are multi-arch, so an arm64 plan also works.

## 2. Deploy the instance

Vultr console → **Deploy** (or **Products → Compute → Deploy Server**):

1. **Type:** the plan from §1.
2. **Location:** **Sydney** (or Melbourne), close to the testers and Australian phone numbers.
3. **Image:** **Debian 13 x64** (Debian 12 also works; `bin/host-setup` accepts both).
4. **SSH key:** add your public key and select it. Never use password login.
5. **Hostname / label:** `swoovr-uat`.
6. **Additional features:**
   - **IPv6:** optional. The bundle uses IPv4 only, and Cloudflare must reach the VM over IPv4: no `AAAA` records.
   - **Automatic backups:** off. UAT is disposable and is rebuilt, not restored (owner). A manual snapshot after `host-setup` (§5) is a cheap quick start.
   - **DDoS protection:** optional; Cloudflare already fronts every public name.
   - **Cloud-init / user data:** none.
   - **VPC:** not needed for one VM.
7. **Deploy**, and note the **public IPv4**.

## 3. Vultr firewall (in front of the VM)

**Network → Firewall → Add Firewall Group** `swoovr-uat`. Inbound IPv4 rules:

| Protocol | Port | Source |
| --- | --- | --- |
| SSH (TCP) | 22 | **anywhere** (key-only root): GitHub's runners deploy over SSH from changing addresses |
| TCP | 80 | **Cloudflare** (Vultr's built-in Cloudflare source) |
| TCP | 443 | **Cloudflare** |

Everything else is dropped. Add the same rules for IPv6 only if you enabled it. Then **Linked Instances → add `swoovr-uat`**.

This is the outer layer. On the VM, `bin/host-setup` adds an nftables input firewall, and `bin/cloudflare-ips` limits Docker's published 80/443 to Cloudflare's ranges again. Mailpit's UI is published on the VM's loopback only, so it's never reachable from outside, even if a firewall rule is wrong.

Vultr blocks outgoing SMTP port 25 on new accounts. That doesn't matter here: SendGrid uses 587 with STARTTLS, Twilio uses HTTPS, and IMAP uses 993.

## 4. DNS and TLS (Cloudflare)

Proxied (orange-cloud) `A` records to the VM's IPv4 for `uat`, `api-uat`, `admin-uat`, `auth-uat` and `helpdesk-uat` (no `AAAA`: the Docker networks are IPv4-only), SSL/TLS **Full (strict)**, a cache rule that **bypasses cache** for all five hostnames, and a Cloudflare **Origin CA certificate** (owner #307: PEM, RSA 2048, explicit SANs `uat`, `api-uat`, `admin-uat`, `auth-uat`, `helpdesk-uat`, `venues-uat` `.swoovr.app`, no wildcard).

**GitHub deployments (D362)** connect over SSH from GitHub's runners, whose addresses change. The current VM has **no** Vultr firewall group (owner, 12 November 2026); the host firewall allows key-only SSH. If you add a group, keep SSH open to all addresses for the runners.

## 5. First login and host preparation (a new VM only)

The current VM is already prepared. For a new one, every command on the VM runs **as root** (`sudo -i` first if you log in as another user). Copy the host scripts from your checkout of `main`; the deployment workflow ships everything else:

```sh
# On your machine
scp deploy/vultr-uat/bin/{host-setup,tls-setup,cloudflare-ips,origin-cert-check} root@<VM IPv4>:/tmp/
# On the VM
apt-get update && apt-get -y full-upgrade && reboot      # once, then log in again
install -d -m 750 /opt/swoovr/swoovr-uat/bin && install -d -m 700 /opt/swoovr/swoovr-uat/env
mv /tmp/{host-setup,tls-setup,cloudflare-ips,origin-cert-check} /opt/swoovr/swoovr-uat/bin/ && cd /opt/swoovr/swoovr-uat
./bin/host-setup                                         # Docker, sysctls, nftables input table, /etc/hosts aliases, unattended upgrades
install -m 700 -d /etc/caddy/certs                       # then paste the Origin certificate and key with an editor:
nano /etc/caddy/certs/uat-origin.pem; nano /etc/caddy/certs/uat-origin.key
chmod 644 /etc/caddy/certs/uat-origin.pem; chmod 600 /etc/caddy/certs/uat-origin.key
./bin/origin-cert-check && ./bin/tls-setup && ./bin/cloudflare-ips && systemctl start swoovr-edge-firewall
```

`SWOOVR_SSH_CIDRS="<your public IP>/32" ./bin/host-setup` optionally limits SSH on the VM itself; don't use it if GitHub runners must connect. `host-setup` finds the public interface from the default route (`enp1s0` on Vultr); set `SWOOVR_EXT_IF=<name>` if it guesses wrong. Harden SSH yourself (key-only, `PasswordAuthentication no`, `PermitRootLogin prohibit-password`) and add the deploy key to `/root/.ssh/authorized_keys`. Then set `UAT_EXPECTED_HOSTNAME` to the VM's `hostname` and the new host key on GitHub, and make the first deployment a **refresh** (runbook "First deployment").

No Docker registry login is needed: the Swoovr images on GHCR are public and the third-party images are public upstream. Optional: **Snapshots → Take snapshot** after this section.

## 6. Day-to-day

- **Deploy:** push a `vX.Y.Z` tag from `main` (runbook "Releasing"). **Status:** Actions → UAT maintenance → `status`, or `./bin/swoovr status`. **Logs:** `./bin/swoovr logs api`.
- **Mailpit** (SMS codes and anything sent to the sink): `ssh -L 8025:127.0.0.1:8025 root@<VM IPv4>`, then open http://127.0.0.1:8025 on your machine.
- **Keycloak admin:** the SSH SOCKS tunnel (runbook "Keycloak admin console") or Tailscale ([UAT-TAILSCALE-SETUP.md](../../docs/delivery/UAT-TAILSCALE-SETUP.md)).
- **Resize:** Vultr → instance → **Settings → Change Plan**. It reboots the VM. Docker restarts the stack, which signs members out.
- **Reset the application data:** a **refresh** deployment (`DEPLOYMENT_MODE=refresh`, runbook). **Rebuild the VM:** destroy the instance and repeat this page.
