import fs from 'node:fs/promises';
import path from 'node:path';

import pngToIco from 'png-to-ico';
import sharp from 'sharp';

const root = process.cwd();
const iconName = 'layout-template';
const svgPath = path.join(root, 'node_modules', 'lucide-static', 'icons', `${iconName}.svg`);
const assetsDir = path.join(root, 'assets');
const pngPath = path.join(assetsDir, 'app-icon.png');
const icoPath = path.join(assetsDir, 'app-icon.ico');

const lucideSvg = await fs.readFile(svgPath, 'utf8');

const iconSvg = `
<svg width="256" height="256" viewBox="0 0 256 256" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="#0f1722"/>
      <stop offset="100%" stop-color="#18354d"/>
    </linearGradient>
  </defs>
  <rect x="12" y="12" width="232" height="232" rx="52" fill="url(#bg)"/>
  <rect x="12" y="12" width="232" height="232" rx="52" fill="none" stroke="#72e2ae" stroke-width="8"/>
  <g transform="translate(52 52) scale(6.333333)">
    ${lucideSvg
      .replace('<svg', '<g')
      .replace('</svg>', '</g>')
      .replace(/stroke="currentColor"/g, 'stroke="#f4fbff"')
      .replace(/width="24"/g, '')
      .replace(/height="24"/g, '')}
  </g>
</svg>`;

await fs.mkdir(assetsDir, { recursive: true });

await sharp(Buffer.from(iconSvg))
  .png()
  .toFile(pngPath);

const icoBuffer = await pngToIco([
  await sharp(Buffer.from(iconSvg)).resize(16, 16).png().toBuffer(),
  await sharp(Buffer.from(iconSvg)).resize(32, 32).png().toBuffer(),
  await sharp(Buffer.from(iconSvg)).resize(48, 48).png().toBuffer(),
  await sharp(Buffer.from(iconSvg)).resize(64, 64).png().toBuffer(),
  await sharp(Buffer.from(iconSvg)).resize(128, 128).png().toBuffer(),
  await sharp(Buffer.from(iconSvg)).resize(256, 256).png().toBuffer()
]);

await fs.writeFile(icoPath, icoBuffer);

console.log(`Generated ${icoPath}`);
