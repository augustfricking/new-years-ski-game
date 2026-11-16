// Voice chat between players, peer to peer in the browser (WebRTC through PeerJS).
// The game decides who to call (everyone in the same room) and how loud each one is
// (distance); this file only moves the audio. The microphone is asked for the first time
// the player holds the talk key, and is only live while it is held.
mergeInto(LibraryManager.library, {

  LRVoiceInit: function (idPtr) {
    var id = UTF8ToString(idPtr);
    if (window.LRVoice) return;
    var V = window.LRVoice = { id: id, calls: {}, gains: {}, analysers: {}, els: {}, want: {}, talking: false, ready: false, state: 1, mic: null, micTrack: null, asking: false };

    V.local = function () { return V.mic || V.silent; };

    V.drop = function (pid) {
      if (V.calls[pid]) { try { V.calls[pid].close(); } catch (e) {} }
      if (V.els[pid]) { try { V.els[pid].srcObject = null; } catch (e) {} }
      delete V.calls[pid]; delete V.gains[pid]; delete V.analysers[pid]; delete V.els[pid];
    };

    V.attach = function (call) {
      var pid = call.peer;
      if (V.calls[pid] && V.calls[pid] !== call) { try { V.calls[pid].close(); } catch (e) {} }
      V.calls[pid] = call;
      call.on('stream', function (stream) {
        // Chrome only feeds a remote stream to Web Audio while an element is playing it.
        var el = new Audio(); el.srcObject = stream; el.muted = true;
        var p = el.play(); if (p && p.catch) p.catch(function () {});
        var src = V.ctx.createMediaStreamSource(stream);
        var g = V.ctx.createGain(); g.gain.value = V.want[pid] !== undefined ? V.want[pid] : 1;
        var an = V.ctx.createAnalyser(); an.fftSize = 256;
        src.connect(g); g.connect(an); an.connect(V.ctx.destination);
        V.gains[pid] = g; V.analysers[pid] = an; V.els[pid] = el;
      });
      call.on('close', function () { if (V.calls[pid] === call) V.drop(pid); });
      call.on('error', function () { if (V.calls[pid] === call) V.drop(pid); });
    };

    function start() {
      try {
        V.ctx = new (window.AudioContext || window.webkitAudioContext)();
        V.silent = V.ctx.createMediaStreamDestination().stream;
        V.peer = new Peer(id, { debug: 0 });
        V.peer.on('open', function () { V.ready = true; V.state = 2; });
        V.peer.on('error', function (e) { if (!V.ready) V.state = 3; });
        V.peer.on('disconnected', function () { try { V.peer.reconnect(); } catch (e) {} });
        V.peer.on('call', function (call) { call.answer(V.local()); V.attach(call); });
      } catch (e) { V.state = 3; }
    }
    if (window.Peer) start();
    else {
      var s = document.createElement('script');
      s.src = 'https://unpkg.com/peerjs@1.5.4/dist/peerjs.min.js';
      s.onload = start;
      s.onerror = function () { V.state = 3; };
      document.head.appendChild(s);
    }
  },

  LRVoiceCall: function (idPtr) {
    var V = window.LRVoice; if (!V || !V.ready) return;
    var pid = UTF8ToString(idPtr);
    if (V.calls[pid] && (V.analysers[pid] || Date.now() - (V.calls[pid]._t || 0) < 5000)) return;
    if (V.calls[pid]) V.drop(pid);
    try { var call = V.peer.call(pid, V.local()); if (call) { call._t = Date.now(); V.attach(call); } } catch (e) {}
  },

  LRVoiceHang: function (idPtr) {
    var V = window.LRVoice; if (!V) return;
    V.drop(UTF8ToString(idPtr));
  },

  LRVoiceGain: function (idPtr, gain) {
    var V = window.LRVoice; if (!V) return;
    var pid = UTF8ToString(idPtr);
    V.want[pid] = gain;
    if (V.gains[pid]) V.gains[pid].gain.setTargetAtTime(gain, V.ctx.currentTime, 0.15);
  },

  LRVoiceTalk: function (on) {
    var V = window.LRVoice; if (!V) return;
    V.talking = !!on;
    if (V.ctx && V.ctx.state === 'suspended') V.ctx.resume();
    if (on && !V.mic && !V.asking && navigator.mediaDevices) {
      V.asking = true;
      navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } }).then(function (st) {
        V.mic = st; V.micTrack = st.getAudioTracks()[0]; V.micTrack.enabled = V.talking; V.state = 4;
        // Calls made before the microphone was allowed carry silence: swap the microphone in.
        for (var k in V.calls) {
          var pc = V.calls[k].peerConnection;
          if (!pc) continue;
          pc.getSenders().forEach(function (snd) { if (!snd.track || snd.track.kind === 'audio') snd.replaceTrack(V.micTrack); });
        }
      }).catch(function () { V.state = 5; V.asking = false; });
    }
    if (V.micTrack) V.micTrack.enabled = V.talking;
  },

  LRVoiceLevel: function (idPtr) {
    var V = window.LRVoice; if (!V) return 0;
    var an = V.analysers[UTF8ToString(idPtr)];
    if (!an) return 0;
    var buf = new Uint8Array(an.fftSize);
    an.getByteTimeDomainData(buf);
    var sum = 0;
    for (var i = 0; i < buf.length; i++) { var v = (buf[i] - 128) / 128; sum += v * v; }
    return Math.sqrt(sum / buf.length);
  },

  // 0 off, 1 starting, 2 ready, 3 failed, 4 microphone on, 5 microphone refused
  LRVoiceState: function () {
    var V = window.LRVoice; if (!V) return 0;
    return V.state;
  },

  LRVoiceConnected: function (idPtr) {
    var V = window.LRVoice; if (!V) return 0;
    return V.analysers[UTF8ToString(idPtr)] ? 1 : 0;
  }
});
