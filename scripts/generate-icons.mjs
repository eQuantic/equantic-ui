/**
 * Icon pack generator — WRITE-ONCE architecture.
 *
 * Source: the Iconify icon-sets JSON (one file per set). Each icon's SVG body is flattened to a
 * single target-neutral `IconGlyph` (name + path data + fill/stroke style + viewBox): groups pass
 * their presentation attributes down, basic shapes (circle/rect/line/ellipse/polyline/polygon)
 * convert to path data, and invisible bounding paths (fill:none stroke:none) drop. The emitted
 * package references ONLY eQuantic.UI.Primitives — the same catalog serves the web realizer
 * (inline SVG) and the native glyph atlas (W4) once it lands.
 *
 * Usage: node scripts/generate-icons.mjs [prefix …]   (no args = all sets)
 */
import fs from 'fs';
import path from 'path';

const ICONIFY_JSON_URL = (prefix) => `https://raw.githubusercontent.com/iconify/icon-sets/master/json/${prefix}.json`;

// ---- SVG body parsing (regular machine output — a tag scanner is sufficient) ---------------------

function parseSvgBodyRecursive(body) {
    const stack = [{ children: [] }];
    const tagRegex = /<([a-z0-9-]+)\s*([^>]*?)(\/?)>|<\/([a-z0-9-]+)>/gi;

    let match;
    while ((match = tagRegex.exec(body)) !== null) {
        if (match[4]) {
            if (stack.length > 1) stack.pop();
            continue;
        }
        const tagName = match[1].toLowerCase();
        const attrs = {};
        const attrRegex = /([a-z0-9-]+)="([^"]*)"/gi;
        let attrMatch;
        while ((attrMatch = attrRegex.exec(match[2])) !== null) {
            attrs[attrMatch[1]] = attrMatch[2];
        }
        const node = { tagName, attrs, children: [] };
        stack[stack.length - 1].children.push(node);
        if (match[3] !== '/') stack.push(node);
    }
    return stack[0].children;
}

// ---- shape → path data ----------------------------------------------------------------------------

const num = (v, fallback = 0) => {
    const n = parseFloat(v);
    return Number.isFinite(n) ? n : fallback;
};
const fmt = (n) => {
    const r = Math.round(n * 1000) / 1000;
    return Object.is(r, -0) ? '0' : String(r);
};

function shapeToPath(node) {
    const a = node.attrs;
    switch (node.tagName) {
        case 'path':
            return a.d ?? null;
        case 'circle': {
            const cx = num(a.cx), cy = num(a.cy), r = num(a.r);
            if (r <= 0) return null;
            return `M${fmt(cx - r)} ${fmt(cy)}a${fmt(r)} ${fmt(r)} 0 1 0 ${fmt(2 * r)} 0a${fmt(r)} ${fmt(r)} 0 1 0 ${fmt(-2 * r)} 0`;
        }
        case 'ellipse': {
            const cx = num(a.cx), cy = num(a.cy), rx = num(a.rx), ry = num(a.ry);
            if (rx <= 0 || ry <= 0) return null;
            return `M${fmt(cx - rx)} ${fmt(cy)}a${fmt(rx)} ${fmt(ry)} 0 1 0 ${fmt(2 * rx)} 0a${fmt(rx)} ${fmt(ry)} 0 1 0 ${fmt(-2 * rx)} 0`;
        }
        case 'rect': {
            const x = num(a.x), y = num(a.y), w = num(a.width), h = num(a.height);
            if (w <= 0 || h <= 0) return null;
            let rx = a.rx !== undefined ? num(a.rx) : (a.ry !== undefined ? num(a.ry) : 0);
            let ry = a.ry !== undefined ? num(a.ry) : rx;
            rx = Math.min(rx, w / 2);
            ry = Math.min(ry, h / 2);
            if (rx <= 0 || ry <= 0) {
                return `M${fmt(x)} ${fmt(y)}h${fmt(w)}v${fmt(h)}h${fmt(-w)}z`;
            }
            return (
                `M${fmt(x + rx)} ${fmt(y)}h${fmt(w - 2 * rx)}a${fmt(rx)} ${fmt(ry)} 0 0 1 ${fmt(rx)} ${fmt(ry)}` +
                `v${fmt(h - 2 * ry)}a${fmt(rx)} ${fmt(ry)} 0 0 1 ${fmt(-rx)} ${fmt(ry)}` +
                `h${fmt(-(w - 2 * rx))}a${fmt(rx)} ${fmt(ry)} 0 0 1 ${fmt(-rx)} ${fmt(-ry)}` +
                `v${fmt(-(h - 2 * ry))}a${fmt(rx)} ${fmt(ry)} 0 0 1 ${fmt(rx)} ${fmt(-ry)}z`
            );
        }
        case 'line':
            return `M${fmt(num(a.x1))} ${fmt(num(a.y1))}L${fmt(num(a.x2))} ${fmt(num(a.y2))}`;
        case 'polyline':
        case 'polygon': {
            const points = (a.points ?? '').trim().split(/[\s,]+/).map(parseFloat).filter(Number.isFinite);
            if (points.length < 4) return null;
            let d = `M${fmt(points[0])} ${fmt(points[1])}`;
            for (let i = 2; i + 1 < points.length; i += 2) d += `L${fmt(points[i])} ${fmt(points[i + 1])}`;
            if (node.tagName === 'polygon') d += 'z';
            return d;
        }
        default:
            return null; // <defs>, <mask>, <title>… — handled by the caller as unsupported
    }
}

