import test from 'node:test';
import assert from 'node:assert/strict';
import { format, expandEmbedded } from './format-json.js';

test('format preserves large integers, exponent notation and escaped text', () => {
  const input = '{"id":9007199254740993,"decimal":1.234567890123456789,"exponent":1e+99,"text":"a \\"b\\"","empty":[],"object":{}}';
  const result = format(input);
  assert.match(result, /9007199254740993/);
  assert.match(result, /1\.234567890123456789/);
  assert.match(result, /1e\+99/);
  assert.match(result, /\n/);
  assert.deepEqual(JSON.parse(result), JSON.parse(input));
});

test('embedded JSON expands for inspection without rewriting the original', () => {
  const input = '{"payload":"{\\"id\\":9007199254740993}","other":"normal"}';
  const expanded = format(expandEmbedded(input));
  assert.match(expanded, /"id": 9007199254740993/);
  assert.equal(input, '{"payload":"{\\"id\\":9007199254740993}","other":"normal"}');
});

test('invalid diagnostics are left unchanged', () => assert.equal(format('not json'), 'not json'));
test('JSON-looking property names remain keys', () => {
  const input = '{"{\\"key\\":1}":"{\\"value\\":2}"}';
  assert.deepEqual(JSON.parse(expandEmbedded(input)), { '{"key":1}': { value: 2 } });
});
