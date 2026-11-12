// Validate and format lexically; never reserialize JSON numbers in JavaScript.
export const format = source => {
  try { JSON.parse(source); } catch { return source; }
  let result = '', depth = 0, inString = false, escaped = false;
  const indent = () => '  '.repeat(depth);
  for (let i = 0; i < source.length; i++) {
    const ch = source[i];
    if (inString) { result += ch; if (escaped) escaped = false; else if (ch === '\\') escaped = true; else if (ch === '"') inString = false; continue; }
    if (ch === '"') { inString = true; result += ch; }
    else if (ch === '{' || ch === '[') { result += ch; let next = i + 1; while (next < source.length && /\s/.test(source[next])) next++; if (source[next] !== '}' && source[next] !== ']') { depth++; result += '\n' + indent(); } else depth++; }
    else if (ch === '}' || ch === ']') { depth = Math.max(0, depth - 1); if (!/[{[]$/.test(result)) result += '\n' + indent(); result += ch; }
    else if (ch === ',') result += ',\n' + indent();
    else if (ch === ':') result += ': ';
    else if (!/\s/.test(ch)) result += ch;
  }
  return result;
};

export const expandEmbedded = source => source.replace(/"(?:\\.|[^"\\])*"/g, (token, offset) => {
  let next = offset + token.length;
  while (next < source.length && /\s/.test(source[next])) next++;
  if (source[next] === ':') return token;
  try { const value = JSON.parse(token); if (typeof value !== 'string' || !/^[\s]*[{[]/.test(value)) return token; JSON.parse(value); return value; }
  catch { return token; }
});
