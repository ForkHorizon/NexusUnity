# Security Policy

Nexus Unity is a local Unity Editor automation package. It intentionally exposes powerful editor operations, including file writes, asset changes, play mode control, reflection-based inspection, and code compilation. Treat the server as a trusted local developer tool, not as a network service.

## Supported Versions

| Version | Supported |
|---|---|
| `1.0.x` | Yes |
| Pre-public `2.x` / `3.x` internal builds | No |

## Security Model

- The server binds only to loopback addresses (`127.0.0.1` / `localhost`).
- HTTP and WebSocket requests are rejected unless the request URL is loopback.
- Browser `Origin` headers, when present, must also be loopback `http` or `https` origins.
- File APIs resolve paths and enforce the Unity project root boundary.
- HTTP and WebSocket payloads are capped to reduce memory exhaustion risk.
- No remote authentication layer is provided. Do not proxy or expose the server to a LAN, VPN, container bridge, tunnel, or public internet endpoint.

## Reporting a Vulnerability

Please report suspected vulnerabilities privately through GitHub Security Advisories for the public repository:

```text
https://github.com/ForkHorizon/NexusUnity/security/advisories/new
```

Include:

- Affected Nexus Unity version and Unity version.
- Operating system.
- Reproduction steps or proof-of-concept payload.
- Expected impact.
- Whether the server was exposed beyond loopback.

Do not include secrets, tokens, private project source, or proprietary assets in the report. Use `[REDACTED]` for sensitive values.

## Disclosure Target

We aim to acknowledge valid reports within 7 days and publish a fix or mitigation guidance before public disclosure whenever practical.
