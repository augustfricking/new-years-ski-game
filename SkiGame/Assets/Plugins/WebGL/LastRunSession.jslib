// Flags kept for as long as the browser tab is open (sessionStorage): survive a reload, not a new tab.
mergeInto(LibraryManager.library, {
  LastRunSessionGet: function (key) {
    try { return sessionStorage.getItem(UTF8ToString(key)) ? 1 : 0; } catch (e) { return 0; }
  },
  LastRunSessionSet: function (key) {
    try { sessionStorage.setItem(UTF8ToString(key), "1"); } catch (e) { }
  }
});
