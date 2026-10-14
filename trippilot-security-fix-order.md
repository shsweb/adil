# Cloud agent order: fix security on asktrippilot.com

Apply this in the repository that **builds and deploys** https://asktrippilot.com. This file is the work order only. Do not redesign the product. Do not weaken controls that already work.

Reviewed from the live site on 14 October 2026. Confirm each item in source before editing. Ship the items in the order below.

## Already correct — do not regress

- HTTP redirects to HTTPS. `www` redirects to `https://asktrippilot.com/`.
- TLS 1.0 and 1.1 are disabled. Certificate is Let’s Encrypt and covers `asktrippilot.com` and `www.asktrippilot.com`.
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`.
- `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`.
- nginx does not print its version (`server: nginx` only).
- `/.env`, `/.git/HEAD`, `/.git/config`, and `/admin` return 404.
- `POST /api/login/password` with a bad password returns exactly a generic error: `Wrong email or password.` Keep that. Do not say whether the email exists.
- Password minimum of 8 characters is enforced on the server, not only in the browser.
- Google OAuth uses a random `state`. Cookie `tp_oauth` is `HttpOnly; Secure; SameSite=Lax; Path=/auth/google; Max-Age=600`.
- An absolute `next` such as `https://evil.example` is replaced with `/account`. Keep that behavior and tighten it in item 8.
- Passwords and session tokens are not stored in `localStorage`. Chat history in `localStorage` is only the browser’s copy. Leave that alone.
- The Google OAuth client id in the authorize URL is public by design. Do not treat it as a secret and do not rotate it for this work.

## Rules

- One change set for this order. No unrelated refactors.
- User-facing errors stay generic. Vendor errors, stack traces, and provider JSON are server logs only.
- If a third-party key is missing (Turnstile, Have I Been Pwned), do not take the site down. Rate limits and generic errors must work with no new keys. Document the new env vars.
- After code changes, click through the real site: home, `/plan` (send one short chat and open the map), `/login`, Google button (stop at Google’s page), `/deals`, newsletter form, `/contact`, `/account` when logged out (must still redirect to login).
- Do not commit real secrets, IPs, or customer data.

---

## 1. Stop leaking the email provider error

**Bug.** `POST /api/login` with `{"email":"someone@example.com"}` returned:

```json
{"error":"Email failed (422): {\"message\":\"Invalid `to` field. ...\",\"name\":\"validation_error\",\"statusCode\":422}"}
```

That is Resend’s raw error. The same leak can exist on signup, newsletter subscribe, and any other send.

**Change.**

- Every mail failure returns one generic JSON error. Suggested copy: `If that address can receive mail, we sent a link.` for the magic link, and the existing friendly line for newsletter (`Please enter a valid email.` / a generic send failure).
- Log the provider status and body on the server only.
- Do this for `/api/login`, `/api/signup`, `/api/subscribe`, and any other route that sends mail.

**Done when.** A request to `/api/login` with `someone@example.com` does not contain `422`, `validation_error`, `Resend`, or the provider JSON. A normal-looking address still gets the same generic success shape you use when you do not want to reveal whether the account exists (`{"ok":true}` is fine).

## 2. Rate-limit mail and the AI chat

**Bug.** No rate-limit headers. `POST /api/login` with a random Gmail address returned `{"ok":true}`. `POST /api/chat` works with no session (`{"error":"No messages"}` on an empty body, so the route is open). The login UI says a magic link lasts 20 minutes. Logged-out visitors can plan trips, so chat is the expensive route.

**Change.** Keep any daily free-message quota the product already has. Add abuse limits under it:

| Route | Limit |
| --- | --- |
| `POST /api/login` | 5 per email per hour, and 20 per IP per hour |
| `POST /api/signup` | 5 per IP per hour |
| `POST /api/login/password` | 10 failures per IP per 15 minutes, and 10 per email per 15 minutes, then a temporary block |
| `POST /api/subscribe` | 5 per IP per hour |
| `POST /api/chat` | burst of 10 requests per minute per IP; anonymous daily cap of 30 messages per IP |

- Successful and failed mail sends both count.
- Over the limit: `429` and a short generic message. Do not mention which limit fired in a way that helps an attacker tune the attack.
- If `TURNSTILE_SECRET` is set, require a valid Turnstile token on `/api/login`, `/api/signup`, and `/api/subscribe`. If it is unset, skip the challenge and still enforce the limits. Document `TURNSTILE_SITE_KEY` and `TURNSTILE_SECRET`.

**Done when.** A burst of login and chat calls from one IP returns `429` before the table’s ceiling, and one normal planning session still works.

## 3. SPF and DMARC (DNS — owner publishes these)

**Bug.** `resend._domainkey.asktrippilot.com` exists. The apex TXT record is only Google Search Console. `_dmarc.asktrippilot.com` is `v=DMARC1; p=none;`. There is no MX. Forged mail from this domain is not rejected.

