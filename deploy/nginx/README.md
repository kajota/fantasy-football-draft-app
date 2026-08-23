# Draft board on kellynorton.com

Public read, token-gated write. One nginx location serves every league; each league is a folder.

| League | URL |
|---|---|
| FilthyMothers | https://kellynorton.com/draft/filthymothers/ |
| Football Fanatics | https://kellynorton.com/draft/football-fanatics/ |
| Strata | https://kellynorton.com/draft/strata/ |

`GET` is public (TV, phone, Fire Stick). `PUT` of `board.json` needs `Authorization: Bearer <token>`. `/draft/` itself does not list folders. Write auth is `auth_request` on `/draft/`; the internal location allows GET/HEAD with no token.

## Files

| This directory | On `nginx-server` |
|---|---|
| `draft.conf` | `/etc/nginx/snippets/draft.conf` |
| `draft-publish.conf` | `/etc/nginx/conf.d/draft-publish.conf` |
| `draft-token.map.example` | `/etc/nginx/draft-token.map` (real token, `chmod 600`, not in git) |

`sites-available/kellynorton` HTTPS server includes the snippet:

```nginx
include snippets/draft.conf;
```

## Apply an update

From this repo, as a user who can `ssh nginx` and sudo:

```bash
scp deploy/nginx/draft.conf nginx:/tmp/draft.conf
scp deploy/nginx/draft-publish.conf nginx:/tmp/draft-publish.conf
ssh nginx 'sudo cp /tmp/draft.conf /etc/nginx/snippets/draft.conf && sudo cp /tmp/draft-publish.conf /etc/nginx/conf.d/draft-publish.conf && sudo nginx -t && sudo systemctl reload nginx'
```

Do not copy `draft-token.map` unless you are rotating the token.

## Add a league

On the server (`create_full_put_path` is off, so the folder must exist before the first PUT):

```bash
sudo mkdir -p /var/www/draft/new-slug
sudo chown www-data:www-data /var/www/draft/new-slug
echo '{"status":"ok"}' | sudo -u www-data tee /var/www/draft/new-slug/board.json
```

No nginx reload.

## Check

```bash
curl -i https://kellynorton.com/draft/filthymothers/board.json

curl -i -X PUT --data-binary '{"pick":1}' \
  -H 'Content-Type: application/json' \
  https://kellynorton.com/draft/filthymothers/board.json
# 403

curl -i -X PUT --data-binary '{"pick":1}' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer YOUR_TOKEN' \
  https://kellynorton.com/draft/filthymothers/board.json
# 201 or 204
```

GeoIP on kellynorton.com allows US, CA, and CO (plus LAN `192.168.88.0/24` and VPN `10.0.0.0/8`). The GeoIP.dat on this box is the Debian 2019 dataset. Hairpin NAT from inside the house hits the WAN address, which that database currently tags as IR, so `https://kellynorton.com/...` from the LAN can 403 the **whole site**, not just `/draft/`. From the LAN, `192.168.88.151` (or split-horizon DNS) bypasses that. Remote clients whose IPs still map to US/CA/CO are fine.
