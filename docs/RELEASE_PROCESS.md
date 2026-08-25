# Release process

Every Otium version must follow this process:

1. Update the application version and public build metadata.
2. Run formatting verification, Debug and Release builds, and both smoke-test configurations.
3. Update `RELEASE_NOTES.md` in detailed English.
4. Document highlights, behavior changes, security changes, migration notes, validation performed, and known issues.
5. Create an annotated Git tag with an English release summary.
6. Push the release commit and tag to GitHub.
7. Publish a GitHub Release with the same English notes when release tooling is available.
8. Attach the matching signed installer and verification manifest for final public releases.

Final releases must never be published while a documented release blocker remains open. Release candidates and test packages must be labeled clearly and must not be described as signed or production-ready when they are not.
