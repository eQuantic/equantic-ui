import remapping from "@ampproject/remapping";
import { readFileSync, writeFileSync, existsSync } from "fs";
import { join, dirname } from "path";

const [,, mapPath] = process.argv;

if (!mapPath) {
    console.error("Usage: bun merge-maps.js <map-file>");
    process.exit(1);
}

const mapContent = readFileSync(mapPath, "utf8");
const map = JSON.parse(mapContent);
const mapDir = dirname(mapPath);

const remapped = remapping(map, (file, ctx) => {
    // 'file' is the path from 'sources' in the map
    // We need to resolve it relative to the map file
    const absolutePath = join(mapDir, file);
    const tsMapPath = absolutePath + ".map";

    if (existsSync(tsMapPath)) {
        // console.log(`Found intermediate map: ${tsMapPath}`);
        return JSON.parse(readFileSync(tsMapPath, "utf8"));
    }
    
    // console.log(`No intermediate map for: ${file}`);
    return null;
});

writeFileSync(mapPath, remapped.toString());
console.log(`Merged source map written to: ${mapPath}`);
