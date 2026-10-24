// Avisa a Unity quando a aba fica oculta/visível (o laço WebGL para em abas ocultas).
mergeInto(LibraryManager.library, {
  ES_RegisterVisibility: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    document.addEventListener('visibilitychange', function () {
      SendMessage(goName, 'OnVisibilityChanged', document.hidden ? 'hidden' : 'visible');
    });
  }
});
