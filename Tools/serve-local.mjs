// Servidor estático para testar builds WebGL localmente, com os cabeçalhos que a
// Unity exige para arquivos comprimidos com Brotli (.br).
// Uso: node Tools/serve-local.mjs Build/WebGL-proof 8080
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { join, extname, normalize, resolve } from 'node:path';

const root = resolve(process.argv[2] || 'Build/WebGL');
const port = Number(process.argv[3] || 8080);

const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm',
  '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon',
};

createServer(async (req, res) => {
  try {
    let path = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    if (path.endsWith('/')) path += 'index.html';
    const file = normalize(join(root, path));
    if (!file.startsWith(root)) { res.writeHead(403).end(); return; }
    await stat(file);
    const headers = { 'Cache-Control': 'no-cache' };
    let ext = extname(file);
    if (ext === '.br' || ext === '.gz') {
      headers['Content-Encoding'] = ext === '.br' ? 'br' : 'gzip';
      ext = extname(file.slice(0, -ext.length));
    }
    headers['Content-Type'] = types[ext] || 'application/octet-stream';
    res.writeHead(200, headers).end(await readFile(file));
  } catch {
    res.writeHead(404).end('não encontrado');
  }
}).listen(port, () => console.log(`Servindo ${root} em http://localhost:${port}/`));