**Change.** Do not invent an SPF string in code. In Resend → Domains, copy the records for the domain that actually sends (From address and return-path). Publish those. Then set DMARC, in this order:

1. Publish the Resend SPF and DKIM records exactly as the dashboard shows them.
2. Set `_dmarc` to quarantine, with a report mailbox the owner actually reads. If no mailbox exists yet, do not invent one; leave a note in the deploy docs and use quarantine only after `rua` has a real inbox.

```text
v=DMARC1; p=quarantine; adkim=s; aspf=r; rua=mailto:REPLACE_WITH_A_REAL_MAILBOX
```

3. After reports look clean for a while, move `p=quarantine` to `p=reject`. Do not start at `p=reject`.

**Done when.** `dig TXT` shows the Resend SPF the dashboard asked for, DKIM still verifies, and DMARC is `p=quarantine` or stronger with a real `rua`.

## 4. Enforce a real Content-Security-Policy

**Bug.** The enforced policy is only:

```text
frame-ancestors 'self'; base-uri 'self'; object-src 'none'; form-action 'self'
```

The stricter policy is `Content-Security-Policy-Report-Only` and it allows `'unsafe-inline'` for scripts. Inline scripts on the home page (the ask box, JSON-LD) would run if injected. Report-Only does not block anything.

**Change.**

- Inventory every script, style, font, image, connect, and frame the live pages use, including the map on `/plan` (tile hosts), `/vendor/gsap.min.js`, `/vendor/ScrollTrigger.min.js`, `/vendor/leaflet/leaflet.js`, and first-party files such as `/auth.js` and `/plan.js`.
- Per request, generate a CSP nonce. Put it on every inline `<script>`, including JSON-LD.
- Enforce (not only report) a policy shaped like this, expanded with the hosts the inventory found:

```text
default-src 'self';
script-src 'self' 'nonce-{nonce}';
style-src 'self' 'unsafe-inline';
font-src 'self';
img-src 'self' data: https:;
connect-src 'self';
object-src 'none';
base-uri 'self';
frame-ancestors 'self';
form-action 'self';
upgrade-insecure-requests
```

- Do not ship `script-src` with `'unsafe-inline'`. A nonce makes browsers ignore `'unsafe-inline'` anyway, so every inline script needs the nonce.
- Keep `'unsafe-inline'` on `style-src` only if the inventory shows inline styles you cannot nonce in this pass.
- Add map, font, or connect hosts you actually saw. Do not add broad wildcards to get the page green.
- Remove the Report-Only header once the enforced policy matches what you verified. Until that browser pass is done, you may send both, but the final state is enforced only.

**Done when.** Response headers on `/`, `/plan`, and `/login` show an enforced `script-src` with a nonce and no `'unsafe-inline'`. Home, planner, map, login, and newsletter still work. A page view does not throw CSP errors in the console for first-party code.

## 5. Take `/stats` off public Basic Auth

**Bug.** `GET /stats` returns `401` with `WWW-Authenticate: Basic realm="TripPilot stats"`. The admin surface is on the public hostname. Basic auth has no second factor, and browsers store the password.

**Change.**

- Remove HTTP Basic from the public site.
- Prefer the existing logged-in account, and allow `/stats` only for an admin flag you already have.
- If there is no admin role, do not invent a public password. Disable the route (404) unless `STATS_ALLOW_IPS` is set. That variable is a comma-separated IP allowlist, default empty, and empty means deny everyone. Never default it to `0.0.0.0/0`. Do not commit real IPs.

**Done when.** `https://asktrippilot.com/stats` from the public internet does not offer Basic auth. An allowed admin or an allowlisted IP still reaches it. Everyone else gets 401 or 404 with no `WWW-Authenticate: Basic`.

## 6. Reject cross-site POSTs

**Bug.** `POST /api/login`, `POST /api/account/save`, and `POST /api/chat` with `Origin: https://evil.example` were handled as normal requests (`400`, `401`, `400`), not rejected for origin. `SameSite=Lax` on `tp_oauth` blocks a lot of browser CSRF. The server should check origin as well.

**Change.**

- For `POST`, `PUT`, `PATCH`, and `DELETE` under `/api/` and `/auth/`:
  - If `Origin` is present, it must be `https://asktrippilot.com`.
  - Reject other origins, including `null`, with `403` and a generic error.
  - If `Origin` is missing, allow the request only when `Sec-Fetch-Site` is missing or `same-origin` (same-origin browser fetches and non-browser callers). Reject `Sec-Fetch-Site: cross-site`.
- Find the session cookie (not only `tp_oauth`). It must be `HttpOnly`, `Secure`, and `SameSite=Lax` or `Strict`. Set those flags if any are missing. Do not put the session in `localStorage` or a non-HttpOnly cookie.

