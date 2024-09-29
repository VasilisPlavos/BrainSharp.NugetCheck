#! /usr/bin/env node
// the above line (Shebang) is important to stay there...
const { spawn } = require('child_process');

var dotnetArgs = [`${__dirname}\\BrainSharp.NugetCheck.ConsoleApp.dll`];
dotnetArgs = dotnetArgs.concat(process.argv.slice(2));

spawn('dotnet', dotnetArgs, { stdio: 'inherit' });
