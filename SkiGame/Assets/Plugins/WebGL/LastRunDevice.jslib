// What kind of device the game is played on: a finger (phone, tablet) or a mouse, and how big the screen is.
mergeInto(LibraryManager.library, {

  // 1 when the main pointer is a finger (a laptop with a touch screen still has a mouse: 0).
  LRCoarsePointer: function () {
    try { return window.matchMedia && window.matchMedia('(pointer: coarse)').matches ? 1 : 0; } catch (e) { return 0; }
  },

  // The short side of the screen in CSS pixels (a phone is under about 600, a tablet over 700).
  LRShortSide: function () {
    try { return Math.min(window.screen.width, window.screen.height); } catch (e) { return 1000; }
  }
});
