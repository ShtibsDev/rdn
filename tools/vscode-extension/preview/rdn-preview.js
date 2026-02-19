(function () {
  'use strict';

  var ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' };
  function esc(s) { return s.replace(/[&<>"]/g, function (c) { return ESC[c]; }); }
  function span(cls, text) { return '<span class="' + cls + '">' + esc(text) + '</span>'; }

  // Duration needs per-letter tokens
  function highlightDuration(raw) {
    var out = span('rdn-at', '@');
    var rest = raw.slice(1); // remove @
    out += span('rdn-dur-p', rest[0]); // P
    rest = rest.slice(1);
    var re = /(\d+)([YMDHMS])|T/g;
    var m;
    while ((m = re.exec(rest)) !== null) {
      if (m[0] === 'T') {
        out += span('rdn-dur-t', 'T');
      } else {
        out += span('rdn-number', m[1]);
        var letter = m[2];
        var cls = 'rdn-dur-' + ({ Y: 'year', M: 'month', D: 'day', H: 'hour', S: 'second' }[letter] || 'minute');
        // After T, M means minute not month
        if (letter === 'M' && raw.indexOf('T') !== -1 && m.index > rest.indexOf('T')) {
          cls = 'rdn-dur-minute';
        }
        out += span(cls, letter);
      }
    }
    return out;
  }

  // Datetime/TimeOnly - split into components
  function highlightDatetime(raw) {
    var out = span('rdn-at', '@');
    var rest = raw.slice(1);
    // Duration check (already handled separately)
    // Full datetime: 2024-01-15T10:30:00.000Z
    var dtFull = rest.match(/^(\d{4}-\d{2}-\d{2})(T)(\d{2}:\d{2}:\d{2})(\.\d{1,3})?(Z)?$/);
    if (dtFull) {
      out += span('rdn-date', dtFull[1]);
      out += span('rdn-dt-t', dtFull[2]);
      out += span('rdn-time', dtFull[3]);
      if (dtFull[4]) out += span('rdn-ms', dtFull[4]);
      if (dtFull[5]) out += span('rdn-dt-z', dtFull[5]);
      return out;
    }
    // Date only: 2024-01-15
    var dtDate = rest.match(/^(\d{4}-\d{2}-\d{2})$/);
    if (dtDate) { return out + span('rdn-date', dtDate[1]); }
    // Time only: 14:30:00 or 14:30:00.123
    var dtTime = rest.match(/^(\d{2}:\d{2}:\d{2})(\.\d{1,3})?$/);
    if (dtTime) {
      out += span('rdn-time', dtTime[1]);
      if (dtTime[2]) out += span('rdn-ms', dtTime[2]);
      return out;
    }
    // Unix timestamp
    return out + span('rdn-date', rest);
  }

  // Token patterns - order matters (first match wins)
  var patterns = [
    { re: /b"[A-Za-z0-9+\/=]*"/, cls: 'rdn-binary' },
    { re: /x"[0-9A-Fa-f]*"/, cls: 'rdn-binary' },
    { re: /@P(?:\d+Y)?(?:\d+M)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+S)?)?/, handler: highlightDuration },
    { re: /@\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?Z?)?/, handler: highlightDatetime },
    { re: /@\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?/, handler: highlightDatetime },
    { re: /@\d+/, handler: highlightDatetime },
    { re: /\/(?:[^\/\\\n]|\\.)+\/[dgimsuvy]*/, cls: 'rdn-regexp' },
    { re: /"(?:[^"\\\n]|\\.)*"\s*(?=:)/, cls: 'rdn-key' },
    { re: /"(?:[^"\\\n]|\\.)*"/, cls: 'rdn-string' },
    { re: /-?Infinity\b/, cls: 'rdn-special' },
    { re: /NaN\b/, cls: 'rdn-special' },
    { re: /\b(?:Map|Set)(?=\{)/, cls: 'rdn-keyword' },
    { re: /\b(?:true|false)\b/, cls: 'rdn-constant' },
    { re: /\bnull\b/, cls: 'rdn-null' },
    { re: /-?(?:0|[1-9]\d*)n\b/, cls: 'rdn-bigint' },
    { re: /-?(?:0|[1-9]\d*)(?:\.\d+)(?:[eE][+-]?\d+)?/, cls: 'rdn-number' },
    { re: /-?(?:0|[1-9]\d*)[eE][+-]?\d+/, cls: 'rdn-number' },
    { re: /-?(?:0|[1-9]\d*)(?![.\deEn])/, cls: 'rdn-number' },
    { re: /=>/, cls: 'rdn-arrow' },
  ];

  // Build sticky versions for matching at specific positions
  var stickies = patterns.map(function (p) {
    return { re: new RegExp(p.re.source, 'y'), cls: p.cls, handler: p.handler };
  });

  function tokenize(code) {
    var result = '';
    var i = 0;
    while (i < code.length) {
      var matched = false;
      for (var j = 0; j < stickies.length; j++) {
        var s = stickies[j];
        s.re.lastIndex = i;
        var m = s.re.exec(code);
        if (m) {
          if (s.handler) {
            result += s.handler(m[0]);
          } else {
            result += span(s.cls, m[0]);
          }
          i = s.re.lastIndex;
          matched = true;
          break;
        }
      }
      if (!matched) {
        result += esc(code[i]);
        i++;
      }
    }
    return result;
  }

  function highlightAll() {
    var blocks = document.querySelectorAll('code.language-rdn');
    for (var k = 0; k < blocks.length; k++) {
      var block = blocks[k];
      if (block.getAttribute('data-rdn-hl')) continue;
      block.setAttribute('data-rdn-hl', '1');
      block.innerHTML = tokenize(block.textContent || '');
    }
  }

  highlightAll();
  new MutationObserver(highlightAll).observe(document.body, { childList: true, subtree: true });
})();
