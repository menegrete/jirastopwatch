#!/usr/bin/env node
"use strict";

// Uploads the release assets to the draft GitHub Release that
// @semantic-release/github created, then publishes it. Run by
// @semantic-release/exec's successCmd (see release.config.js).
//
// Why not let @semantic-release/github upload them: it posts all assets in
// parallel, and the first upload of a large one regularly times out on
// GitHub's side even though the file lands. Its retry then gets 422
// "already_exists" and the whole release job fails, leaving a draft.
// Here each asset is uploaded on its own with `gh release upload --clobber`,
// so a retry replaces a half-finished upload instead of failing on it.
//
// Re-runnable by hand for a release left as a draft, from a checkout with
// release-artifacts/ built for that version:
//   node scripts/upload-release-assets.js <version>

const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const version = process.argv[2];
if (!version) {
  console.error("Usage: node upload-release-assets.js <version>");
  process.exit(1);
}

const tag = `v${version}`;
const artifactsDir = path.join(__dirname, "..", "release-artifacts");
const prefix = `JiraStopWatch-v${version}-`;
const attempts = 4;

const assets = fs
  .readdirSync(artifactsDir)
  .filter((name) => name.startsWith(prefix))
  .sort();

// Four Windows/Linux builds plus a .sha256 each.
if (assets.length !== 8) {
  console.error(`Expected 8 release assets in ${artifactsDir}, found ${assets.length}: ${assets.join(", ")}`);
  process.exit(1);
}

function gh(args) {
  return execFileSync("gh", args, { encoding: "utf8", stdio: ["ignore", "pipe", "inherit"] });
}

function sleep(seconds) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, seconds * 1000);
}

for (const name of assets) {
  for (let attempt = 1; ; attempt++) {
    try {
      gh(["release", "upload", tag, path.join(artifactsDir, name), "--clobber"]);
      console.log(`Uploaded ${name}`);
      break;
    } catch (error) {
      if (attempt === attempts) {
        throw error;
      }
      console.warn(`Upload of ${name} failed (attempt ${attempt} of ${attempts}), retrying`);
      sleep(15 * attempt);
    }
  }
}

// Publish only once every asset is really there, so the auto-updater never
// sees a release without its files.
const uploaded = JSON.parse(gh(["release", "view", tag, "--json", "assets"]))
  .assets.filter((asset) => asset.state === "uploaded")
  .map((asset) => asset.name);
const missing = assets.filter((name) => !uploaded.includes(name));
if (missing.length > 0) {
  console.error(`Not published, assets missing on ${tag}: ${missing.join(", ")}`);
  process.exit(1);
}

gh(["release", "edit", tag, "--draft=false"]);
console.log(`Published ${tag}`);
