# 🔐 Keydral

**Keydral is a local-first secrets and key management service designed for Kubernetes and on-prem environments.**

It provides secure storage, access control, and delivery of secrets, cryptographic keys, and certificates — with a strong focus on developer experience and simple operations.

Keydral enables teams to run a trusted secrets platform locally, in edge clusters, or in private infrastructure without relying on external cloud services.

---

## ✨ Features

- 🔐 Encrypted secret storage (encryption at rest and in transit)
- 🧾 Secret versioning and audit logging
- 👤 Identity-based authentication (OIDC / service accounts / mTLS)
- 🛡️ Role-based access control (RBAC)
- ☸️ Kubernetes-native secret delivery (CSI driver / operator support)
- 🧰 Developer-friendly CLI
- 🏠 Local-first architecture — run fully on-prem or in local clusters
- 📦 API-first design with OpenAPI support

---

## 🚀 Example CLI Usage

```bash
keydral login

keydral secret set db-password
keydral secret get db-password

keydral policy apply team-a.yaml
```

---

## 🏗️ Architecture Overview

Keydral is designed as a lightweight, cloud-native trust service:

- Stateless API service
- Secure encryption layer using envelope encryption
- Pluggable storage backend (e.g. PostgreSQL)
- Identity provider integration (OIDC)
- Kubernetes integration via CSI driver and operator patterns

---

## 🎯 Use Cases

- Local Kubernetes development clusters
- On-prem production environments
- Edge computing deployments
- Secure secret distribution for microservices
- Replacement or complement to cloud-managed secret stores

---

## 🔐 Security Principles

- Secrets are never stored in plaintext
- TLS required for all communication
- Short-lived access tokens
- Strict server-side authorization
- Full audit trail for secret access
- Designed for secure multi-tenant usage

---

## �️ Getting Started

The fastest way to start developing Keydral is with .NET Aspire, which orchestrates the entire stack (API, PostgreSQL, Keycloak) automatically:

1. Install [Podman](https://podman.io/docs/installation) (or [Docker](https://docs.docker.com/get-docker/)) and [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0).
   - **Recommended**: [Podman Desktop](https://podman.io/podman-desktop/) for a seamless GUI experience.
2. Clone the repo and start Aspire:
   ```bash
   git clone https://github.com/LittleAndi/keydral.git && cd keydral
   cd src/Keydral.AppHost && dotnet run
   ```
3. Open `http://localhost:5001/swagger` (API) or `http://localhost:8080/admin` (Keycloak admin).

For alternative setup options (Dev Container, manual Docker Compose), see [docs/SETUP.md](docs/SETUP.md).

---

## �📦 Project Status

Keydral is currently in early development.
APIs and features may change as the architecture evolves.

---

## 🤝 Contributing

Contributions, ideas, and discussions are welcome.

---

## 📜 License

See [LICENSE](./LICENSE)
