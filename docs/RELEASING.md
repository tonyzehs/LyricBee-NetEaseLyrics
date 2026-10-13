# Preparing release notes

Use [.github/RELEASE_TEMPLATE.md](../.github/RELEASE_TEMPLATE.md) for 1.9.5 and later. Keep the changes to three to six bullets about what users will notice. Release notes are English; installation instructions stay bilingual. Put hashes, source identity, regression counts and CI status under the collapsed Build details section.

Fill every placeholder from the actual release candidate and its verification records. Link the download to the installation ZIP, INSTALL.md to the release tag, and the full changes to the previous stable tag. Use the check count from the final packaged DLL; stable and development builds both include the diagnostic report under Advanced.

Preparing notes or a local build does not approve publication. Present the candidate, checksums and final notes to Tony before creating or publishing a release, uploading assets or pushing a release tag.

## Follow-up when 1.9.5 ships

Only after 1.9.5 is published, add this line to 1.9.4's published notes:

> Known issue (fixed in 1.9.5): Saving settings in 1.9.4 and then rolling back to upstream v1.9.0 or v1.8.1 can make those versions accept the first search result. 1.9.5 repairs the saved settings automatically.

At the same time, replace 1.9.4's old restore-the-backup rollback instructions with a link to the current installation guidance. Keep repaired current settings during rollback; historical backups can restore unsafe flags. Older builds can still discard preferences they do not support.

Historical release-note edits must preserve assets, tags, version titles, release status and publication dates. Use the latest-release link for supersession notices so they do not need updating every version.