**Done when.** The same three routes with `Origin: https://evil.example` return `403`. The site’s own login, save, and chat still work in the browser.

## 7. Stronger passwords, without locking current users out

**Bug.** Signup and password login only require 8 characters. There is no breach check. The server already returns `Your password needs at least 8 characters.` for a 7-character signup, so keep enforcement on the server.

**Change.**

- New passwords (signup and any password change): at least 12 characters.
- On those same paths, reject passwords found in breaches via the Have I Been Pwned range API (k-anonymity): send only the first 5 characters of the SHA-1, compare the suffix locally. Never send the password or the full hash.
- If the HIBP request fails or times out, allow the password and log the failure. Do not block signup because HIBP is down.
- Existing accounts: login stays at “8 or more, and the password matches.” Do not force a reset in this change. Do not run HIBP on every login in a way that blocks login when HIBP is down.
- Keep the generic `Wrong email or password.` on failed login.
- Update the hint in `auth.js` (“At least 8 characters.” / French copy) so it matches the new signup rule.

**Done when.** Signup with `12345678901` (11 chars) is rejected. Signup with a long unique password still works. Login for an existing 8-character password still works. The browser never receives a HIBP response body.

## 8. Tighten the OAuth `next` path

**Bug.** `next` values that are full URLs correctly fall back to `/account`. These were stored in `tp_oauth` anyway (base64 after the state dot):

- `/%2f%2fevil.example` stored as the decoded-looking path `/%2f%2fevil.example`
- `/%09/evil.example` stored as `/%09/evil.example`
- `/account?x=1` stored as `/account?x=1` (this one is fine)

If the callback later decodes and redirects, a value that becomes `//evil.example` is an open redirect.

**Change.** Decode `next` once. Accept it only if all of these are true:

- It starts with a single `/`.
- It does not start with `//` or `/\`.
- It contains no `\`, tab, newline, or carriage return.
- It contains no `://`.
- A second decode does not produce a string that fails the rules above.

Otherwise use `/account`. Allow a normal path plus query, such as `/account?x=1`.

**Done when.** After Google returns, the site only redirects to a same-site path. These inputs all end at `/account`: `https://evil.example`, `//evil.example`, `/\evil.example`, `https://asktrippilot.com.evil.example`, `/%2f%2fevil.example`, `/%09/evil.example`. `/account?x=1` still returns there.

## 9. Magic links: single use, 15 minutes or less

**Bug.** `auth.js` tells the user the email link works for 20 minutes. A long-lived reusable link is a stolen-inbox risk.

**Change.**

- Store the link token hashed.
- Expire it in 15 minutes or less.
- Mark it used on first successful login so a second click fails.
- Update the English and French strings in `auth.js` that say 20 minutes.

**Done when.** A used link cannot sign in again, an expired link cannot sign in, and the UI text matches the TTL.

## 10. `security.txt` and CAA

**`security.txt`.** Serve `text/plain` at `https://asktrippilot.com/.well-known/security.txt`:

```text
Contact: https://asktrippilot.com/contact
Expires: 2027-10-14T00:00:00.000Z
Preferred-Languages: en, fr
Canonical: https://asktrippilot.com/.well-known/security.txt
```

There is no public mailbox on the site today; the contact page is `/contact`. Use that. Do not invent an email address.

**CAA (DNS — owner publishes this).** Only Let’s Encrypt should issue for the domain:

```text
asktrippilot.com. CAA 0 issue "letsencrypt.org"
asktrippilot.com. CAA 0 issuewild "letsencrypt.org"
```

Add `iodef` only if a real security mailbox exists.

**Done when.** `GET /.well-known/security.txt` returns 200 and `text/plain` with the body above. CAA is published at the DNS host, or the deploy notes list the two records if the agent cannot change DNS.

---

## Verify before you finish

1. Headers on `/`: HSTS, `nosniff`, frame protection, referrer policy, and the enforced CSP from item 4 are still present.
2. `POST /api/login` with `someone@example.com` returns a generic body. No provider name, status code, or JSON from Resend.
3. Cross-site `Origin` on `POST /api/login`, `/api/account/save`, and `/api/chat` returns 403. The same calls from the site work.
4. `/stats` does not send `WWW-Authenticate: Basic`.
5. Password login failure text is still only `Wrong email or password.`
6. Browser pass: home ask box goes to `/plan`, map tiles load, chat returns a normal answer, login modal opens, Google button reaches `accounts.google.com` with `redirect_uri=https://asktrippilot.com/auth/google/callback`, newsletter and contact forms submit.
7. `/.well-known/security.txt` is the file in item 10.

## Out of scope

- TLS configuration, HSTS preload, and cipher changes. Those are already fine.
- Rotating the Google OAuth client id.
- Rewriting the planner, payments, or page design.
- Pen-testing beyond the checks in this order. No exploit payloads, no account attacks, no scanning that is not a single request to confirm a fix.
