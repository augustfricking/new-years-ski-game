// Live movement straight between browsers (WebRTC data channels through PeerJS), so the other
// players are seen a tenth of a second late instead of seconds. The game decides who to dial
// (everyone in its room; the smaller id dials) and what to say; this file only carries short
// text messages both ways and keeps what came in until the game reads it. When a line cannot
// be opened (some networks block direct connections) the game falls back on the room.
//
// Messages: "p<packet>" a moment of movement, "h<punch>" a punch, "q<ms>" / "a<ms>" a ping and
// its answer (for the round trip shown with the FPS meter). With echo on (a test tab), every
// moment received is sent straight back, four metres to the side: a stand-in to ski beside.
mergeInto(LibraryManager.library, {

  LRLinkInit: function (idPtr, echo) {
    var id = UTF8ToString(idPtr);
    var L = window.LRLink;
    if (L && L.id === id) return;
    if (L && L.peer) { try { L.peer.destroy(); } catch (e) {} }
    L = window.LRLink = { id: id, conns: {}, rtt: {}, inbox: [], state: 1, peer: null, echo: !!echo };

    // Both sides may dial at once: the line the smaller id dialled is the one both keep.
    L.add = function (c) {
      var pid = c.peer;
      var old = L.conns[pid];
      if (old && old !== c) {
        var oldMine = !!old._t, newMine = !!c._t;
        var keepOld = oldMine !== newMine && (oldMine ? L.id < pid : pid < L.id) && !old._closed;
        if (keepOld) { try { c.close(); } catch (e) {} return; }
        try { old.close(); } catch (e) {}
      }
      L.conns[pid] = c;
      c.on('data', function (d) {
        if (typeof d !== 'string' || d.length > 1000) return;
        var k = d.charAt(0);
        if (k === 'q') { try { c.send('a' + d.substring(1)); } catch (e) {} return; }
        if (k === 'a') {
          var ms = Date.now() - parseFloat(d.substring(1));
          if (ms >= 0 && ms < 10000) L.rtt[pid] = L.rtt[pid] ? L.rtt[pid] * 0.7 + ms * 0.3 : ms;
          return;
        }
        if (L.echo && k === 'p') { L.bounce(c, d); return; }
        L.inbox.push(pid + '\t' + d);
        if (L.inbox.length > 4000) L.inbox.splice(0, 2000);
      });
      c.on('close', function () { c._closed = true; if (L.conns[pid] === c) { delete L.conns[pid]; delete L.rtt[pid]; } });
      c.on('error', function () { c._closed = true; if (L.conns[pid] === c) { delete L.conns[pid]; delete L.rtt[pid]; } });
    };

    // The echo: the packet's samples moved four metres to the side (x is a little-endian int16 in
    // twentieths of a metre at bytes 11 + 7i), sent back as this tab's own movement.
    L.bounce = function (c, d) {
      try {
        var raw = atob(d.substring(1)), b = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) b[i] = raw.charCodeAt(i);
        var n = b[9];
        for (var s = 0; s < n; s++) {
          var o = 11 + s * 7;
          var x = (b[o] | (b[o + 1] << 8)) << 16 >> 16;
          x = Math.max(-32768, Math.min(32767, x + 80));
          b[o] = x & 255; b[o + 1] = (x >> 8) & 255;
        }
        var out = '';
        for (var j = 0; j < b.length; j++) out += String.fromCharCode(b[j]);
        c.send('p' + btoa(out));
      } catch (e) {}
    };

    function start() {
      try {
        L.peer = new Peer(id, { debug: 0 });
        L.peer.on('open', function () { L.state = 2; });
        L.peer.on('connection', function (c) { L.add(c); });
        L.peer.on('disconnected', function () { try { L.peer.reconnect(); } catch (e) {} });
        // A peer that cannot be reached is the game's business (it falls back on the room); only a
        // failure before we are on the signalling server stops the link.
        L.peer.on('error', function (e) { if (L.state !== 2 && (!e || e.type !== 'peer-unavailable')) L.state = 3; });
      } catch (e) { L.state = 3; }
    }
    if (window.Peer) start();
    else {
      var s = document.createElement('script');
      s.src = 'https://unpkg.com/peerjs@1.5.4/dist/peerjs.min.js';
      s.onload = start;
      s.onerror = function () { L.state = 3; };
      document.head.appendChild(s);
    }
  },

  LRLinkDial: function (idPtr) {
    var L = window.LRLink; if (!L || L.state !== 2) return;
    var pid = UTF8ToString(idPtr);
    var c = L.conns[pid];
    // Not while a line is open or being dialled, nor over one the other side (the smaller id) is opening.
    if (c && !c._closed && (c.open || (c._t ? Date.now() - c._t < 8000 : pid < L.id))) return;
    try { c = L.peer.connect(pid, { reliable: false, serialization: 'raw' }); if (c) { c._t = Date.now(); L.add(c); } } catch (e) {}
  },

  LRLinkDrop: function (idPtr) {
    var L = window.LRLink; if (!L) return;
    var pid = UTF8ToString(idPtr);
    if (L.conns[pid]) { try { L.conns[pid].close(); } catch (e) {} delete L.conns[pid]; delete L.rtt[pid]; }
  },

  // 1 when a line to that peer is open.
  LRLinkOpen: function (idPtr) {
    var L = window.LRLink; if (!L) return 0;
    var c = L.conns[UTF8ToString(idPtr)];
    return c && c.open ? 1 : 0;
  },

  // Round trip to that peer in milliseconds (0: not measured yet).
  LRLinkRtt: function (idPtr) {
    var L = window.LRLink; if (!L) return 0;
    return L.rtt[UTF8ToString(idPtr)] || 0;
  },

  // To every open line (not from an echo tab: it only answers).
  LRLinkSend: function (textPtr) {
    var L = window.LRLink; if (!L || L.echo) return;
    var t = UTF8ToString(textPtr);
    for (var k in L.conns) { var c = L.conns[k]; if (c.open) { try { c.send(t); } catch (e) {} } }
  },

  LRLinkPing: function () {
    var L = window.LRLink; if (!L) return;
    var t = 'q' + Date.now();
    for (var k in L.conns) { var c = L.conns[k]; if (c.open) { try { c.send(t); } catch (e) {} } }
  },

  // Everything that came in since the last call: "peer<TAB>message" lines, or null.
  LRLinkPoll: function () {
    var L = window.LRLink; if (!L || L.inbox.length === 0) return null;
    var s = L.inbox.join('\n');
    L.inbox.length = 0;
    var n = lengthBytesUTF8(s) + 1, p = _malloc(n);
    stringToUTF8(s, p, n);
    return p;
  },

  // 0 off, 1 starting, 2 ready, 3 failed
  LRLinkState: function () {
    var L = window.LRLink; return L ? L.state : 0;
  }
});
