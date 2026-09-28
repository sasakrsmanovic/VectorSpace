# Security

Report vulnerabilities privately through the repository's Security advisories reporting feature when available. Otherwise contact the maintainer using the public contact information on their GitHub profile and request a private channel. Do not post live invitation tokens, creation keys, confidential documents or exploit-ready production details in public issues.

## Local files and imports

Local editing has no account requirement. Browser IndexedDB and desktop profile recovery are not encrypted backup services. Export important files and protect the device/profile. Native documents can embed images and comments. SVG parsing prohibits external entities and DTD resolution; inputs are validated and bounded before entering the editor. Unsupported formats are not executed as scripts or plugins.

## Collaboration trust boundary

Sharing sends the complete selected document to a user-reviewed service. Remote HTTP, URL credentials and server query/fragment parameters are rejected; HTTP loopback is reserved for development. Redirects are disabled. Invitation tokens travel in Authorization headers, never URL queries; invitation fragments are removed from browser history before consent. Secrets are excluded from read-only diagnostics and local recovery journals. Avoid third-party analytics that capture browser URLs, text inputs or request headers.

Invitation possession is authorization. Viewer/commenter/editor/owner permissions are server enforced, and guest invitations can be revoked. Display names and document comment authors are not verified real-world identities. Use separate invitations for attribution and protect owner links. Revocation cannot erase already downloaded copies. There is no SSO, account recovery, immutable audit log or enterprise certification.

The service stores token hashes but complete readable designs/journal data. TLS protects properly configured transport; the service operator and disk owner can read shared content. This is not end-to-end encryption. Protect the creation key separately: it permits creating new rooms, not reading existing rooms. Exact-origin CORS is not a substitute for authorization.

## Hosting

Use maintained HTTPS infrastructure, private persistent storage, backups, explicit origins and secret management. Do not log Authorization/creation-key headers or request bodies. A single process owns a data directory; horizontal scaling against one journal is unsupported. Checksummed flush-before-ack storage handles tested restart cases, not every hardware/filesystem power-loss scenario. Complete corruption fails closed. Enforce proxy upload/concurrency/time limits and profile intended workloads before production use.

GitHub Pages is a static client. Deployment templates do not create a public backend automatically. Review paid-service plans, disk permissions and lifecycle before provisioning. Keep dependencies/runtime images updated and perform an independent security review before using sensitive production designs.

Prototype external links require user confirmation and do not execute a plugin runtime. Native `.fig`, plugin execution and remote published libraries remain unsupported. See [collaboration](docs/COLLABORATION.md), [hosting](docs/HOSTING.md) and [compatibility limits](docs/FEATURES.md).
