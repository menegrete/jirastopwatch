#!/usr/bin/env node
"use strict";

const fs = require("fs");
const path = require("path");

const version = process.argv[2];
if (!version) {
  console.error("Usage: node set-assembly-version.js <version>");
  process.exit(1);
}

const assemblyInfoPath = path.join(
  __dirname,
  "..",
  "source",
  "StopWatch",
  "Properties",
  "AssemblyInfo.cs"
);

// AssemblyVersion and AssemblyFileVersion only accept numeric versions, so a
// prerelease such as "3.8.0-rc.1" gets its numeric part ("3.8.0") there and
// the full string only in AssemblyInformationalVersion.
const numericVersion = version.split("-")[0];
const versionFor = {
  AssemblyVersion: numericVersion,
  AssemblyFileVersion: numericVersion,
  AssemblyInformationalVersion: version,
};

let contents = fs.readFileSync(assemblyInfoPath, "utf8");

for (const attribute of Object.keys(versionFor)) {
  // Anchored to the start of the line (with the `m` flag) so this only
  // matches the live attribute, not the commented-out example above it.
  const pattern = new RegExp(`^\\[assembly: ${attribute}\\("[^"]*"\\)\\]$`, "m");
  if (!pattern.test(contents)) {
    throw new Error(`${attribute} attribute not found in ${assemblyInfoPath}`);
  }
  contents = contents.replace(pattern, `[assembly: ${attribute}("${versionFor[attribute]}")]`);
}

fs.writeFileSync(assemblyInfoPath, contents);
console.log(`Set ${assemblyInfoPath} to version ${version}`);
