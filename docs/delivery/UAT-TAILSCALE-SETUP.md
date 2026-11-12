# Tailscale on the UAT VM (manual)

D362 preparation. **The owner does this by hand; no workflow or script installs, enrols or configures Tailscale.** It gives private SSH and private Keycloak administration from your own devices. Until you've verified it, keep using the existing SSH SOCKS tunnel ([runbook](../../deploy/vultr-uat/README.md#keycloak-admin-console)). Tailscale was not installed on the VM when it was inspected on 12 November 2026.

## How Keycloak admin access works today, and what changes

- Caddy serves `auth-uat.swoovr.app` publicly, but answers `/admin…` and `/realms/master…` with **404** unless the direct peer is on the Docker edge network `172.30.0.0/24`. `remote_ip` is the TCP peer, never a forwarded header.
- Public packets reach Caddy only from Cloudflare's ranges: the `SWOOVR-EDGE` chain in `DOCKER-USER` filters ports 80/443 arriving on the public interface `enp1s0`.
- Keycloak's hostname stays `https://auth-uat.swoovr.app` (`KC_HOSTNAME`). The console must be opened at that name with a certificate the browser trusts. Caddy serves the Cloudflare Origin CA certificate, so the admin browser profile must trust the Cloudflare Origin CA RSA root, exactly as with the SOCKS tunnel.

With Tailscale, your device connects to the VM's Tailscale address on port 443. Docker's published port forwards it to Caddy with the source address preserved (`userland-proxy: false` in `/etc/docker/daemon.json`), so Caddy sees your `100.x.y.z` address. The bundle supports an optional allow-list for exactly that: `env/caddy-admin.env` with `SWOOVR_ADMIN_PRIVATE_NETWORKS`. It is empty by default, so behaviour is unchanged. Packets on `tailscale0` are not subject to the Cloudflare-only edge filter (it matches `-i enp1s0` only), and the public internet can't present a `100.64.0.0/10` peer address on `enp1s0`, because that filter admits only Cloudflare ranges. **No separate hostname, certificate or Keycloak setting is needed.**

Rejected alternatives: a separate admin hostname (needs a new certificate SAN and `KC_HOSTNAME_ADMIN`, so the origin certificate would change); exposing Keycloak's port directly on `tailscale0` (bypasses Caddy's log redaction and adds a second TLS setup); Tailscale in Docker (excluded by the owner).

## 1. Install on Debian (as root on the VM)

Follow Tailscale's Debian instructions (they publish the repository key and list for trixie):

```sh
curl -fsSL https://pkgs.tailscale.com/stable/debian/trixie.noarmor.gpg -o /usr/share/keyrings/tailscale-archive-keyring.gpg
curl -fsSL https://pkgs.tailscale.com/stable/debian/trixie.tailscale-keyring.list -o /etc/apt/sources.list.d/tailscale.list
apt-get update && apt-get install -y tailscale
systemctl enable --now tailscaled
```

Check the URLs against <https://tailscale.com/download/linux/debian-trixie> before running them.

## 2. Enrol the device

```sh
tailscale up --ssh=false     # prints a login URL: open it on your Mac and approve the device in your tailnet
tailscale ip -4              # the VM's 100.x.y.z address
tailscale status
```

Don't put an auth key in a file or a command. In the admin console (<https://login.tailscale.com/admin/machines>), rename the machine `swoovr-uat`. Consider **Disable key expiry** for this server, so it isn't dropped from the tailnet without notice. Keep `--ssh=false`: plain OpenSSH over Tailscale keeps the existing key-only root login and audit trail.

## 3. MagicDNS

Tailnet admin → **DNS** → enable **MagicDNS**. The VM is then reachable as `swoovr-uat` (or `swoovr-uat.<tailnet>.ts.net`) from your devices.

## 4. SSH over Tailscale

```sh
ssh root@swoovr-uat          # from your Mac, while Tailscale runs there
```