const DRAWABLE = new Set(['path', 'circle', 'ellipse', 'rect', 'line', 'polyline', 'polygon']);

/**
 * Flattens an icon's node tree to { path, style, strokeWidth } — group presentation attributes
 * inherit down; invisible bounding shapes drop. Returns null (with a reason) when the icon uses
 * features outside the single-glyph contract (unsupported elements, transforms, mixed fill+stroke).
 */
function flattenIcon(nodes) {
    const drawn = [];
    let unsupported = null;

    const walk = (node, inherited) => {
        if (unsupported) return;
        const merged = { ...inherited, ...node.attrs };
        if (node.attrs.transform) {
            unsupported = `transform on <${node.tagName}>`;
            return;
        }
        if (node.tagName === 'g' || node.tagName === 'svg') {
            for (const child of node.children) walk(child, merged);
            return;
        }
        if (!DRAWABLE.has(node.tagName)) {
            unsupported = `<${node.tagName}>`;
            return;
        }
        const d = shapeToPath(node);
        if (d === null) return;
        const fill = merged.fill;
        const stroke = merged.stroke;
        const visibleFill = fill !== 'none';
        const visibleStroke = stroke !== undefined && stroke !== 'none';
        if (!visibleFill && !visibleStroke) return; // bounding ghost
        drawn.push({
            d,
            stroke: visibleStroke,
            strokeWidth: num(merged['stroke-width'], 2),
        });
    };

    for (const node of nodes) walk(node, {});
    if (unsupported) return { error: unsupported };
    if (drawn.length === 0) return { error: 'no drawable content' };

    const strokes = drawn.filter((p) => p.stroke);
    if (strokes.length > 0 && strokes.length < drawn.length) return { error: 'mixed fill+stroke' };

    return {
        path: drawn.map((p) => p.d).join(' '),
        style: strokes.length > 0 ? 'Stroke' : 'Fill',
        strokeWidth: strokes.length > 0 ? strokes[0].strokeWidth : 2,
    };
}

// ---- emission -------------------------------------------------------------------------------------

const pascal = (name) =>
    name
        .split(/[-_]/)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join('')
        .replace(/^(\d)/, '_$1');

