// Rendert scènes uit render.html met headless Chromium (Playwright).
// Gebruik: node ArtSource/Render/render.cjs <scenes.json> <outdir>
// Vereist: three.js in ArtSource/Render/three (zie README) en een lokale http-server op ArtSource/.
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
const [scenesPath, outDir, base = 'http://127.0.0.1:8766/Render/render.html'] = process.argv.slice(2);
const scenes = JSON.parse(fs.readFileSync(scenesPath, 'utf8'));
fs.mkdirSync(outDir, { recursive: true });
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
for (const s of scenes) {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(s.params)) params.set(k, typeof v === 'string' ? v : JSON.stringify(v));
  const w = s.params.w || 900, h = s.params.h || 1200;
  const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: s.params.ss || 2 });
  page.on('console', m => console.log(`[${s.name}]`, m.text()));
  page.on('pageerror', e => console.log(`[${s.name}] pageerror`, e.message));
  if (s.extra) await page.addInitScript({ content: `window.extraSceneSrc = ${JSON.stringify(s.extra)};` });
  await page.goto(`${base}?${params}`);
  await page.waitForFunction('window.done', null, { timeout: 900000 });
  const res = await page.evaluate('window.done');
  await page.locator('canvas').screenshot({ path: `${outDir}/${s.name}.png`, omitBackground: true });
  console.log(s.name, res);
  await page.close();
}
await browser.close();
})();
