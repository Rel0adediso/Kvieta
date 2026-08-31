# Release process

Every Otium version must follow this process:

1. Update the application version and public build metadata.
2. Run formatting verification, Debug and Release builds, and both smoke-test configurations.
3. Update `docs/RELEASE_NOTES.md` in detailed English.
4. Document highlights, behavior changes, security changes, migration notes, validation performed, and known issues.
5. Create an annotated Git tag with an English release summary.
6. Push the release commit and tag to GitHub.
7. Publish a GitHub Release with the same English notes when release tooling is available.
8. Attach the matching installer, verification manifest, and SHA-256 values. State clearly whether the package is Authenticode-signed.

For the second community prerelease, the public release name is **Otium Alpha 2**.
Use `alpha-2` for the Git tag and `Alpha-2` where a package-safe label is needed;
do not present `v1.0.0-alpha.2` as the product name. The numeric MSI version stays
`1.0.0` so Windows Installer can service the existing product correctly.

Final releases must never be published while a documented release blocker remains open. Release candidates and test packages must be labeled clearly and must not be described as signed or production-ready when they are not.

Otium currently plans an unsigned, non-commercial community release. It must remain technically distinct from Debug/test packages: no development bypass may be compiled in, manifest and source commit must match, Guardian client identity checks must pass, and the expected Windows SmartScreen warning must be documented. Authenticode can be added later without changing the open-source license.
