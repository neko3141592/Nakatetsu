#!/usr/bin/env node
// Local-only mxGraph rendering, layout, PNG export, and geometric checks.
// npm install --ignore-scripts mxgraph@4.2.2 puppeteer-core
// node render_logic_diagrams.cjs repo node_modules chrome output [--layout]
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const crypto = require('node:crypto');
const {createRequire} = require('node:module');

async function main() {
  const [repoArg, modulesArg, chrome, outputArg, ...flags] = process.argv.slice(2);
  if (!outputArg) throw new Error('Usage: repo node_modules chrome output [--layout]');
  const repo = path.resolve(repoArg), modules = path.resolve(modulesArg), output = path.resolve(outputArg);
  const localRequire = createRequire(path.join(modules, '_renderer.cjs'));
  const puppeteer = localRequire('puppeteer-core');
  const report = JSON.parse(fs.readFileSync(path.join(repo, 'Documentation/Validation/LogicDiagrams/CodeCheck.json')));
  fs.mkdirSync(output, {recursive: true});
  const base = path.join(modules, 'mxgraph/javascript');
  const markup = `<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:white}#graph{position:relative;font-family:Arial,sans-serif}</style>
    <script>var mxBasePath='/mxgraph';var mxLoadResources=false;var mxLoadStylesheets=false;</script><script src='/mxgraph/mxClient.js'></script><div id='graph'></div>`;
  const server = http.createServer((req, res) => {
    if (req.url === '/') {res.setHeader('Content-Type','text/html; charset=utf-8');res.end(markup);return;}
    if (!req.url.startsWith('/mxgraph/')) {res.writeHead(404);res.end();return;}
    const filename = path.resolve(base, decodeURIComponent(req.url.slice('/mxgraph/'.length)));
    if (!filename.startsWith(base + path.sep)) {res.writeHead(403);res.end();return;}
    if (!fs.existsSync(filename)) {res.writeHead(404);res.end();return;}
    res.setHeader('Content-Type',filename.endsWith('.js') ? 'text/javascript' : 'application/octet-stream');
    fs.createReadStream(filename).pipe(res);
  });
  await new Promise((resolve,reject) => {server.once('error',reject);server.listen(0, '127.0.0.1', resolve);});
  let browser;
  const rendered = [];
  try {
    browser = await puppeteer.launch({executablePath: chrome, headless:true, args:['--disable-background-networking']});
    const page = await browser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    for (const entry of report.results) {
      const file = path.join(repo, entry.diagram);
      const xml = fs.readFileSync(file, 'utf8');
      const pages = await page.evaluate(xml => {
        const doc = new DOMParser().parseFromString(xml, 'text/xml');
        return Array.from(doc.querySelectorAll('diagram')).map(d => ({name:d.getAttribute('name'), model:new XMLSerializer().serializeToString(d.querySelector('mxGraphModel'))}));
      }, xml);
      const updated = [];
      for (let i = 0; i < pages.length; i++) {
        const shouldLayout = flags.includes('--layout') && !entry.existingDiagram;
        const result = await page.evaluate(({model, shouldLayout}) => {
          const container = document.getElementById('graph');
          container.replaceChildren();
          if (window.currentGraph) window.currentGraph.destroy();
          const graph = window.currentGraph = new mxGraph(container);
          graph.setEnabled(false);
          graph.setHtmlLabels(true);
          graph.getStylesheet().putCellStyle('text', {shape:'label',fillColor:'none',strokeColor:'none'});
          const doc = mxUtils.parseXml(model);
          new mxCodec(doc).decode(doc.documentElement, graph.getModel());
          const parent = graph.getDefaultParent();
          if (shouldLayout) {
            const layout = new mxHierarchicalLayout(graph, mxConstants.DIRECTION_WEST);
            layout.intraCellSpacing = 40;
            layout.interRankCellSpacing = 120;
            layout.interHierarchySpacing = 60;
            layout.isVertexIgnored = c => !(c.function || c.externalFunction);
            const vertices = graph.getChildVertices(parent).filter(c => c.function || c.externalFunction);
            // Explicit roots keep title/legend/source annotations out of the layout.
            layout.execute(parent, vertices);
            let minX = Math.min(...vertices.map(c => c.geometry.x));
            let minY = Math.min(...vertices.map(c => c.geometry.y));
            // Move the graph below the title/legend; move routed edges with it.
            graph.moveCells([...vertices, ...graph.getChildEdges(parent)], 40-minX, 190-minY);
            // Preserve the existing draw.io orthogonal-arrow convention after layout.
            const edges = graph.getChildEdges(parent);
            graph.setCellStyles('noEdgeStyle','0',edges);
            graph.setCellStyles('exitX','1',edges);
            graph.setCellStyles('exitY','0.5',edges);
            graph.setCellStyles('entryX','0',edges);
            graph.setCellStyles('entryY','0.5',edges);
          }
          graph.refresh();
          const vertices = graph.getChildVertices(parent).filter(c => c.function || c.externalFunction);
          const collisionVertices = graph.getChildVertices(parent);
          const overlaps = [], overflowingLabels = [];
          for (let a = 0; a < collisionVertices.length; a++) {
            const c = collisionVertices[a], g = c.geometry, state = graph.view.getState(c);
            for (let b = a + 1; b < collisionVertices.length; b++) {
              const d = collisionVertices[b], h = d.geometry;
              if (g.x < h.x+h.width-1 && g.x+g.width > h.x+1 && g.y < h.y+h.height-1 && g.y+g.height > h.y+1)
                overlaps.push([c.function || c.externalFunction || c.id, d.function || d.externalFunction || d.id]);
            }
            const bb = state && state.text && state.text.boundingBox;
            if (bb && (bb.x < state.x-2 || bb.y < state.y-2 || bb.x+bb.width > state.x+state.width+2 || bb.y+bb.height > state.y+state.height+2))
              overflowingLabels.push(c.function || c.externalFunction);
          }
          let bounds = graph.getGraphBounds();
          // Existing diagrams can place functions at negative coordinates.
          // Translate the render view only; do not rewrite their document geometry.
          if (bounds.x < 0 || bounds.y < 0) {
            graph.view.setTranslate(bounds.x < 0 ? 40-bounds.x : 0, bounds.y < 0 ? 40-bounds.y : 0);
            bounds = graph.getGraphBounds();
          }
          const width = Math.ceil(Math.max(1000, bounds.x+bounds.width+50));
          const height = Math.ceil(Math.max(340, bounds.y+bounds.height+50));
          const encoded = new mxCodec().encode(graph.getModel());
          encoded.setAttribute('page','1');encoded.setAttribute('pageScale','1');
          encoded.setAttribute('pageWidth',String(width));encoded.setAttribute('pageHeight',String(height));
          encoded.setAttribute('background','#ffffff');
          return {width,height,overlaps,overflowingLabels,model:mxUtils.getXml(encoded)};
        }, {model:pages[i].model, shouldLayout});
        await page.setViewport({width:result.width, height:result.height, deviceScaleFactor:1});
        await page.evaluate(() => document.fonts.ready);
        const png = `${entry.class}-${i+1}.png`;
        await page.screenshot({path:path.join(output,png), fullPage:true});
        updated.push(result.model);
        rendered.push({class:entry.class,diagram:entry.diagram,page:pages[i].name,png,
          width:result.width,height:result.height,overlapPairs:result.overlaps,labelOverflows:result.overflowingLabels,
          existingDiagram:entry.existingDiagram});
        if (!entry.existingDiagram && (result.overlaps.length || result.overflowingLabels.length))
          throw new Error(`Layout needs repair: ${entry.class} ${JSON.stringify(result)}`);
      }
      if (flags.includes('--layout') && !entry.existingDiagram) {
        const newXml = await page.evaluate(({xml,models}) => {
          const doc = new DOMParser().parseFromString(xml, 'text/xml');
          Array.from(doc.querySelectorAll('diagram')).forEach((d,i) => {
            const m = new DOMParser().parseFromString(models[i], 'text/xml').documentElement;
            d.replaceChildren(doc.importNode(m,true));
          });
          return new XMLSerializer().serializeToString(doc).replace(/></g,'>\n<')+'\n';
        }, {xml,models:updated});
        fs.writeFileSync(file,newXml);
      }
    }
    for (const item of rendered) item.diagramSha256 = crypto.createHash('sha256').update(fs.readFileSync(path.join(repo,item.diagram))).digest('hex');
    fs.writeFileSync(path.join(output,'RenderCheck.json'), JSON.stringify({renderer:'mxGraph 4.2.2 / Google Chrome',sourceCommit:report.sourceCommit,diagramCount:report.diagramCount,pageCount:rendered.length,results:rendered},null,2)+'\n');
    // Compact overview sheets; full-size pages remain available for detailed inspection.
    const sheets=[];
    for (let i=0;i<rendered.length;i+=6) {
      const batch=rendered.slice(i,i+6);
      const cards=batch.map(x=>`<section><h2>${x.class} · ${x.page}</h2><img src="data:image/png;base64,${fs.readFileSync(path.join(output,x.png)).toString('base64')}"></section>`).join('');
      await page.setContent(`<meta charset="utf-8"><style>body{font-family:Arial;margin:20px;background:#eef1f4}.grid{display:grid;grid-template-columns:repeat(2,1fr);gap:16px}section{background:white;padding:12px;height:390px}h2{font-size:16px;margin:0 0 10px}img{width:100%;height:350px;object-fit:contain;object-position:left top}</style><div class="grid">${cards}</div>`);
      await page.setViewport({width:1800,height:1320,deviceScaleFactor:1});
      const name=`overview-${1+Math.floor(i/6)}.png`;
      await page.screenshot({path:path.join(output,name),fullPage:true});sheets.push(name);
    }
    console.log(JSON.stringify({diagramCount:report.diagramCount,pageCount:rendered.length,overviewSheets:sheets,output}));
  } finally {
    if (browser) await browser.close();
    await new Promise(resolve=>server.close(resolve));
  }
}
main().catch(e=>{console.error(e.stack);process.exitCode=1;});
