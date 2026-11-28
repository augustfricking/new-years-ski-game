// What kind of device the game is played on: a finger (phone, tablet) or a mouse, and how big the screen is.
mergeInto(LibraryManager.library, {

  // 1 when the main pointer is a finger (a laptop with a touch screen still has a mouse: 0).
  LRCoarsePointer: function () {
    try { return window.matchMedia && window.matchMedia('(pointer: coarse)').matches ? 1 : 0; } catch (e) { return 0; }
  },

  // The short side of the screen in CSS pixels (a phone is under about 600, a tablet over 700).
  LRShortSide: function () {
    try { return Math.min(window.screen.width, window.screen.height); } catch (e) { return 1000; }
  },

  // Full screen on a finger device: 2 = the page can take it (the page does at the next tap, see index.html),
  // 1 = it cannot but runs from the home screen already (no bars), 0 = it cannot (an iPhone's Safari).
  LRFullscreenKind: function () {
    try {
      var root = document.documentElement;
      if ((root.requestFullscreen || root.webkitRequestFullscreen) && (document.fullscreenEnabled || document.webkitFullscreenEnabled)) return 2;
      var home = (window.matchMedia && (matchMedia('(display-mode: fullscreen)').matches || matchMedia('(display-mode: standalone)').matches)) || navigator.standalone === true;
      return home ? 1 : 0;
    } catch (e) { return 0; }
  },

  // Whether the player wants the whole screen (kept in the page's own storage, read before the game loads).
  LRFullscreenWanted: function () {
    try { return localStorage.getItem('lastrun-fullscreen') === '0' ? 0 : 1; } catch (e) { return 1; }
  },

  LRSetFullscreenWanted: function (on) {
    try { localStorage.setItem('lastrun-fullscreen', on ? '1' : '0'); } catch (e) {}
    if (on) return;   // taken at the next tap
    try {
      if (document.fullscreenElement) document.exitFullscreen();
      else if (document.webkitFullscreenElement) document.webkitExitFullscreen();
    } catch (e) {}
  }
});
