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

### 5a. Get the bundle (on your machine)

The bundle `swoovr-uat-bundle-<commit>.tar.gz` is **not in Git and you don't build it by hand**. The **Images** GitHub Actions workflow makes it in the same run that pushes the five images to Docker Hub (runbook §0 step 2). `<commit>` is the full 40-character commit the images were built from.

1. This repository on GitHub → **Actions** → **Images** → open the successful run → **Artifacts** (bottom of the summary page) → download **`swoovr-release-<commit>`**. Or with the GitHub CLI, from inside your checkout of this repository:

   ```sh
   gh run list --workflow images.yml -L 5 --json databaseId,headSha,conclusion,createdAt \
     -q '.[] | "\(.databaseId)  \(.headSha)  \(.conclusion)  \(.createdAt)"'
   # prints: <run-id>  <commit>  success  <date>; use the newest "success" line
   gh run download <run-id> -n swoovr-release-<commit> -D ~/swoovr-release
   ```

2. The browser download is a zip (unzip it); `gh` unzips into `~/swoovr-release` for you. Inside:

   ```text
   release-manifest.env
   release-manifest.json
   dist/swoovr-uat-bundle-<commit>.tar.gz      <- this is the bundle; note the dist/ folder
   ```

Artifacts expire **30 days** after the run. After that, run **Images** again (Actions → Images → **Run workflow** on `main`), or rebuild only the bundle from a checkout at the same commit with that run's `release-manifest.env` (its contents are also printed on the run's summary page):

```sh
git checkout <commit>
./deploy/vultr-uat/bin/make-bundle.sh release-manifest.env dist     # writes dist/swoovr-uat-bundle-<commit>.tar.gz
```

The bundle holds no secrets: `compose.yaml`, the Caddyfile, `bin/`, `env/*.env.example`, the Zammad provisioner and `release.env` (the image digests). Secrets are created on the VM in runbook §3.

### 5b. Copy it to the VM and prepare the host

Every command on the VM runs **as root**. If you log in as another user, run `sudo -i` first and stay root for the whole runbook. Mixing `sudo` and a normal user leaves files with mixed owners.

```sh
# On your machine (from the folder that holds dist/):
scp dist/swoovr-uat-bundle-<commit>.tar.gz root@<VM IPv4>:/tmp/     # or <user>@<VM IPv4>:/tmp/

# On the VM:
ssh root@<VM IPv4>                                       # or ssh <user>@<VM IPv4>, then: sudo -i
apt-get update && apt-get -y full-upgrade && reboot      # once, then log in again (and sudo -i)
mkdir -p /opt/swoovr && mv /tmp/swoovr-uat-bundle-*.tar.gz /opt/swoovr/ && cd /opt/swoovr
tar -xzf swoovr-uat-bundle-<commit>.tar.gz && cd swoovr-uat
./bin/host-setup                                         # or: SWOOVR_SSH_CIDRS="<your admin IP>/32" ./bin/host-setup
docker login -u <dockerhub-user>                          # read-only token at the prompt
```

`SWOOVR_SSH_CIDRS` is optional. It limits SSH on the VM to the address of the machine **you connect from** (your public internet IP, `curl -4 ifconfig.me`), not the VM's own address. Leave it out if that address changes, for example on home internet: SSH then stays open to any address, still key-only. Never use `0.0.0.0` (that blocks everyone); if you get locked out, use the Vultr web console.

`host-setup` finds the public interface from the default route (`enp1s0` on most Vultr VMs). Set `SWOOVR_EXT_IF=<name>` if it guesses wrong. Then harden SSH (key-only, `PasswordAuthentication no`), check that you can still log in, and continue with runbook §2 (TLS and the Cloudflare edge filter).

Optional: **Snapshots → Take snapshot** now. A rebuilt VM from that snapshot skips §5.

## 6. Day-to-day

- **Status:** `./bin/swoovr status`. **Logs:** `./bin/swoovr logs api`.
- **Mailpit** (SMS codes and anything sent to the sink): `ssh -L 8025:127.0.0.1:8025 root@<VM IPv4>`, then open http://127.0.0.1:8025 on your machine.
- **Keycloak admin:** the SSH SOCKS tunnel in the runbook §5.
- **Resize:** Vultr → instance → **Settings → Change Plan**. It reboots the VM. Docker restarts the stack, which signs members out.
- **Rebuild from scratch:** destroy the instance and repeat this page. Or keep the VM and run `./bin/swoovr reset --destroy-uat-data`.
