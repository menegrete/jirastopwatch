#!/usr/bin/env node
"use strict";

const { execFileSync } = require("child_process");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const version = process.argv[2];
if (!version) {
  console.error("Usage: node publish-release-artifacts.js <version>");
  process.exit(1);
}

const repoRoot = path.join(__dirname, "..");
const projectPath = path.join(repoRoot, "source", "StopWatch", "StopWatch.csproj");
const artifactsDir = path.join(repoRoot, "release-artifacts");
const selfContainedOut = path.join(artifactsDir, "self-contained");
const frameworkDependentOut = path.join(artifactsDir, "framework-dependent");

fs.rmSync(artifactsDir, { recursive: true, force: true });
fs.mkdirSync(artifactsDir, { recursive: true });

function publish(outDir, selfContained, extraArgs = []) {
  execFileSync(
    "dotnet",
    [
      "publish",
      projectPath,
      "-c", "Release",
      "-r", "win-x64",
      `--self-contained=${selfContained}`,
      "-p:PublishSingleFile=true",
      "-o", outDir,
      ...extraArgs,
    ],
    { stdio: "inherit" }
  );
}

// The self-contained build gets a compile-time marker so the running app can
// tell it apart from the framework-dependent one at runtime (auto-update
// needs to know which release asset matches what it's currently running).
publish(selfContainedOut, true, ["-p:DefineConstants=STOPWATCH_SELF_CONTAINED"]);
publish(frameworkDependentOut, false);

const selfContainedAsset = path.join(artifactsDir, `JiraStopWatch-v${version}-self-contained.exe`);
fs.renameSync(path.join(selfContainedOut, "StopWatch.exe"), selfContainedAsset);

const frameworkDependentZip = path.join(artifactsDir, `JiraStopWatch-v${version}-framework-dependent.zip`);
execFileSync(
  "powershell",
  [
    "-NoProfile",
    "-Command",
    `Compress-Archive -Path '${frameworkDependentOut}\\*' -DestinationPath '${frameworkDependentZip}'`,
  ],
  { stdio: "inherit" }
);

// A SHA256 checksum published alongside each asset, so both the app's
// auto-updater and a human downloading by hand can verify what they got.
// The "<hash>  <filename>" format matches sha256sum's, so it's also
// verifiable with `sha256sum -c` or `certutil -hashfile`.
function writeChecksum(assetPath) {
  const digest = crypto.createHash("sha256").update(fs.readFileSync(assetPath)).digest("hex");
  fs.writeFileSync(`${assetPath}.sha256`, `${digest}  ${path.basename(assetPath)}\n`);
}

writeChecksum(selfContainedAsset);
writeChecksum(frameworkDependentZip);

console.log(`Artifacts ready in ${artifactsDir}`);
