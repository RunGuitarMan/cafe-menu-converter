// Development-only renderer: every size comes directly from the SVG, never another PNG.
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');

function vectorResource(svg) {
  // This small converter covers the primitives in our icon, preserving the same
  // path data and colours in Avalonia's native vector renderer.
  const required = (pattern) => {
    const match = svg.match(pattern);
    if (!match) throw new Error(`Unsupported icon SVG: ${pattern}`);
    return match;
  };
  const xml = text => text.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
  const base = required(/<linearGradient id="base"[\s\S]*?<\/linearGradient>/)[0];
  const colours = [...base.matchAll(/stop-color="([^"]+)"/g)].map(match => match[1]);
  const cell = Number(required(/<pattern id="cells" width="([^"]+)"/)[1]) / 2;
  const geometry = required(/<g transform="translate\(242 254\) scale\(3.52\)">([\s\S]*?)<\/g>/)[1];
  const shapes = [...geometry.matchAll(/<path d="([^"]+)" fill="([^"]+)"/g)];
  if (shapes.length !== 2) throw new Error('Expected the shield and T vector paths.');
  // SVG patternTransform = translate(22, 5) rotate(-12, 512, 512).
  const angle = -12 * Math.PI / 180;
  const point = (x, y) => [
    534 + (x - 512) * Math.cos(angle) - (y - 512) * Math.sin(angle),
    517 + (x - 512) * Math.sin(angle) + (y - 512) * Math.cos(angle)
  ].map(value => value.toFixed(4)).join(',');
  const cells = [];
  for (let row = -3; row <= 9; row++) {
    for (let col = -3; col <= 9; col++) {
      if ((row + col) % 2 !== 0) continue;
      const x = col * cell, y = row * cell;
      cells.push(`M${point(x,y)} L${point(x+cell,y)} ${point(x+cell,y+cell)} ${point(x,y+cell)} Z`);
    }
  }
  return `<!-- Generated from app-icon.svg by build/render-icons.cjs. -->
<ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <DrawingImage x:Key="MenuAppIcon">
    <DrawingGroup>
      <GeometryDrawing Brush="Transparent" Geometry="M0,0 H1024 V1024 H0 Z"/>
      <DrawingGroup>
        <DrawingGroup.ClipGeometry><RectangleGeometry Rect="48,48,928,928" RadiusX="204" RadiusY="204"/></DrawingGroup.ClipGeometry>
        <GeometryDrawing Geometry="M48,48 H976 V976 H48 Z">
          <GeometryDrawing.Brush><LinearGradientBrush StartPoint="0%,0%" EndPoint="85%,100%"><GradientStop Color="${colours[0]}"/><GradientStop Color="${colours[1]}" Offset="1"/></LinearGradientBrush></GeometryDrawing.Brush>
        </GeometryDrawing>
        <GeometryDrawing Brush="#59F6F6E5">
          <GeometryDrawing.Geometry><CombinedGeometry GeometryCombineMode="Intersect" Geometry1="${cells.join(' ')}" Geometry2="M48,48 H976 V976 H48 Z"/></GeometryDrawing.Geometry>
        </GeometryDrawing>
        <GeometryDrawing Geometry="M48,48 H976 V976 H48 Z">
          <GeometryDrawing.Brush><RadialGradientBrush Center="10%,8%" GradientOrigin="10%,8%" RadiusX="110%" RadiusY="110%"><GradientStop Color="#42FFFFFF"/><GradientStop Color="#00FFFFFF" Offset="1"/></RadialGradientBrush></GeometryDrawing.Brush>
        </GeometryDrawing>
        <DrawingGroup>
          <DrawingGroup.Transform><MatrixTransform Matrix="3.52,0,0,3.52,242,254"/></DrawingGroup.Transform>
${shapes.map(shape => `          <GeometryDrawing Brush="${shape[2]}" Geometry="${xml(shape[1])}"/>`).join('\n')}
        </DrawingGroup>
      </DrawingGroup>
    </DrawingGroup>
  </DrawingImage>
</ResourceDictionary>
`;
}

async function main() {
  const root = path.resolve(__dirname, '..');
  const assets = path.join(root, 'src/CafeMenuConverter.App/Assets');
  const work = path.join(root, 'artifacts/icons');
  const iconset = path.join(work, 'AppIcon.iconset');
  await fs.mkdir(iconset, { recursive: true });
  const source = await fs.readFile(path.join(assets, 'app-icon.svg'));
  await fs.writeFile(path.join(assets, 'AppIcon.axaml'), vectorResource(source.toString('utf8')));
  async function render(size, output) {
    // SVG density sets the rasterizer's target resolution. PNG compression is lossless.
    await sharp(source, { density: 72 * size / 1024 })
      .png({ compressionLevel: 9, palette: false })
      .toFile(output);
  }
  await render(4096, path.join(assets, 'app-icon.png'));
  for (const size of [16, 32, 128, 256, 512]) {
    await render(size, path.join(iconset, `icon_${size}x${size}.png`));
    await render(size * 2, path.join(iconset, `icon_${size}x${size}@2x.png`));
  }
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const images = [];
  for (const size of sizes) {
    const file = path.join(work, `windows-${size}.png`);
    await render(size, file);
    images.push(await fs.readFile(file));
  }
  const directory = Buffer.alloc(6 + 16 * sizes.length);
  directory.writeUInt16LE(1, 2);
  directory.writeUInt16LE(sizes.length, 4);
  let offset = directory.length;
  sizes.forEach((size, index) => {
    const entry = 6 + 16 * index;
    directory[entry] = directory[entry + 1] = size % 256;
    directory.writeUInt16LE(1, entry + 4);
    directory.writeUInt16LE(32, entry + 6);
    directory.writeUInt32LE(images[index].length, entry + 8);
    directory.writeUInt32LE(offset, entry + 12);
    offset += images[index].length;
  });
  await fs.writeFile(path.join(assets, 'app-icon.ico'), Buffer.concat([directory, ...images]));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
