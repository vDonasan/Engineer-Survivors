// Tela de carregamento animada (bateria, desfile dos heróis, itens e dicas), aviso de navegador sem WebGL 2
// e mensagens de erro amigáveis.
var ESLoader = (function () {
  function $(id) { return document.getElementById(id); }

  function showError(text) {
    clearInterval(tipTimer); clearInterval(lootTimer);
    $('es-loading').hidden = true;
    $('es-error-text').textContent = text;
    $('es-error').hidden = false;
  }

  function friendly(message) {
    var m = String(message || '');
    if (/WebGL/i.test(m)) return 'Seu navegador ou placa de vídeo não ativou o WebGL 2. Use Chrome, Edge ou Firefox atualizados e verifique se a aceleração de hardware está ligada.';
    if (/memory|memória|Out of memory/i.test(m)) return 'O navegador ficou sem memória para carregar o jogo. Feche outras abas e recarregue.';
    if (/Content-Encoding|decompress|brotli|gzip/i.test(m)) return 'O servidor entregou os arquivos do jogo sem a compressão correta. Avise quem publicou o jogo.';
    if (/fetch|network|Failed to load|404/i.test(m)) return 'Falha ao baixar os arquivos do jogo. Verifique sua conexão e recarregue a página.';
    return 'Erro inesperado ao carregar: ' + m;
  }

  function banner(msg, type) {
    if (type !== 'error' && type !== 'warning') return;
    var div = document.createElement('div');
    div.className = 'es-banner-' + type;
    div.textContent = msg;
    $('es-banner').appendChild(div);
    setTimeout(function () { div.remove(); }, type === 'error' ? 12000 : 6000);
  }

  function hasWebGL2() {
    try { return !!document.createElement('canvas').getContext('webgl2'); } catch (e) { return false; }
  }

  // ——— animações da tela de carregamento ———
  var TIPS = [
    'Ligando os equipamentos do laboratório', 'Calibrando o osciloscópio', 'Esquentando o café expresso',
    'Convencendo o Arduino a compilar', 'Soldando os últimos resistores', 'Contando os microbôs da horda',
    'Afinando o motor do Astra', 'Procurando o professor no laboratório', 'Explicando o projeto pela 3ª vez',
    'Colocando o chapéu de palha', 'Reiniciando o roteador da faculdade', 'Gelando a tequila (para a pesquisa)'
  ];
  var CELLS = 20, cellEls = [], tipTimer = 0, lootTimer = 0;

  function setupLoading() {
    var cells = $('es-cells');
    for (var i = 0; i < CELLS; i++) { var c = document.createElement('div'); c.className = 'es-cell'; cells.appendChild(c); cellEls.push(c); }
    // desfile: heróis em fila com velocidades parecidas, a horda de microbôs logo atrás
    var parade = $('es-parade'), heroes = 10, base = 14;
    var order = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9].sort(function () { return Math.random() - 0.5; });
    for (var k = 0; k < heroes; k++) {
      var r = document.createElement('div');
      r.className = 'es-runner';
      r.style.backgroundPositionY = (-order[k] * 96) + 'px';
      r.style.setProperty('--dur', base + 's');
      r.style.setProperty('--delay', (-k * base / heroes) + 's');
      parade.appendChild(r);
      var b = document.createElement('div');
      b.className = 'es-runner bot';
      b.style.backgroundPositionY = (-10 * 72) + 'px';
      b.style.setProperty('--dur', base + 's');
      b.style.setProperty('--delay', (-k * base / heroes - 0.55) + 's');
      parade.appendChild(b);
    }
    var tip = 0;
    tipTimer = setInterval(function () { tip = (tip + 1) % TIPS.length; type(TIPS[tip]); }, 2600);
    lootTimer = setInterval(spawnLoot, 650);
  }

  function type(text) {
    var el = $('es-tip'), n = 0;
    el.textContent = '';
    var t = setInterval(function () { n++; el.textContent = text.slice(0, n); if (n >= text.length) clearInterval(t); }, 28);
  }

  function spawnLoot() {
    var el = document.createElement('div');
    el.className = 'es-item';
    el.style.backgroundPositionX = (-Math.floor(Math.random() * 12) * 64) + 'px';
    var side = Math.random() < 0.5 ? Math.random() * 26 : 74 + Math.random() * 22;
    el.style.left = side + 'vw';
    el.style.top = (55 + Math.random() * 25) + 'vh';
    el.style.setProperty('--dur', (4 + Math.random() * 3) + 's');
    $('es-loot').appendChild(el);
    setTimeout(function () { el.remove(); }, 7500);
  }

  function progressTo(p) {
    var lit = Math.round(p * CELLS);
    for (var i = 0; i < CELLS; i++) cellEls[i].className = 'es-cell' + (i < lit ? (p >= 1 ? ' full' : ' on') : '');
    $('es-pct').textContent = Math.round(p * 100);
  }

  function finish() {
    clearInterval(tipTimer); clearInterval(lootTimer);
    progressTo(1);
    type('Laboratório pronto. Boa sorte!');
    var el = $('es-loading');
    setTimeout(function () { el.classList.add('es-done'); }, 450);
    setTimeout(function () { el.hidden = true; }, 1100);
  }

  function start(loaderUrl, config) {
    setupLoading();
    $('es-reload').onclick = function () { location.reload(); };
    if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
      banner('Celulares e tablets não são suportados nesta versão: use um computador com teclado.', 'warning');
    }
    if (!hasWebGL2()) {
      showError(friendly('WebGL'));
      return;
    }
    config.showBanner = banner;
    var canvas = $('unity-canvas');
    var script = document.createElement('script');
    script.src = loaderUrl;
    script.onerror = function () { showError(friendly('Failed to load ' + loaderUrl)); };
    script.onload = function () {
      createUnityInstance(canvas, config, function (progress) {
        progressTo(Math.min(0.99, progress));
      }).then(function (instance) {
        window.esUnity = instance;
        finish();
        canvas.focus();
      }).catch(function (message) {
        showError(friendly(message));
      });
    };
    document.body.appendChild(script);
    canvas.addEventListener('pointerdown', function () { canvas.focus(); });
  }

  return { start: start };
})();
