#!/usr/bin/env node
"use strict";

const { execFileSync } = require("child_process");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

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
const linuxSelfContainedOut = path.join(artifactsDir, "linux-self-contained");
const linuxFrameworkDependentOut = path.join(artifactsDir, "linux-framework-dependent");

fs.rmSync(artifactsDir, { recursive: true, force: true });
fs.mkdirSync(artifactsDir, { recursive: true });

function publish(rid, outDir, selfContained, extraArgs = []) {
  execFileSync(
    "dotnet",
    [
      "publish",
      projectPath,
      "-c", "Release",
      "-r", rid,
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
publish("win-x64", selfContainedOut, true, ["-p:DefineConstants=STOPWATCH_SELF_CONTAINED"]);
publish("win-x64", frameworkDependentOut, false);
// The Linux builds are cross-published from this (Windows) runner. Their
// asset names carry a "linux-x64" infix; the Windows names above must not
// change, so that the 3.x updater finds this release.
publish("linux-x64", linuxSelfContainedOut, true, ["-p:DefineConstants=STOPWATCH_SELF_CONTAINED"]);
publish("linux-x64", linuxFrameworkDependentOut, false);

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

// A .tar.gz of the top-level files of a publish folder, with the
// executable (StopWatch) at mode 0755 and everything else at 0644. Written
// by hand because tar.exe on a Windows runner stores every file as 0666, and
// the execute bit has to survive extraction (the app's auto-updater, and a
// human running tar -xzf). Names must fit ustar's 100-character field.
function writeTarGz(tarGzPath, dir, onlyFiles) {
  const names = fs.readdirSync(dir, { withFileTypes: true })
    .filter((e) => e.isFile() && (!onlyFiles || onlyFiles.includes(e.name)))
    .map((e) => e.name)
    .sort((a, b) => (a === "StopWatch" ? -1 : b === "StopWatch" ? 1 : a.localeCompare(b)));
  if (!names.includes("StopWatch")) {
    throw new Error(`No StopWatch executable in ${dir}`);
  }

  const mtime = Math.floor(Date.now() / 1000);
  const blocks = [];
  for (const name of names) {
    const data = fs.readFileSync(path.join(dir, name));
    const header = Buffer.alloc(512);
    header.write(name, 0, 100, "utf8");
    header.write((name === "StopWatch" ? "0000755" : "0000644") + "\0", 100, 8, "ascii");
    header.write("0000000\0", 108, 8, "ascii"); // uid
    header.write("0000000\0", 116, 8, "ascii"); // gid
    header.write(data.length.toString(8).padStart(11, "0") + "\0", 124, 12, "ascii");
    header.write(mtime.toString(8).padStart(11, "0") + "\0", 136, 12, "ascii");
    header.write("        ", 148, 8, "ascii"); // checksum field counts as spaces
    header.write("0", 156, 1, "ascii"); // regular file
    header.write("ustar\0" + "00", 257, 8, "ascii");
    let sum = 0;
    for (const byte of header) sum += byte;
    header.write(sum.toString(8).padStart(6, "0") + "\0 ", 148, 8, "ascii");
    blocks.push(header, data, Buffer.alloc((512 - (data.length % 512)) % 512));
  }
  blocks.push(Buffer.alloc(1024)); // end-of-archive marker
  fs.writeFileSync(tarGzPath, zlib.gzipSync(Buffer.concat(blocks), { level: 9 }));
}

const linuxSelfContainedTarGz = path.join(artifactsDir, `JiraStopWatch-v${version}-linux-x64-self-contained.tar.gz`);
const linuxFrameworkDependentTarGz = path.join(artifactsDir, `JiraStopWatch-v${version}-linux-x64-framework-dependent.tar.gz`);
// Self-contained: the bare executable (the native libraries are bundled into
// it, see StopWatch.csproj). Framework-dependent: the whole publish folder.
writeTarGz(linuxSelfContainedTarGz, linuxSelfContainedOut, ["StopWatch"]);
writeTarGz(linuxFrameworkDependentTarGz, linuxFrameworkDependentOut);

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
writeChecksum(linuxSelfContainedTarGz);
writeChecksum(linuxFrameworkDependentTarGz);

console.log(`Artifacts ready in ${artifactsDir}`);
