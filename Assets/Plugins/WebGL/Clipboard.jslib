// Copia o código da sala para a área de transferência do navegador.
mergeInto(LibraryManager.library, {
  ES_CopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function fallback() {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      try { document.execCommand('copy'); } catch (e) { }
      document.body.removeChild(ta);
    }
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(fallback);
    else fallback();
  }
});
