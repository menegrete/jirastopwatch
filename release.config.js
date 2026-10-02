"use strict";

// semantic-release configuration. Two release channels:
//   - main: stable releases. Commits CHANGELOG.md and the AssemblyInfo.cs
//     version back to main.
//   - rc*:  X.Y.Z-rc.N pre-releases (marked as pre-release on GitHub, so
//     `releases/latest` and the auto-updater ignore them). Nothing is
//     committed back to the rc branch, to avoid version/changelog conflicts
//     when it is merged into main; the tag and the GitHub Release are still
//     created.
//
// semantic-release can't condition a plugin on the branch, hence a JS config:
// the changelog and git plugins are only listed on main.

const branch = process.env.GITHUB_REF_NAME;
const isMain = branch === "main";

const changelogTitle = [
  "# Changelog",
  "",
  "All notable changes to this project are documented in this file.",
  "",
  "The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),",
  "and this project's version numbers are the ones in",
  "`source/StopWatch/Properties/AssemblyInfo.cs`",
  "(`AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion`).",
  "",
  "Entries for versions released before this file existed are not",
  "reconstructed here — see `git log` and the archived proposals under",
  "`openspec/changes/archive/` for that history.",
].join("\n");

const plugins = [
  "@semantic-release/commit-analyzer",
  [
    "@semantic-release/release-notes-generator",
    {
      preset: "conventionalcommits",
      presetConfig: {
        types: [
          { type: "feat", section: "Added" },
          { type: "fix", section: "Fixed" },
          { type: "perf", section: "Changed" },
          { type: "revert", section: "Removed" },
          { type: "refactor", hidden: true },
          { type: "docs", hidden: true },
          { type: "style", hidden: true },
          { type: "test", hidden: true },
          { type: "build", hidden: true },
          { type: "ci", hidden: true },
          { type: "chore", hidden: true },
        ],
      },
    },
  ],
];

if (isMain) {
  plugins.push([
    "@semantic-release/changelog",
    { changelogFile: "CHANGELOG.md", changelogTitle },
  ]);
}

plugins.push([
  "@semantic-release/exec",
  {
    prepareCmd: "node scripts/set-assembly-version.js ${nextRelease.version}",
    publishCmd: "node scripts/publish-release-artifacts.js ${nextRelease.version}",
  },
]);

if (isMain) {
  plugins.push([
    "@semantic-release/git",
    {
      assets: ["CHANGELOG.md", "source/StopWatch/Properties/AssemblyInfo.cs"],
      message: "chore(release): ${nextRelease.version} [skip ci]\n\n${nextRelease.notes}",
    },
  ]);
}

plugins.push([
  "@semantic-release/github",
  {
    assets: [
      { path: "release-artifacts/JiraStopWatch-v*-self-contained.exe", label: "JiraStopWatch-v${nextRelease.version}-self-contained.exe" },
      { path: "release-artifacts/JiraStopWatch-v*-self-contained.exe.sha256", label: "JiraStopWatch-v${nextRelease.version}-self-contained.exe.sha256" },
      { path: "release-artifacts/JiraStopWatch-v*-framework-dependent.zip", label: "JiraStopWatch-v${nextRelease.version}-framework-dependent.zip" },
      { path: "release-artifacts/JiraStopWatch-v*-framework-dependent.zip.sha256", label: "JiraStopWatch-v${nextRelease.version}-framework-dependent.zip.sha256" },
    ],
  },
]);

module.exports = {
  branches: ["main", { name: "rc*", prerelease: "rc" }],
  plugins,
};
