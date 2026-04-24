import { marked } from 'marked';
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';

const dir = 'c:/tmp/sd-248562';
const md = fs.readFileSync(path.join(dir, 'SD-248562.md'), 'utf8');

const renderer = new marked.Renderer();
renderer.image = ({ href, title, text }) => {
  try {
    const p = path.join(dir, href.replace(/^\.\//, ''));
    const buf = fs.readFileSync(p);
    const ext = path.extname(p).slice(1).toLowerCase();
    const mime = ext === 'svg' ? 'image/svg+xml' : `image/${ext}`;
    const b64 = buf.toString('base64');
    return `<figure><img src="data:${mime};base64,${b64}" alt="${text||''}"/><figcaption>${text||''}</figcaption></figure>`;
  } catch (e) {
    return `<p><em>[imagem não encontrada: ${href}]</em></p>`;
  }
};

marked.setOptions({ renderer, gfm: true, breaks: false });
const bodyHtml = marked.parse(md);

const attachHtml = '';

const html = `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"/>
<title>SD-248562</title>
<style>
  @page { size: A4; margin: 18mm 16mm; }
  body { font-family: -apple-system, "Segoe UI", Arial, sans-serif; font-size: 10.5pt; line-height: 1.45; color: #1a1a1a; }
  h1 { font-size: 20pt; border-bottom: 2px solid #333; padding-bottom: 4px; margin-top: 22px; page-break-after: avoid; }
  h2 { font-size: 15pt; margin-top: 18px; border-bottom: 1px solid #ccc; padding-bottom: 3px; page-break-after: avoid; }
  h3 { font-size: 12.5pt; margin-top: 14px; page-break-after: avoid; }
  h4 { font-size: 11pt; margin-top: 12px; page-break-after: avoid; }
  table { border-collapse: collapse; width: 100%; margin: 10px 0; font-size: 9.5pt; page-break-inside: avoid; }
  th, td { border: 1px solid #bbb; padding: 5px 7px; text-align: left; vertical-align: top; }
  th { background: #f0f0f0; }
  code { font-family: "Consolas", "Cascadia Code", monospace; font-size: 9pt; background: #f4f4f4; padding: 1px 4px; border-radius: 3px; }
  pre { background: #f6f8fa; border: 1px solid #ddd; padding: 10px; border-radius: 4px; font-size: 8.5pt; line-height: 1.35; white-space: pre-wrap; word-wrap: break-word; }
  pre code { background: transparent; padding: 0; font-size: 8.5pt; }
  figure { text-align: center; margin: 14px 0; page-break-inside: avoid; }
  figure img { max-width: 100%; height: auto; border: 1px solid #ddd; }
  figcaption { font-size: 9pt; color: #666; font-style: italic; margin-top: 4px; }
  hr { border: 0; border-top: 1px solid #ccc; margin: 18px 0; }
  a { color: #0366d6; text-decoration: none; }
  ul, ol { margin: 6px 0 6px 22px; }
</style></head><body>
${bodyHtml}
${attachHtml}
</body></html>`;

const htmlPath = path.join(dir, 'SD-248562.html');
fs.writeFileSync(htmlPath, html);

const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const pdfPath = path.join(dir, 'SD-248562.pdf');
const fileUrl = 'file:///' + htmlPath.replace(/\\/g,'/');

execFileSync(chrome, [
  '--headless=new',
  '--disable-gpu',
  '--no-pdf-header-footer',
  `--print-to-pdf=${pdfPath}`,
  fileUrl
], { stdio: 'inherit' });

console.log('PDF:', pdfPath);
