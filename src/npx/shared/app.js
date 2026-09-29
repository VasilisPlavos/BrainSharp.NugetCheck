#! /usr/bin/env node
// The shebang above must stay on the first line: npm runs this file as the command.
const path = require('path');
const { spawn } = require('child_process');

const dll = path.join(__dirname, 'BrainSharp.NugetCheck.ConsoleApp.dll');
const child = spawn('dotnet', [dll, ...process.argv.slice(2)], { stdio: 'inherit' });

child.on('error', (error) => {
  console.error(`Could not start 'dotnet' (${error.message}). Install the .NET 10 runtime: https://dotnet.microsoft.com/download`);
  process.exit(127);
});

// pass the scan result (0 = no warnings, 1 = warnings, 2 = invalid usage) to the caller, e.g. a CI pipeline
child.on('exit', (code) => process.exit(code ?? 1));
