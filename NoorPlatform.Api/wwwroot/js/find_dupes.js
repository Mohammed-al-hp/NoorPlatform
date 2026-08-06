const fs = require('fs');
const path = require('path');

const dir = 'c:\\AI_noor\\NoorPlatform_Fixed (3)\\NoorPlatform_Fixed\\NoorPlatform\\NoorPlatform.Api\\wwwroot\\js';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.js'));
const funcMap = {};

// Matches 'function name(' and 'const name = function(' and 'const name = async () =>'
const regex = /(?:^|\s)(?:async\s+)?function\s+([a-zA-Z0-9_]+)\s*\(|(?:^|\s)(?:const|let|var)\s+([a-zA-Z0-9_]+)\s*=\s*(?:async\s*)?(?:function\s*\(|\([^)]*\)\s*=>|[a-zA-Z0-9_]+\s*=>)/gm;

for (const file of files) {
    const content = fs.readFileSync(path.join(dir, file), 'utf-8');
    let match;
    while ((match = regex.exec(content)) !== null) {
        const name = match[1] || match[2];
        if (name && !['if', 'for', 'while', 'switch', 'catch', 'map', 'filter', 'reduce'].includes(name)) {
            if (!funcMap[name]) funcMap[name] = new Set();
            funcMap[name].add(file);
        }
    }
}

for (const [name, fileSet] of Object.entries(funcMap)) {
    if (fileSet.size > 1) {
        console.log(name + ' -> ' + Array.from(fileSet).join(', '));
    }
}
