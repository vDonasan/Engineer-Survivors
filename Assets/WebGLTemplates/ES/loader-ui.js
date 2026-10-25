// Tela de carregamento, aviso de navegador sem WebGL 2 e mensagens de erro amigáveis.
var ESLoader = (function () {
  function $(id) { return document.getElementById(id); }

  function showError(text) {
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

  function start(loaderUrl, config) {
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
        var p = Math.round(progress * 100);
        $('es-bar-fill').style.width = p + '%';
        $('es-status').textContent = p < 90 ? 'Baixando ' + p + '%' : 'Preparando o laboratório...';
      }).then(function (instance) {
        window.esUnity = instance;
        $('es-loading').hidden = true;
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
