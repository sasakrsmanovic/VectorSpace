# Host the collaboration service

The browser editor and collaboration service are separate deployments. GitHub Pages serves the static Uno/WebAssembly application; an ASP.NET process with persistent disk stores shared rooms. The repository includes the service, a Dockerfile, Compose configuration and an optional Render blueprint. Templates do not provision a hosting account or establish that a server is online.

## Local development

Use the .NET SDK pinned by `global.json`. No Uno browser workload is needed to build the backend.

```bash
# Generate a private administrator key; never commit it.
export VECTORSPACE_CREATE_KEY="$(openssl rand -hex 32)"
export VECTORSPACE_DATA="$HOME/.local/share/VectorSpace/rooms"
export VECTORSPACE_ALLOWED_ORIGINS="http://127.0.0.1:4173,http://localhost:4173"

dotnet run --project server/VectorSpace.Server -c Release \
  --urls http://127.0.0.1:5097
```

Keep the key in a password manager or your deployment's secret store. It authorizes **creation of new rooms**, not access to an existing room. A room's owner and guest invitations are independent secrets. Losing an owner link has no in-app account-recovery path; preserve it privately.

Publish and serve the Uno client as described in the root README. In Share, use `http://127.0.0.1:5097`, your name and the creation key. HTTP is accepted only for loopback development. Cross-device use requires an HTTPS server reachable by every participant.

## Docker Compose

```bash
export VECTORSPACE_CREATE_KEY="$(openssl rand -hex 32)"
docker compose up --build -d
curl --fail http://127.0.0.1:5097/health
```

Compose binds the service to loopback, runs its container as the non-root `app` user and mounts a named persistent volume at `/data/vectorspace`. Do not use `docker compose down --volumes` unless deleting every room is intentional. Changing a container or image does not migrate or back up the volume.

The image is built from the pinned SDK and a .NET 10 ASP.NET runtime image. Review and rebuild the runtime image as security updates become available. The repository does not publish an image to a public container registry automatically.

For an externally mounted disk, ensure the directory is writable by the container's `app` user (UID 1654 in the standard Microsoft image). Do not solve a permissions problem by making the room files world-readable. Metadata, journals and local recovery can contain complete designs and embedded images.

## Internet deployment

Place the single service instance behind a maintained HTTPS reverse proxy. Forward normal HTTP requests and allow the 20-second long poll to complete. This version uses HTTP long polling, not a WebSocket endpoint. Keep proxy timeouts greater than 30 seconds and align its request-body limit with the supported service limit; do not enable an unlimited upload policy.

Set these environment variables:

| Variable | Purpose |
|---|---|
| `VECTORSPACE_CREATE_KEY` | Random administrator secret, at least 32 characters. Required at startup. |
| `VECTORSPACE_DATA` | Writable persistent room directory. Must survive container replacement. |
| `VECTORSPACE_ALLOWED_ORIGINS` | Comma-separated exact browser origins, without paths, credentials or wildcards. |
| `ASPNETCORE_URLS` or `--urls` | Listening address. The Docker image listens internally on port 8080. |

For the existing Pages client, the browser origin is `https://wieslawsoltes.github.io`, **not** a URL ending in `/VectorSpace/`. CORS is not authorization; bearer invitations are still required. The creation key should be given only to people allowed to upload new rooms. Do not embed it in the static client or in public workflow logs.

The client rejects remote HTTP endpoints, URL credentials, query/fragment-bearing server URLs and redirects. It asks the user to confirm the endpoint before connecting. The backend rechecks invitation access after a long poll wakes and before applying each mutation. Do not configure a proxy to log Authorization or creation-key headers, request bodies, or invitation fragments from client analytics.

The checked-in `render.yaml` is an optional **paid persistent-service template**, not an already created deployment. Review the selected plan and disk settings in your own account before applying it. It generates a creation key in the hosting environment, disables automatic deploys, mounts persistent storage and allows the Pages origin. Verify disk ownership, retrieve the creation key privately and test a room restart before inviting other users.

## Persistence and recovery

A data directory is owned by one process through `service.lock`. Do not run multiple replicas against the same disk. Each room has initial metadata (`*.room.json`) and an append journal (`*.room.journal`). Metadata stores token hashes, not plaintext invitation tokens. Journal records contain design edits and durable receipts.

The server flushes complete event frames before acknowledgement. On restart, an incomplete final frame is truncated to the last intact boundary; a checksum mismatch in a complete frame aborts room loading. A disk-write error stops further mutation of that room until storage is repaired and the service restarted. A 256 MiB journal limit does not trigger silent history deletion; export to a new room when reaching the limit.

For a consistent backup, stop the service, copy the **entire** data directory to encrypted backup storage and restart. Test restoration into an isolated instance with an isolated client. Preserve room metadata and its journal together. Do not edit journals manually or delete receipt records to fix synchronization errors. Disk snapshots without a stopped/consistent filesystem require separate operational validation.

The service has no automatic compaction, distributed consensus, replication, account administration or managed backup scheduler. Room/client/invitation limits are protection bounds, not a promise of maximum throughput. Memory usage includes full document state, projected cells, receipts, native process buffers and the retained revision window. Profile the intended workload before production use.

## Verify before sharing

Check `/health`, create a room with two separate browser profiles, edit different properties, test own undo, disconnect/reconnect one client, revoke a guest and restart the service. Confirm the document and history survive. Verify the browser's CORS origin and HTTPS trust without disabling certificate checks.

The same workflow can be reproduced through the repository tests:

```bash
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
dotnet build server/VectorSpace.Server -c Release
python3 -m unittest discover -s tests/server -v

# With a published client already served at localhost:4173/VectorSpace/:
npm ci
npx playwright install chromium
python3 scripts/run-collaboration-browser-tests.py --grep @collaboration
```

The browser runner creates a real temporary server with an ephemeral administrator key and destroys its room directory afterward. Tests use real Uno controls and separate browser contexts. It is not a persistent deployment.

**Build** gates browser artifacts on transaction, editor-boundary, HTTP/restart and browser checks. **Pages** verifies the deployed artifact's commit and tests the static editor, while the multi-user tests run against the actual ephemeral backend during Build. Public backend availability is a separate deployment responsibility.

## Scope of access control

Invitation possession is the authorization model. It is not SSO, an account login, verified real-world identity, an immutable audit log or end-to-end encryption. TLS protects transport when properly configured; the service operator and disk owner can read stored designs. Do not represent the service as enterprise-certified or use production-sensitive documents without reviewing the threat model, backing up data and performing the relevant security review.

References: [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), [ASP.NET deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/), [Microsoft .NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/introduction), [Render Blueprint specification](https://render.com/docs/blueprint-spec).
