import fs from 'fs';
import path from 'path';

const ICONIFY_JSON_URL = (prefix) => `https://raw.githubusercontent.com/iconify/icon-sets/master/json/${prefix}.json`;

function parseSvgBodyRecursive(body) {
    const stack = [{ children: [] }];
    // Simple regex to match opening tags, self-closing tags, and closing tags
    const tagRegex = /<([a-z0-9-]+)\s*([^>]*?)(\/?)>|<\/([a-z0-9-]+)>/gi;
    const attrRegex = /([a-z0-9-]+)="([^"]+)"/gi;

    let match;
    while ((match = tagRegex.exec(body)) !== null) {
        if (match[4]) { // Closing tag e.g. </g>
            if (stack.length > 1) stack.pop();
            continue;
        }

        const tagName = match[1];
        const attrsContent = match[2];
        const isSelfClosing = match[3] === '/' || ['path', 'circle', 'rect', 'line', 'polyline', 'polygon'].includes(tagName.toLowerCase());
        const attrs = {};

        let attrMatch;
        while ((attrMatch = attrRegex.exec(attrsContent)) !== null) {
            attrs[attrMatch[1]] = attrMatch[2];
        }

        const node = { tagName, attrs, children: [] };
        stack[stack.length - 1].children.push(node);

        if (!isSelfClosing) {
            stack.push(node);
        }
    }
    return stack[0].children;
}

function generateNodeCode(node, indent) {
    const attrEntries = Object.entries(node.attrs).map(([k, v]) => {
        let val = `"${v}"`;
        if (k === 'stroke-width') val = 'strokeWidth.ToString()';
        return `["${k}"] = ${val}`;
    });

    const attrDict = attrEntries.join(', ');
    const spaces = ' '.repeat(indent);
    
    let code = `${spaces}new DynamicElement { TagName = "${node.tagName}", CustomAttributes = new Dictionary<string, string> { ${attrDict} }`;
    
    if (node.children.length > 0) {
        code += `, Children = { \n`;
        code += node.children.map(child => generateNodeCode(child, indent + 8)).join(',\n');
        code += ` \n${spaces}} }`;
    } else {
        code += " }";
    }
    
    return code;
}

async function generate(prefix, projectName, className, outputDir) {
    console.log(`Fetching icon data for ${prefix}...`);
    const response = await fetch(ICONIFY_JSON_URL(prefix));
    if (!response.ok) {
        throw new Error(`Failed to fetch ${prefix} icons: ${response.statusText}`);
    }
    const iconData = await response.json();

    const srcDir = path.join(outputDir, projectName);
    if (!fs.existsSync(srcDir)) {
        fs.mkdirSync(srcDir, { recursive: true });
    }

    // Generate Icons class
    let code = `using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace eQuantic.UI.${className};

public static class ${className}Icons
{
`;

    for (const [name, icon] of Object.entries(iconData.icons)) {
        const pascalName = name
            .split(/[-_]/)
            .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
            .join('')
            .replace(/^(\d)/, '_$1'); // Handle names starting with numbers

        const nodes = parseSvgBodyRecursive(icon.body);
        const width = icon.width || iconData.width || 24;
        const height = icon.height || iconData.height || 24;

        code += `    public static ${className}Icon ${pascalName}(int size = ${width}, double strokeWidth = 2, string color = "currentColor", string? className = null) => new ${className}Icon
    {
        Name = "${name}",
        Size = size,
        StrokeWidth = strokeWidth,
        Color = color,
        ClassName = className,
        ViewBox = "0 0 ${width} ${height}",
        Content = new List<IComponent>
        {
`;

        code += nodes.map(node => generateNodeCode(node, 12)).join(',\n');

        code += `
        }
    };\n\n`;
    }

    code += '}\n';

    const outputFile = path.join(srcDir, `${className}Icons.cs`);
    console.log(`Writing to ${outputFile}...`);
    fs.writeFileSync(outputFile, code);
    
    // Generate Base Icon Component if not exists
    const iconComponentFile = path.join(srcDir, `${className}Icon.cs`);
    if (!fs.existsSync(iconComponentFile)) {
        const iconComponentCode = `using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace eQuantic.UI.${className};

public class ${className}Icon : StatelessComponent
{
    public string? Name { get; set; }
    public int Size { get; set; } = 24;
    public double StrokeWidth { get; set; } = 2;
    public string Color { get; set; } = "currentColor";
    public string ViewBox { get; set; } = "0 0 24 24";
    public List<IComponent> Content { get; set; } = new();

    public override IComponent Build(RenderContext context)
    {
        var svg = new DynamicElement
        {
            TagName = "svg",
            CustomAttributes = new Dictionary<string, string>
            {
                ["xmlns"] = "http://www.w3.org/2000/svg",
                ["width"] = Size.ToString(),
                ["height"] = Size.ToString(),
                ["viewBox"] = ViewBox,
                ["fill"] = "none",
                ["stroke"] = "currentColor",
                ["stroke-width"] = StrokeWidth.ToString(),
                ["stroke-linecap"] = "round",
                ["stroke-linejoin"] = "round",
                ["style"] = $"color: {Color}",
                ["class"] = $"icon icon-${prefix} icon-${prefix}-{Name} {ClassName}".Trim()
            }
        };

        foreach (var item in Content)
        {
            svg.Children.Add(item);
        }

        return svg;
    }
}
`;
        fs.writeFileSync(iconComponentFile, iconComponentCode);
    }

    // Generate ServiceExtensions if not exists
    const extensionsFile = path.join(srcDir, `${className}ServiceExtensions.cs`);
    if (!fs.existsSync(extensionsFile)) {
        const extensionsCode = `using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.${className};

public static class ${className}ServiceExtensions
{
    public static IServiceCollection Add${className}Icons(this IServiceCollection services)
    {
        return services;
    }
}
`;
        fs.writeFileSync(extensionsFile, extensionsCode);
    }

    // Generate .csproj if not exists
    const csprojFile = path.join(srcDir, `eQuantic.UI.${className}.csproj`);
    if (!fs.existsSync(csprojFile)) {
        const csprojCode = `<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
        <ProjectReference Include="..\\eQuantic.UI.Core\\eQuantic.UI.Core.csproj" />
        <ProjectReference Include="..\\eQuantic.UI.Components\\eQuantic.UI.Components.csproj" />
    </ItemGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <Content Include="**\\*.cs" Exclude="obj\\**;bin\\**" PackagePath="tools\\source\\" />
    </ItemGroup>

</Project>
`;
        fs.writeFileSync(csprojFile, csprojCode);
    }

    console.log(`Done with ${prefix}!`);
}

async function main() {
    const sets = [
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
        { prefix: 'iconoir', projectName: 'eQuantic.UI.Iconoir', className: 'Iconoir' }
    ];

    const outputDir = './src';
    for (const set of sets) {
        await generate(set.prefix, set.projectName, set.className, outputDir).catch(console.error);
    }
}

main();
