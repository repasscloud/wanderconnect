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
| SSH (TCP) | 22 | your admin IP, `/32` |
| TCP | 80 | **Cloudflare** (Vultr's built-in Cloudflare source) |
| TCP | 443 | **Cloudflare** |

Everything else is dropped. Add the same rules for IPv6 only if you enabled it. Then **Linked Instances → add `swoovr-uat`**.

This is the outer layer. On the VM, `bin/host-setup` adds an nftables input firewall, and `bin/cloudflare-ips` limits Docker's published 80/443 to Cloudflare's ranges again. Mailpit's UI is published on the VM's loopback only, so it's never reachable from outside, even if a firewall rule is wrong.

Vultr blocks outgoing SMTP port 25 on new accounts. That doesn't matter here: SendGrid uses 587 with STARTTLS, Twilio uses HTTPS, and IMAP uses 993.

## 4. DNS and TLS (Cloudflare)

Follow the runbook §0 step 3 with the VM's IPv4: proxied `A` records for `uat`, `api-uat`, `admin-uat`, `auth-uat` and `helpdesk-uat`, Full (strict), the cache bypass, and the Origin CA certificate from #307.

## 5. First login and host preparation

```sh
ssh root@<VM IPv4>
apt-get update && apt-get -y full-upgrade && reboot      # once, then log in again
mkdir -p /opt/swoovr && cd /opt/swoovr
# From your machine: scp swoovr-uat-bundle-<commit>.tar.gz root@<VM IPv4>:/opt/swoovr/
tar -xzf swoovr-uat-bundle-<commit>.tar.gz && cd swoovr-uat
SWOOVR_SSH_CIDRS="<your admin IP>/32" ./bin/host-setup
docker login -u <dockerhub-user>                          # read-only token at the prompt
```

`host-setup` finds the public interface from the default route (`enp1s0` on most Vultr VMs). Set `SWOOVR_EXT_IF=<name>` if it guesses wrong. Then harden SSH (key-only, `PasswordAuthentication no`), check that you can still log in, and continue with runbook §2 (TLS and the Cloudflare edge filter).

Optional: **Snapshots → Take snapshot** now. A rebuilt VM from that snapshot skips §5.

## 6. Day-to-day

- **Status:** `./bin/swoovr status`. **Logs:** `./bin/swoovr logs api`.
- **Mailpit** (SMS codes and anything sent to the sink): `ssh -L 8025:127.0.0.1:8025 root@<VM IPv4>`, then open http://127.0.0.1:8025 on your machine.
- **Keycloak admin:** the SSH SOCKS tunnel in the runbook §5.
- **Resize:** Vultr → instance → **Settings → Change Plan**. It reboots the VM. Docker restarts the stack, which signs members out.
- **Rebuild from scratch:** destroy the instance and repeat this page. Or keep the VM and run `./bin/swoovr reset --destroy-uat-data`.
