#!/usr/bin/env node
"use strict";

const { execFileSync } = require("child_process");
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

function publish(outDir, selfContained) {
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
    ],
    { stdio: "inherit" }
  );
}

publish(selfContainedOut, true);
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

console.log(`Artifacts ready in ${artifactsDir}`);