The host firewall's input chain accepts TCP 22 on every interface, so no rule change is needed. Tailscale's own UDP 41641 is dropped by the `inet swoovr_host` input policy. Tailscale still works through its relays (DERP), only slower. To allow direct connections (optional host firewall change, owner decision), add `udp dport 41641 accept` to `/etc/swoovr/host.nft` and `nft -f /etc/swoovr/host.nft`. UFW is also active on the VM; if you rely on UFW, `ufw allow 41641/udp`. Neither is required.

Keep the public SSH path until Tailscale has worked for a while. The GitHub deployment still uses public SSH ([UAT-GITHUB-DEPLOYMENT.md](UAT-GITHUB-DEPLOYMENT.md) prerequisite 5).

## 5. Keycloak admin through Tailscale

1. On the VM, allow your devices' Tailscale addresses (one `/32` each; avoid the whole `100.64.0.0/10`, which would admit every device in the tailnet):

   ```sh
   cd /opt/swoovr/swoovr-uat
   ( umask 077; echo 'SWOOVR_ADMIN_PRIVATE_NETWORKS=100.101.102.103/32' > env/caddy-admin.env )   # your Mac's `tailscale ip -4`
   ./bin/swoovr compose up -d --no-deps --force-recreate caddy
   ```

   This file survives deployments (env files are never overwritten) and refreshes.
2. In the tailnet **Access controls**, limit port 443 and 22 on `swoovr-uat` to your own user (`"dst": ["tag:swoovr-uat:22,443"]` or the machine), so other tailnet members can't use the allow-list.
3. On your Mac, make `auth-uat.swoovr.app` resolve to the VM's Tailscale address **only in the admin browser profile's context**. The simplest way is a hosts entry while you work:

   ```sh
   sudo sh -c 'echo "100.x.y.z auth-uat.swoovr.app # swoovr-uat-admin" >> /etc/hosts'    # the VM's tailscale ip -4
   ```

   Remove it afterwards (`sudo sed -i '' '/# swoovr-uat-admin$/d' /etc/hosts`), because while it's present every app on your Mac sends `auth-uat` traffic over Tailscale, bypassing Cloudflare. MagicDNS can't override a public name without a split-DNS server, so the hosts entry is the reliable option.
4. Use the `swoovr-uat-admin` Firefox profile that trusts the Cloudflare Origin CA root (runbook "Keycloak admin console", part B), **without** the SOCKS proxy (set it to *No proxy*). Open `https://auth-uat.swoovr.app/admin/`.

## 6. Verification

```sh
# On the VM
tailscale status; tailscale ip -4
ss -lntp | grep ':443'                       # docker-proxy not listed (userland-proxy false); DNAT handles 443
iptables -S DOCKER-USER                      # still only the -i enp1s0 jumps to SWOOVR-EDGE
cat /opt/swoovr/swoovr-uat/env/caddy-admin.env
./bin/swoovr compose logs --tail 20 caddy | grep -c '"/admin/' || true   # admin requests appear with your 100.x address
# On your Mac (with the hosts entry)
curl -sk -o /dev/null -w '%{http_code}\n' https://auth-uat.swoovr.app/admin/       # 200 or 302 over Tailscale
# On your Mac WITHOUT the hosts entry, or from any other network
curl -s -o /dev/null -w '%{http_code}\n' https://auth-uat.swoovr.app/admin/        # must stay 404 through Cloudflare
```

The deployment workflow also checks on every run that `/admin/` and `/realms/master` answer 404 through Cloudflare.

## 7. Rollback

```sh
rm -f /opt/swoovr/swoovr-uat/env/caddy-admin.env
cd /opt/swoovr/swoovr-uat && ./bin/swoovr compose up -d --no-deps --force-recreate caddy   # back to edge-network only
tailscale down                       # leave the tailnet without uninstalling
apt-get remove -y tailscale && rm -f /etc/apt/sources.list.d/tailscale.list   # to remove completely
```

Remove the machine in the tailnet admin console, and undo any `41641/udp` firewall rule you added. Then remove the hosts entry on your Mac. The SOCKS tunnel method works throughout, because it never depended on Tailscale.
