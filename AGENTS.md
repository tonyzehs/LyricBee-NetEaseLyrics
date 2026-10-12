# Project workflow

- Ask Tony for explicit confirmation before creating or publishing a GitHub release, uploading release assets, or pushing a release tag. Present the prepared build and release contents for review first. Preparing local test builds and committing source changes does not publish a release.
- Release notes may be English only. Download and installation instructions must be available in English and Simplified Chinese.
- Maintain one plugin build with English and Simplified Chinese settings. Interface language is independent of lyric language.
- Keep original lyrics included. Separate extra-content preferences from layout, and place language-specific controls under a language-overrides heading.
- Do not prepare an audit bundle unless requested.
- Develop local test builds on `dev`; keep `master` for reviewed stable updates. Branch names do not enable diagnostics: use the explicit DevelopmentDiagnostics build flag for test/prerelease versions. Stable packages must exclude the recorder and diagnostics UI.