const esc = (s) => s.replace(/\\/g, '\\\\').replace(/"/g, '\\"');

async function generate(prefix, projectName, className, outputDir) {
    console.log(`Fetching ${prefix}…`);
    const response = await fetch(ICONIFY_JSON_URL(prefix));
    if (!response.ok) throw new Error(`Failed to fetch ${prefix}: ${response.statusText}`);
    const iconData = await response.json();

    const baseName = className.replace(/icons$/i, '');
    const iconsClassName = `${baseName}Icons`;
    const setLabel = iconData.info?.name ?? baseName;

    const lines = [];
    const skipped = new Map();
    for (const [name, icon] of Object.entries(iconData.icons)) {
        const width = icon.width || iconData.width || 24;
        const height = icon.height || iconData.height || 24;
        const flat = flattenIcon(parseSvgBodyRecursive(icon.body));
        if (flat.error) {
            skipped.set(flat.error, (skipped.get(flat.error) ?? 0) + 1);
            continue;
        }
        const viewBox = `0 0 ${width} ${height}`;
        const args = [`"${esc(name)}"`, `"${esc(flat.path)}"`];
        const needStyle = flat.style !== 'Fill';
        const needViewBox = viewBox !== '0 0 24 24';
        const needWidth = flat.style === 'Stroke' && flat.strokeWidth !== 2;
        if (needStyle || needViewBox || needWidth) args.push(`IconGlyphStyle.${flat.style}`);
        if (needViewBox || needWidth) args.push(`"${viewBox}"`);
        if (needWidth) args.push(`${flat.strokeWidth}f`);
        lines.push(`    public static readonly IconGlyph ${pascal(name)} = new(${args.join(', ')});`);
    }

    const srcDir = path.join(outputDir, projectName);
    fs.mkdirSync(srcDir, { recursive: true });

    const code = `using eQuantic.UI.Primitives;

namespace eQuantic.UI.${className};

/// <summary>
/// ${setLabel} icon catalog on the WRITE-ONCE architecture: every glyph is target-neutral
/// <see cref="IconGlyph"/> data — the web realizer emits inline SVG, the native glyph atlas (W4)
/// rasterizes the same data. Use with the Icon node: <c>new Icon(${iconsClassName}.SomeGlyph,
/// IconSize.Md)</c>. GENERATED by scripts/generate-icons.mjs from the Iconify '${prefix}' set —
/// do not edit by hand.
/// </summary>
public static class ${iconsClassName}
{
${lines.join('\n')}
}
`;
    fs.writeFileSync(path.join(srcDir, `${iconsClassName}.cs`), code);

    // The legacy per-pack component/provider/extensions die with the write-once refactor.
    for (const dead of [`${baseName}Icon.cs`, `${className}IconProvider.cs`, `${className}ServiceExtensions.cs`]) {
        const p = path.join(srcDir, dead);
        if (fs.existsSync(p)) fs.rmSync(p);
    }

    const csproj = `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <PackageId>eQuantic.UI.${className}</PackageId>
    <Description>${setLabel} icon catalog for eQuantic.UI — write-once IconGlyph data (web + native) generated from the Iconify '${prefix}' set.</Description>
    <IsAotCompatible>true</IsAotCompatible>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <!-- Primitives ONLY: glyph data is target-neutral by construction. -->
    <ProjectReference Include="..\\eQuantic.UI.Primitives\\eQuantic.UI.Primitives.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Sources ship for the eqc transpiler (client-side inlining — see the plan's icon-pack notes). -->
    <Content Include="**\\*.cs" Exclude="obj\\**;bin\\**" PackagePath="tools\\source\\" />
    <!-- Marker: identifies this as an inlinable source pack so the SDK feeds it to eqc as ref-sources. -->
    <Content Include="EQuanticIconPack.marker" PackagePath="tools\\source\\" />
  </ItemGroup>

</Project>
`;
    fs.writeFileSync(path.join(srcDir, `eQuantic.UI.${className}.csproj`), csproj);

    // The self-declaring marker: any pack the developer installs (present or future) is discovered
    // by the SDK via this file in tools/source — no per-pack SDK knowledge, no consumer config.
    fs.writeFileSync(path.join(srcDir, 'EQuanticIconPack.marker'),
        'Marker: this eQuantic.UI package ships inlinable IconGlyph constant sources (tools/source).\n' +
        'The SDK feeds these to eqc as reference-sources so pack glyphs inline at the use site.\n');

    const skippedTotal = [...skipped.values()].reduce((a, b) => a + b, 0);
    console.log(`  ${iconsClassName}: ${lines.length} glyphs; skipped ${skippedTotal}` +
        (skippedTotal ? ` (${[...skipped.entries()].map(([k, v]) => `${k}: ${v}`).join(', ')})` : ''));
}

const SETS = [
    { prefix: 'lucide', projectName: 'eQuantic.UI.Lucide', className: 'Lucide' },
    { prefix: 'heroicons', projectName: 'eQuantic.UI.Heroicons', className: 'Heroicons' },
    { prefix: 'radix-icons', projectName: 'eQuantic.UI.RadixIcons', className: 'RadixIcons' },
    { prefix: 'tabler', projectName: 'eQuantic.UI.TablerIcons', className: 'TablerIcons' },
    { prefix: 'fa6-solid', projectName: 'eQuantic.UI.FontAwesome6.Solid', className: 'FontAwesome6Solid' },
    { prefix: 'fa6-regular', projectName: 'eQuantic.UI.FontAwesome6.Regular', className: 'FontAwesome6Regular' },
    { prefix: 'fa6-brands', projectName: 'eQuantic.UI.FontAwesome6.Brands', className: 'FontAwesome6Brands' },
    { prefix: 'ph', projectName: 'eQuantic.UI.Phosphor', className: 'Phosphor' },
    { prefix: 'simple-icons', projectName: 'eQuantic.UI.SimpleIcons', className: 'SimpleIcons' },
    { prefix: 'bi', projectName: 'eQuantic.UI.BootstrapIcons', className: 'BootstrapIcons' },
    { prefix: 'iconoir', projectName: 'eQuantic.UI.Iconoir', className: 'Iconoir' },
];

async function main() {
    const only = process.argv.slice(2);
    const sets = only.length ? SETS.filter((s) => only.includes(s.prefix)) : SETS;
    for (const set of sets) {
        await generate(set.prefix, set.projectName, set.className, './src');
    }
}

main().catch((e) => {
    console.error(e);
    process.exit(1);
});
