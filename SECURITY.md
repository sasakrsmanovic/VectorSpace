# Security policy

Do not attach confidential designs or secrets to public issues. Use GitHub private vulnerability reporting when enabled, or ask the repository owner for a private channel without publishing exploit details.

VectorSpace is a local-first alpha. Documents are not encrypted at rest. Browser recovery data is origin-scoped IndexedDB; desktop recovery files use the user's application-data directory. Code with access to the same origin/profile or OS account may be able to read them. Exports are ordinary unencrypted files.

JSON imports validate identifiers, geometry, nesting, format and size. SVG imports prohibit DTDs/external resolution and do not execute active content. Prototype links resolve only to document node identifiers. The application does not execute Figma plugins or arbitrary document code.

Raster exports are limited to 16,384 pixels per edge and 64 megapixels. Resource limits reduce accidental exhaustion but are not a sandbox for hostile native-decoder vulnerabilities. Update Uno/Skia together and review upstream advisories before production deployment.
